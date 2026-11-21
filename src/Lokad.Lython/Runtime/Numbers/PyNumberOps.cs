using System.Globalization;
using System.Numerics;
using System.Text;

namespace Lokad.Lython.Runtime.Numbers;

internal static class PyNumberOps
{
    // Ingress keeps these CLR scalars for public round trips. Numeric consumers
    // must nevertheless treat all of them as exact Python integers.
    public static bool IsInteger(object? value)
        => value is bool or BigInteger or sbyte or byte or short or ushort or int or uint or long or ulong;

    public static bool TryAsNumber(object value, out PyNumber number)
    {
        switch (value)
        {
            case bool boolean:
                number = PyNumber.FromBoolean(boolean);
                return true;
            case BigInteger integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case sbyte integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case byte integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case short integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case ushort integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case int integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case uint integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case long integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case ulong integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case double floating:
                number = PyNumber.FromFloat(floating);
                return true;
            default:
                number = default;
                return false;
        }
    }

    public static bool TryAsInteger(object value, out BigInteger integer)
    {
        if (TryAsNumber(value, out var number) && !number.IsFloat)
        {
            integer = number.Integer;
            return true;
        }

        integer = default;
        return false;
    }

    // Like CPython, converting an out-of-range integer to float fails
    // instead of saturating to infinity; the boundary funnels report the
    // BCL message with their own span.
    internal static double ToDoubleChecked(PyNumber number)
        => number.IsFloat ? number.Floating : BigIntegerToDouble(number.Integer);

