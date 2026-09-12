using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    internal static LythonRuntimeException DivisionByZero(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "DivisionByZero"), message, span);

    internal static LythonRuntimeException InvalidOperation(string message, LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "InvalidOperation"), message, span);

    internal static LythonRuntimeException DecimalOverflow(LythonSourceSpan span)
        => new(LythonRuntime.ModuleException("decimal", "Overflow"), "Decimal arithmetic overflowed Lython's fixed-precision range.", span);

    // Zero to the zeroth power signals plain decimal.InvalidOperation like
    // CPython, whose message and args carry just the exception class -- the
    // same shape as the NaN ordering signal, so both share its payload.
    internal static LythonRuntimeException InvalidOperationSignal(LythonSourceSpan? span)
        => new(
            LythonRuntime.ModuleException("decimal", "InvalidOperation"),
            DecimalNanComparisonPayload.NanComparisonText,
            span,
            null,
            new DecimalNanComparisonPayload());

    // Divmod by a zero divisor signals InvalidOperation carrying both the
    // InvalidOperation and the DivisionByZero classes like CPython, while a
    // zero dividend is undefined; both reuse the constant-payload shape.
    internal static LythonRuntimeException DivmodByZero(LythonSourceSpan? span)
        => new(
            LythonRuntime.ModuleException("decimal", "InvalidOperation"),
            DecimalDivmodZeroPayload.DivmodZeroText,
            span,
            null,
            new DecimalDivmodZeroPayload());

    internal static LythonRuntimeException DivisionUndefined(LythonSourceSpan? span)
        => new(
            LythonRuntime.ModuleException("decimal", "InvalidOperation"),
            DecimalDivisionUndefinedPayload.DivisionUndefinedText,
            span,
            null,
            new DecimalDivisionUndefinedPayload());

    public static object Add(object left, object right, LythonSourceSpan span, string operation)
        => Binary(left, right, span, operation, static (lhs, rhs) => lhs + rhs, static (lhs, rhs) => Math.Min(lhs, rhs), static (lhs, rhs) => IsSigned(lhs) && IsSigned(rhs));

    public static object Subtract(object left, object right, LythonSourceSpan span, string operation)
        => Binary(left, right, span, operation, static (lhs, rhs) => lhs - rhs, static (lhs, rhs) => Math.Min(lhs, rhs), static (lhs, rhs) => IsSigned(lhs) && !IsSigned(rhs));

    public static object Multiply(object left, object right, LythonSourceSpan span, string operation)
        => Binary(left, right, span, operation, static (lhs, rhs) => lhs * rhs, static (lhs, rhs) => checked(lhs + rhs));

    public static object Divide(object left, object right, LythonSourceSpan span, string operation)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        }

        if (rhs == 0m)
        {
            if (lhs == 0m)
            {
                throw DivisionUndefined(span);
            }

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

    public static object Modulo(object left, object right, LythonSourceSpan span, string operation)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        }

        if (rhs == 0m)
        {
            if (lhs == 0m)
            {
                throw DivisionUndefined(span);
            }

            throw InvalidOperationSignal(span);
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

    public static object FloorDivide(object left, object right, LythonSourceSpan span, string operation)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        }

        if (rhs == 0m)
        {
            if (lhs == 0m)
            {
                throw DivisionUndefined(span);
            }

            if (operation == "divmod()")
            {
                throw DivmodByZero(span);
            }

            throw DivisionByZero("decimal floor division by zero", span);
        }

        // CPython truncates decimal // toward zero (unlike int and float
        // floor), so divide the exact integer scalings instead of truncating
        // the rounded BCL quotient, which can round across an integer boundary.
        var (leftNumerator, leftDenominator) = ExactDecimalParts(lhs);
        var (rightNumerator, rightDenominator) = ExactDecimalParts(rhs);
        var quotient = leftNumerator * rightDenominator / (rightNumerator * leftDenominator);
        if (quotient > (BigInteger)decimal.MaxValue || quotient < (BigInteger)decimal.MinValue)
        {
            throw DecimalOverflow(span);
        }

        // A quotient truncated to zero keeps the sign of the exact value
        // like CPython, so a negative exact quotient yields negative zero.
        var truncated = (decimal)quotient;
        return new PyDecimal(
            quotient == BigInteger.Zero && IsSigned(lhs) != IsSigned(rhs) ? decimal.Negate(truncated) : truncated);
    }

    public static object Power(object left, object right, LythonSourceSpan span, string operation)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryGetIntegerExponent(right, out var exponent))
        {
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        }

        if (exponent < int.MinValue || exponent > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal exponent is too large.", span);
        }

        var exponentInt = (int)exponent;
        if (exponentInt == 0)
        {
            if (lhs == 0m)
            {
                throw InvalidOperationSignal(span);
            }

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

    public static object PowerMod(object value, object exponentValue, object modulusValue, LythonSourceSpan span)
    {
        if (!IsIntegerOperand(value) || !IsIntegerOperand(exponentValue) || !IsIntegerOperand(modulusValue))
        {
            throw new LythonRuntimeException(
                "TypeError",
                $"unsupported operand type(s) for ** or pow(): '{RuntimeErrors.OperandTypeName(value)}', '{RuntimeErrors.OperandTypeName(exponentValue)}', '{RuntimeErrors.OperandTypeName(modulusValue)}'",
                span);
        }

        if (!TryGetIntegerOperand(value, out var integerBase) ||
            !TryGetIntegerOperand(exponentValue, out var exponent) ||
            !TryGetIntegerOperand(modulusValue, out var modulus))
        {
            throw InvalidOperationSignal(span);
        }

        if (exponent < BigInteger.Zero || modulus == BigInteger.Zero)
        {
            throw InvalidOperationSignal(span);
        }

        // Binary modular exponentiation reducing with the truncated BCL
        // remainder at every step, which is exactly the decimal %
        // convention CPython power_modulo follows (including for negative
        // moduli, where int pow would floor instead).
        var result = BigInteger.One % modulus;
        var factor = integerBase % modulus;
        while (exponent > BigInteger.Zero)
        {
            if (!exponent.IsEven)
            {
                result = result * factor % modulus;
            }

            factor = factor * factor % modulus;
            exponent >>= 1;
        }

        if (result > (BigInteger)decimal.MaxValue || result < (BigInteger)decimal.MinValue)
        {
            throw DecimalOverflow(span);
        }

        return new PyDecimal((decimal)result);
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

        ThrowIfNanComparison(left, right, span);

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

    // NaN against a decimal signals decimal.InvalidOperation like CPython
    // instead of the generic comparison TypeError; renderers without a span
    // pass null since the error is rethrown, never reported from there.
    internal static void ThrowIfNanComparison(object left, object right, LythonSourceSpan? span)
    {
        if ((left is PyDecimal || right is PyDecimal) &&
            ((left is double leftFloat && double.IsNaN(leftFloat)) ||
            (right is double rightFloat && double.IsNaN(rightFloat))))
        {
            throw new LythonRuntimeException(
                LythonRuntime.ModuleException("decimal", "InvalidOperation"),
                DecimalNanComparisonPayload.NanComparisonText,
                span,
                null,
                new DecimalNanComparisonPayload());
        }
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
        string operation,
        Func<decimal, decimal, decimal> operate,
        Func<int, int, int> combineExponent,
        Func<decimal, decimal, bool>? negativeZero = null)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        }

        try
        {
            var result = operate(lhs, rhs);
            if (negativeZero is not null && result == 0m && IsSigned(result) != negativeZero(lhs, rhs))
            {
                // A zero sum or difference takes its sign from the operands
                // like CPython instead of the BCL order-dependent bit; the
                // negation only flips the sign, keeping the exact scale.
                result = decimal.Negate(result);
            }
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

    // Integral valued decimals count as integers like CPython (fractional
    // ones keep the unsupported operands refusal); deliberately local so
    // indexing and the other TryAsInteger callers keep rejecting decimals.
    private static bool TryGetIntegerExponent(object value, out BigInteger exponent)
        => TryGetIntegerOperand(value, out exponent);

    private static bool IsIntegerOperand(object value)
        => value is PyDecimal || Numbers.PyNumberOps.TryAsInteger(value, out _);

    private static bool TryGetIntegerOperand(object value, out BigInteger integer)
    {
        if (Numbers.PyNumberOps.TryAsInteger(value, out integer))
        {
            return true;
        }

        if (value is PyDecimal pyDecimal && decimal.Truncate(pyDecimal.Value) == pyDecimal.Value)
        {
            integer = new BigInteger(pyDecimal.Value);
            return true;
        }

        integer = default;
        return false;
    }

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

// NaN-versus-decimal ordering signals decimal.InvalidOperation like
// CPython, whose args carry the exception class in a list. The rendered
// text is constant, so the payload only formats it at display time through
// the display governor instead of capturing anything at the raise site.
internal sealed class DecimalNanComparisonPayload : IPyRenderableValue
{
    internal const string NanComparisonText = "[<class 'decimal.InvalidOperation'>]";

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(NanComparisonText, context.Context.MemoryGovernor);

    public PyString RenderInterpolated(PyRenderingContext context)
        => RenderPython(context);
}

// Divmod by a zero divisor carries both signal classes in args like
// CPython; the text is constant, so the payload only formats it at display
// time through the display governor like its neighbors.
internal sealed class DecimalDivmodZeroPayload : IPyRenderableValue
{
    internal const string DivmodZeroText = "[<class 'decimal.InvalidOperation'>, <class 'decimal.DivisionByZero'>]";

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(DivmodZeroText, context.Context.MemoryGovernor);

    public PyString RenderInterpolated(PyRenderingContext context)
        => RenderPython(context);
}

// Zero divided by zero signals DivisionUndefined like CPython, whose args
// carry just that class; same constant-payload shape as its neighbors.
internal sealed class DecimalDivisionUndefinedPayload : IPyRenderableValue
{
    internal const string DivisionUndefinedText = "[<class 'decimal.DivisionUndefined'>]";

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(DivisionUndefinedText, context.Context.MemoryGovernor);

    public PyString RenderInterpolated(PyRenderingContext context)
        => RenderPython(context);
}
