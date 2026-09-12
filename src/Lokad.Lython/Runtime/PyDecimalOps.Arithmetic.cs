using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    internal static LythonRuntimeException DivisionByZero(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "DivisionByZero"), message, span);

    internal static LythonRuntimeException InvalidOperation(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "InvalidOperation"), message, span);

    internal static LythonRuntimeException DecimalOverflow(LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "Overflow"), "Decimal arithmetic overflowed Lython's fixed-precision range.", span);

    public static object Add(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs + rhs, static (lhs, rhs) => Math.Min(lhs, rhs));

    public static object Subtract(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs - rhs, static (lhs, rhs) => Math.Min(lhs, rhs));

    public static object Multiply(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs * rhs, static (lhs, rhs) => checked(lhs + rhs));

    public static object Divide(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw DivisionByZero("decimal division by zero", span);
        }

        try
        {
            return new PyDecimal(lhs / rhs);
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    public static object Modulo(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw DivisionByZero("decimal modulo by zero", span);
        }

        try
        {
            return new PyDecimal(lhs % rhs, Math.Min(GetOperandExponent(left, lhs), GetOperandExponent(right, rhs)));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    public static object Power(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !Numbers.PyNumberOps.TryAsInteger(right, out var exponent))
        {
            throw new LythonRuntimeException("TypeError", "Decimal power requires a Decimal base and an integer exponent.", span);
        }

        if (exponent < int.MinValue || exponent > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal exponent is too large.", span);
        }

        var exponentInt = (int)exponent;
        if (exponentInt == 0)
        {
            return new PyDecimal(1m);
        }

        if (exponentInt < 0)
        {
            if (lhs == 0m)
            {
                throw DivisionByZero("decimal division by zero", span);
            }

            try
            {
                return new PyDecimal(1m / Pow(lhs, -exponentInt));
            }
            catch (Exception ex) when (ex is OverflowException or DivideByZeroException)
            {
                // An underflowed divisor means a reciprocal past the range.
                throw DecimalOverflow(span);
            }
        }

        try
        {
            var result = Pow(lhs, exponentInt);
            return new PyDecimal(result, ConsistentExponent(result, checked(GetOperandExponent(left, lhs) * exponentInt)));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static LythonRuntimeException CompareFailed(string? operation, object left, object right, LythonSourceSpan span)
        => operation is null
            ? new LythonRuntimeException("TypeError", "Values are not comparable.", span)
            : RuntimeErrors.UnsupportedComparison(operation, left, right, span);

    public static int Compare(object left, object right, LythonSourceSpan span, string? operation = null)
    {
        if (TryAsDecimal(left, out var lhs) && TryAsDecimal(right, out var rhs))
        {
            return lhs.CompareTo(rhs);
        }

        if (TryCompareMixed(left, right, out var mixed))
        {
            return mixed;
        }

        throw CompareFailed(operation, left, right, span);
    }

    public static bool AreEqual(object left, object right)
    {
        if (TryAsDecimal(left, out var lhs) && TryAsDecimal(right, out var rhs))
        {
            return lhs == rhs;
        }

        return TryCompareMixed(left, right, out var mixed) && mixed == 0;
    }

    // Exact ordering across decimal, float, integer and boolean domains for
    // mixes BCL decimal cannot hold (floats outside decimal range or
    // precision, huge integers). NaN is unordered and anything else unknown,
    // so callers keep their existing failure. Infinities order against
    // everything through a zero denominator of the infinity sign.
    internal static bool TryCompareMixed(object left, object right, out int comparison)
    {
        if (!TryExactRational(left, out var leftNum, out var leftDen) ||
            !TryExactRational(right, out var rightNum, out var rightDen))
        {
            comparison = 0;
            return false;
        }
        if (leftDen.IsZero && rightDen.IsZero)
        {
            comparison = leftNum.CompareTo(rightNum);
            return true;
        }
        if (leftDen.IsZero)
        {
            comparison = leftNum.Sign;
            return true;
        }
        if (rightDen.IsZero)
        {
            comparison = -rightNum.Sign;
            return true;
        }
        comparison = (leftNum * rightDen).CompareTo(rightNum * leftDen);
        return true;
    }

    private static bool TryExactRational(object value, out BigInteger numerator, out BigInteger denominator)
    {
        switch (value)
        {
            case bool boolean:
                numerator = boolean ? BigInteger.One : BigInteger.Zero;
                denominator = BigInteger.One;
                return true;
            case BigInteger integer:
                numerator = integer;
                denominator = BigInteger.One;
                return true;
            case PyDecimal pyDecimal:
                (numerator, denominator) = ExactDecimalParts(pyDecimal.Value);
                return true;
            case double floating when double.IsNaN(floating):
            default:
                numerator = BigInteger.Zero;
                denominator = BigInteger.One;
                return false;
            case double floating when double.IsPositiveInfinity(floating):
                numerator = BigInteger.One;
                denominator = BigInteger.Zero;
                return true;
            case double floating when double.IsNegativeInfinity(floating):
                numerator = BigInteger.MinusOne;
                denominator = BigInteger.Zero;
                return true;
            case double floating:
                (numerator, denominator) = ExactDoubleParts(floating);
                return true;
        }
    }

    private static (BigInteger Numerator, BigInteger Denominator) ExactDoubleParts(double floating)
    {
        // Finite doubles expand exactly: the 52-bit mantissa (plus the
        // implicit leading one, absent for subnormals) scaled by the
        // unbiased binary exponent.
        var bits = BitConverter.DoubleToInt64Bits(floating);
        var rawExponent = (int)((bits >> 52) & 0x7FF);
        var significand = new BigInteger(bits & 0xFFFFFFFFFFFFF);
        int binaryExponent;
        if (rawExponent == 0)
        {
            binaryExponent = -1074;
        }
        else
        {
            significand |= new BigInteger(0x10000000000000);
            binaryExponent = rawExponent - 1075;
        }
        var numerator = bits < 0 ? -significand : significand;
        return binaryExponent >= 0
            ? (numerator * BigInteger.Pow(2, binaryExponent), BigInteger.One)
            : (numerator, BigInteger.Pow(2, -binaryExponent));
    }

    private static (BigInteger Numerator, BigInteger Denominator) ExactDecimalParts(decimal value)
    {
        // BCL decimals decode exactly through their 96-bit integer plus
        // scale, so ordering never rounds through double or overflows.
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0x7F;
        var unscaled = new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) + (new BigInteger((uint)bits[2]) << 64);
        var numerator = bits[3] < 0 ? -unscaled : unscaled;
        return (numerator, BigInteger.Pow(10, scale));
    }

    // BCL multiplication rounds to fit instead of throwing, so a declared
    // exponent can overstate the stored scale and poison later rendering;
    // clamp it to the stored scale (a no-op for exact results and zeros).
    private static int ConsistentExponent(decimal value, int declared)
        => value == 0m || declared >= 0 ? declared : Math.Max(declared, -GetScale(value));

    private static object Binary(
        object left,
        object right,
        LythonSourceSpan span,
        Func<decimal, decimal, decimal> operation,
        Func<int, int, int> combineExponent)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        try
        {
            var result = operation(lhs, rhs);
            return new PyDecimal(
                result,
                ConsistentExponent(result, combineExponent(GetOperandExponent(left, lhs), GetOperandExponent(right, rhs))));
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static int GetOperandExponent(object value, decimal numericValue)
        => value is PyDecimal pyDecimal ? pyDecimal.Exponent : -GetScale(numericValue);

    private static decimal Pow(decimal value, int exponent)
    {
        decimal result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }
}