    // Non-throwing twin for callers that translate conversion overflow themselves
    // instead of matching the overflow message text (N16).
    internal static bool TryToDoubleChecked(PyNumber number, out double value)
    {
        try
        {
            value = ToDoubleChecked(number);
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    // Correctly rounded like CPython (the BCL cast clamps near the range
    // top and rounds some magnitudes down by one ulp); ties go to even and
    // true overflow raises.
    internal static double BigIntegerToDouble(BigInteger value)
    {
        if (value >= -9007199254740992L && value <= 9007199254740992L)
        {
            return (double)value;
        }

        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        var bitLength = magnitude.GetBitLength();
        if (bitLength > 1024)
        {
            // Past 1024 bits every rounding lands above double.MaxValue.
            throw new OverflowException("int too large to convert to float");
        }

        var shift = (int)bitLength - 53;
        var truncated = magnitude >> shift;
        var dropped = magnitude & ((BigInteger.One << shift) - BigInteger.One);
        var halfway = BigInteger.One << (shift - 1);
        if (dropped > halfway || (dropped == halfway && !truncated.IsEven))
        {
            truncated += BigInteger.One;
        }

        // A round-up past 2**53 renormalizes to 2**52 with the next exponent.
        var exponent = (int)bitLength - 1;
        if (truncated == (BigInteger.One << 53))
        {
            truncated = BigInteger.One << 52;
            exponent += 1;
        }

        if (exponent > 1023)
        {
            throw new OverflowException("int too large to convert to float");
        }

        var result = Math.ScaleB((double)(ulong)truncated, exponent - 52);
        return negative ? -result : result;
    }

    public static object Add(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer + rhs.Integer : ToDoubleChecked(lhs) + ToDoubleChecked(rhs);

    public static object TrueDivide(PyNumber lhs, PyNumber rhs, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            if (rhs.IsZero) throw new DivideByZeroException();
            return DivideIntegersToFloat(lhs.Integer, rhs.Integer, context, span);
        }

        var left = ToDoubleChecked(lhs);
        var right = ToDoubleChecked(rhs);
        if (right == 0.0) throw new DivideByZeroException();
        return left / right;
    }

    private static double DivideIntegersToFloat(BigInteger left, BigInteger right,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        const long exactLimit = 9007199254740992L;
        if (left >= -exactLimit && left <= exactLimit && right >= -exactLimit && right <= exactLimit)
            return (double)left / (double)right;

        var negative = (left.Sign < 0) != (right.Sign < 0);
        var numerator = BigInteger.Abs(left);
        var denominator = BigInteger.Abs(right);
        var numeratorBits = numerator.GetBitLength();
        var denominatorBits = denominator.GetBitLength();
        var difference = numeratorBits - denominatorBits;
        if (numerator.IsZero || difference < -1075) return negative ? -0.0 : 0.0;
        if (difference > 1024) throw new OverflowException("integer division result too large for a float");

        // Alignment, quotient/remainder, division workspace and rounding overlap.
        // Small fixed-size intermediates fit the execution's scalar headroom.
        context.CheckExecution(span);
        var majorBits = Math.Max(numeratorBits, denominatorBits);
        using var scratch = majorBits > 64 ? context.MemoryGovernor.ReserveTemporary(
            RuntimeMemoryEstimates.SaturatingMultiply(8, RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(
                RuntimeMemoryEstimates.SaturatingAdd(majorBits, 1075))), span) : null;
        var exponent = (int)difference;
        if (exponent >= 0 ? numerator < (denominator << exponent) : (numerator << -exponent) < denominator)
            exponent--;
        if (exponent > 1023) throw new OverflowException("integer division result too large for a float");
        // Normal results carry 53 significant bits; subnormals round on the
        // fixed 2**-1074 grid, avoiding a second rounding during underflow.
        var shift = exponent >= -1022 ? 52 - exponent : 1074;
        context.CheckExecution(span);
        if (shift >= 0) numerator <<= shift;
        else denominator <<= -shift;
        context.CheckExecution(span);
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        context.CheckExecution(span);
        var halfway = (remainder << 1).CompareTo(denominator);
        if (halfway > 0 || halfway == 0 && !quotient.IsEven) quotient++;
        var result = Math.ScaleB((double)(ulong)quotient, -shift);
        if (double.IsInfinity(result)) throw new OverflowException("integer division result too large for a float");
        return negative ? -result : result;
    }

    // Adapted from CPython's _float_div_mod (PSF license; see third-party notices).
    // Derive the quotient from the remainder, then correct roundoff near an integer.
    internal static (double Quotient, double Remainder) FloatDivMod(double left, double right)
    {
        if (right == 0.0) throw new DivideByZeroException();
        var remainder = left % right;
        var quotient = (left - remainder) / right;
        if (remainder != 0.0)
        {
            if ((right < 0.0) != (remainder < 0.0))
            {
                remainder += right;
                quotient -= 1.0;
            }
        }
        else remainder = Math.CopySign(0.0, right);
        if (quotient != 0.0)
        {
            var floor = Math.Floor(quotient);
            if (quotient - floor > 0.5) floor += 1.0;
            return (floor, remainder);
        }
        return (Math.CopySign(0.0, left / right), remainder);
    }

    public static object Power(PyNumber lhs, PyNumber rhs)
    {
        if (!lhs.IsFloat && !rhs.IsFloat && rhs.Integer >= BigInteger.Zero)
        {
            if (rhs.Integer.IsZero) return BigInteger.One;
            if (lhs.Integer.IsZero || lhs.Integer.IsOne) return lhs.Integer;
            if (lhs.Integer == BigInteger.MinusOne) return rhs.Integer.IsEven ? BigInteger.One : BigInteger.MinusOne;
            return BigInteger.Pow(lhs.Integer, checked((int)rhs.Integer));
        }

        return FloatPower(ToDoubleChecked(lhs), ToDoubleChecked(rhs));
    }

    internal static double FloatPower(double left, double right)
    {
        if (left == 0.0 && double.IsFinite(right) && right < 0.0) throw new DivideByZeroException();
        var result = Math.Pow(left, right);
        if (double.IsInfinity(result) && double.IsFinite(left) && double.IsFinite(right))
            throw new OverflowException("numerical result out of range");
        return result;
    }

    public static BigInteger BitwiseOr(BigInteger lhs, BigInteger rhs) => lhs | rhs;

    public static BigInteger BitwiseXor(BigInteger lhs, BigInteger rhs) => lhs ^ rhs;

    public static BigInteger BitwiseAnd(BigInteger lhs, BigInteger rhs) => lhs & rhs;

    public static BigInteger LeftShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new PyNegativeShiftException("negative shift count");
        }

        return lhs.IsZero ? BigInteger.Zero : lhs << checked((int)rhs);
    }

