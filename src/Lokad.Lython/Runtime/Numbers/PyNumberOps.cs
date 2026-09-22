using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime.Numbers;

internal static class PyNumberOps
{
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

    public static object Subtract(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer - rhs.Integer : ToDoubleChecked(lhs) - ToDoubleChecked(rhs);

    public static object Multiply(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer * rhs.Integer : ToDoubleChecked(lhs) * ToDoubleChecked(rhs);

    public static object TrueDivide(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            var result = lhs.ToDouble() / rhs.ToDouble();
            if (!double.IsFinite(result))
            {
                throw new OverflowException("integer division result too large for a float");
            }

            return result;
        }

        return ToDoubleChecked(lhs) / ToDoubleChecked(rhs);
    }

    public static object FloorDivide(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            return FloorDivideIntegers(lhs.Integer, rhs.Integer);
        }

        return Math.Floor(ToDoubleChecked(lhs) / ToDoubleChecked(rhs));
    }

    public static object Modulo(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            return lhs.Integer - FloorDivideIntegers(lhs.Integer, rhs.Integer) * rhs.Integer;
        }

        var left = ToDoubleChecked(lhs);
        var right = ToDoubleChecked(rhs);
        if (double.IsFinite(left) && double.IsInfinity(right))
        {
            if (left == 0.0 || Math.CopySign(1.0, left) == Math.CopySign(1.0, right))
            {
                return left;
            }

            return right;
        }

        var quotient = Math.Floor(left / right);
        return left - quotient * right;
    }

    public static object Power(PyNumber lhs, PyNumber rhs)
    {
        if (!lhs.IsFloat && !rhs.IsFloat && rhs.Integer >= BigInteger.Zero)
        {
            return BigInteger.Pow(lhs.Integer, checked((int)rhs.Integer));
        }

        return Math.Pow(ToDoubleChecked(lhs), ToDoubleChecked(rhs));
    }

    public static BigInteger BitwiseOr(BigInteger lhs, BigInteger rhs) => lhs | rhs;

    public static BigInteger BitwiseXor(BigInteger lhs, BigInteger rhs) => lhs ^ rhs;

    public static BigInteger BitwiseAnd(BigInteger lhs, BigInteger rhs) => lhs & rhs;

    public static BigInteger LeftShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new InvalidOperationException("negative shift count");
        }

        return lhs << checked((int)rhs);
    }

    public static BigInteger RightShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new InvalidOperationException("negative shift count");
        }

        return lhs >> checked((int)rhs);
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

    public static BigInteger ParseInteger(string text)
    {
        var digits = text.Replace("_", string.Empty, StringComparison.Ordinal);
        if (digits.Length > 2 && digits[0] == '0')
        {
            var radix = digits[1] switch
            {
                'x' or 'X' => 16,
                'o' or 'O' => 8,
                'b' or 'B' => 2,
                _ => 0,
            };

            if (radix != 0)
            {
                var magnitude = BigInteger.Zero;
                foreach (var digit in digits.Substring(2))
                {
                    magnitude = magnitude * radix + DigitValue(digit);
                }

                return magnitude;
            }
        }

        return BigInteger.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static int DigitValue(char digit)
        => digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => throw new InvalidOperationException($"Non-hex digit {digit} in prefixed integer literal."),
        };

    public static double ParseFloat(string text)
        => double.Parse(text.Replace("_", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

    public static string RenderFloat(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        var rendered = value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
        var sign = string.Empty;
        if (rendered.StartsWith('-'))
        {
            sign = "-";
            rendered = rendered[1..];
        }

        if (!rendered.Contains('e', StringComparison.Ordinal))
        {
            // BCL round-trip stays fixed through 1e17; CPython repr switches
            // at 1e16, which surfaces here as exactly a 17-digit integer.
            var point = rendered.IndexOf('.');
            if (point < 0 && rendered.Length == 17)
            {
                var mantissa = (rendered[0] + "." + rendered[1..]).TrimEnd('0').TrimEnd('.');
                return sign + mantissa + "e+16";
            }
        }

        return sign + (rendered.Contains('.') || rendered.Contains('e') ? rendered : rendered + ".0");
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

    private static BigInteger FloorDivideIntegers(BigInteger left, BigInteger right)
    {
        var quotient = BigInteger.DivRem(left, right, out var remainder);
        if (remainder != BigInteger.Zero && ((remainder > BigInteger.Zero) != (right > BigInteger.Zero)))
        {
            quotient -= BigInteger.One;
        }

        return quotient;
    }
}