    public static BigInteger RightShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new PyNegativeShiftException("negative shift count");
        }

        return rhs >= RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs)
            ? lhs.Sign < 0 ? BigInteger.MinusOne : BigInteger.Zero
            : lhs >> checked((int)rhs);
    }

    public static int Compare(PyNumber lhs, PyNumber rhs)
        => TryCompare(lhs, rhs, out var comparison) ? comparison : 0;

    public static bool AreEqual(PyNumber lhs, PyNumber rhs)
        => TryCompare(lhs, rhs, out var comparison) && comparison == 0;

    public static bool TryCompare(PyNumber lhs, PyNumber rhs, out int comparison)
    {
        if (lhs.IsFloat && double.IsNaN(lhs.Floating) || rhs.IsFloat && double.IsNaN(rhs.Floating))
        {
            comparison = 0;
            return false;
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            comparison = lhs.Integer.CompareTo(rhs.Integer);
            return true;
        }

        if (!lhs.IsFloat)
        {
            comparison = CompareIntegerToFloat(lhs.Integer, rhs.Floating);
            return true;
        }

        if (!rhs.IsFloat)
        {
            comparison = -CompareIntegerToFloat(rhs.Integer, lhs.Floating);
            return true;
        }

        comparison = lhs.Floating.CompareTo(rhs.Floating);
        return true;
    }

    private static int CompareIntegerToFloat(BigInteger integer, double floating)
    {
        if (double.IsPositiveInfinity(floating))
        {
            return -1;
        }

        if (double.IsNegativeInfinity(floating))
        {
            return 1;
        }

        var truncated = new BigInteger(floating);
        var comparison = integer.CompareTo(truncated);
        if (comparison != 0 || floating == Math.Truncate(floating))
        {
            return comparison;
        }

        return floating > 0 ? -1 : 1;
    }

    public static object Negate(PyNumber number)
        => number.IsFloat ? -number.Floating : -number.Integer;

    public static BigInteger BitwiseNot(BigInteger integer) => ~integer;

    // Enough for an invariant round-trip binary64, including sign, decimal
    // point, exponent and Python's explicit .0 for fixed integer values.
    internal const int FloatRenderCapacity = 32;

    public static string RenderFloat(double value)
    {
        Span<byte> bytes = stackalloc byte[FloatRenderCapacity];
        return Encoding.UTF8.GetString(bytes[..WriteFloatUtf8(value, bytes)]);
    }

    internal static int WriteFloatUtf8(double value, Span<byte> output)
    {
        if (!double.IsFinite(value))
        {
            var spelling = double.IsNaN(value) ? "nan"u8 : double.IsNegative(value) ? "-inf"u8 : "inf"u8;
            spelling.CopyTo(output);
            return spelling.Length;
        }
        if (!value.TryFormat(output, out var length, "R", CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Float formatting exceeded its fixed buffer.");

        var rendered = output[..length];
        var exponent = rendered.IndexOf((byte)'E');
        if (exponent >= 0)
        {
            rendered[exponent] = (byte)'e';
            return length;
        }
        if (rendered.Contains((byte)'.')) return length;

        var firstDigit = rendered[0] == (byte)'-' ? 1 : 0;
        if (length - firstDigit != 17)
        {
            ".0"u8.CopyTo(output[length..]);
            return length + 2;
        }

        // Invariant BCL round-trip keeps some 17-digit integers in fixed
        // notation. CPython prints a scientific exponent starting at 1e16.
        var end = length;
        while (end > firstDigit + 1 && output[end - 1] == (byte)'0') end--;
        if (end > firstDigit + 1)
        {
            output[(firstDigit + 1)..end].CopyTo(output[(firstDigit + 2)..]);
            output[firstDigit + 1] = (byte)'.';
            end++;
        }
        "e+16"u8.CopyTo(output[end..]);
        return end + 4;
    }

    public static int GetHashCode(PyNumber number)
    {
        if (number.IsFloat)
        {
            var value = number.ToDouble();
            if (double.IsFinite(value) && Math.Truncate(value) == value)
            {
                return ((BigInteger)value).GetHashCode();
            }

            return value.GetHashCode();
        }

        return number.Integer.GetHashCode();
    }

    internal static BigInteger ModuloIntegers(BigInteger left, BigInteger right)
    {
        // BCL remainder has the dividend's sign; Python uses the divisor's.
        // Avoid constructing the full quotient and product to obtain it.
        var remainder = left % right;
        return !remainder.IsZero && remainder.Sign != right.Sign ? remainder + right : remainder;
    }

    internal static BigInteger FloorDivideIntegers(BigInteger left, BigInteger right)
    {
        var quotient = BigInteger.DivRem(left, right, out var remainder);
        if (remainder != BigInteger.Zero && ((remainder > BigInteger.Zero) != (right > BigInteger.Zero)))
        {
            quotient -= BigInteger.One;
        }

        return quotient;
    }
}

// N16: negative-shift failures carry this marker from their origin so catch
// sites recognize them by type instead of comparing English message text.
// Deriving from InvalidOperationException keeps any unknown CLR-level catcher
// behaving as before. The public ValueError identity and message flow from
// this origin like any guest-visible failure.
internal sealed class PyNegativeShiftException : InvalidOperationException
{
    public PyNegativeShiftException(string message)
        : base(message)
    {
    }
}
