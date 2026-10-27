using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    // Finite CLR inputs bound these ratios, scales and short-lived BigInteger
    // backing. Admit their simultaneous construction before decoding operands.
    private const long ContextScratchBytes = 4096;

    public static PyDecimal ApplyFloatContext(double value, PyDecimalContext policy, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool implicitConversion = false)
    {
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        if (!double.IsFinite(value))
            throw InvalidOperation("Nonfinite Decimal values are unsupported.", span);
        // Binary64 can require a 1074-bit denominator. Its exact bounded
        // decomposition needs more working headroom than the CLR decimal path.
        using var scratch = context.MemoryGovernor.ReserveTemporary(8192, span);
        if (implicitConversion) policy.RecordFloatConversion(context, span);
        var (numerator, denominator) = ExactDoubleParts(value);
        if (numerator.IsZero)
            return RoundRatio(numerator, BigInteger.One, 0, double.IsNegative(value), policy, context, span);
        while (denominator > BigInteger.One && numerator.IsEven)
        {
            context.CheckExecution(span);
            numerator >>= 1;
            denominator >>= 1;
        }
        var exponent = checked(1 - (int)denominator.GetBitLength());
        return RoundRatio(numerator, denominator, exponent, double.IsNegative(value), policy, context, span);
    }

    public static PyDecimal ApplyContext(PyDecimal value, PyDecimalContext policy, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var (numerator, denominator) = ExactDecimalParts(value.Value);
        return RoundRatio(numerator, denominator, value.Exponent, value.IsSigned, policy, context, span);
    }

    private static PyDecimal ContextFusedMultiplyAdd(PyDecimal value, object right, object third, PyDecimalContext policy,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!TryAsDecimal(right, out var rhs) || !TryAsDecimal(third, out var tail))
            throw new LythonRuntimeException("TypeError", "Decimal.fma expects Decimal-compatible operands.", span);
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var (a, ad) = ExactDecimalParts(value.Value);
        var (b, bd) = ExactDecimalParts(rhs);
        var (c, cd) = ExactDecimalParts(tail);
        var productNegative = value.IsSigned != IsSigned(rhs);
        var negativeZero = productNegative == IsSigned(tail) ? productNegative : policy.Rounding == DecimalRoundingMode.Floor;
        try
        {
            return RoundRatio(a * b * cd + c * ad * bd, ad * bd * cd,
                Math.Min(checked(value.Exponent + GetOperandExponent(right, rhs)), GetOperandExponent(third, tail)),
                negativeZero, policy, context, span);
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static PyDecimal ContextBinary(object left, object right, LythonSourceSpan span, string operation,
        char arithmetic, LythonRuntime.ExecutionContext context)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        context.CheckExecution(span);
        var policy = context.DecimalContext;
        policy.ValidateSignalMaps(span);
        if (arithmetic == '/' && rhs == 0m)
            throw lhs == 0m ? policy.SignalError("InvalidOperation", DivisionUndefined(span), span)
                : policy.SignalError("DivisionByZero", DivisionByZero("decimal division by zero", span), span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var (a, ad) = ExactDecimalParts(lhs);
        var (b, bd) = ExactDecimalParts(rhs);
        var leftExponent = GetOperandExponent(left, lhs);
        var rightExponent = GetOperandExponent(right, rhs);
        BigInteger numerator, denominator;
        int exponent;
        bool negativeZero;
        try
        {
            switch (arithmetic)
            {
                case '+':
                case '-':
                    numerator = a * bd + (arithmetic == '+' ? b : -b) * ad;
                    denominator = ad * bd;
                    exponent = Math.Min(leftExponent, rightExponent);
                    var rightNegative = IsSigned(rhs) ^ (arithmetic == '-');
                    negativeZero = IsSigned(lhs) == rightNegative ? IsSigned(lhs) : policy.Rounding == DecimalRoundingMode.Floor;
                    break;
                case '*':
                    numerator = a * b;
                    denominator = ad * bd;
                    exponent = checked(leftExponent + rightExponent);
                    negativeZero = IsSigned(lhs) != IsSigned(rhs);
                    break;
                case '/':
                    numerator = a * bd;
                    denominator = b * ad;
                    exponent = checked(leftExponent - rightExponent);
                    negativeZero = IsSigned(lhs) != IsSigned(rhs);
                    break;
                default:
                    throw new InvalidOperationException("Unknown finite decimal operation.");
            }
            return RoundRatio(numerator, denominator, exponent, negativeZero, policy, context, span);
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    private static PyDecimal ContextDivRem(object left, object right, LythonSourceSpan span, string operation,
        bool remainder, LythonRuntime.ExecutionContext context)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        context.CheckExecution(span);
        var policy = context.DecimalContext;
        policy.ValidateSignalMaps(span);
        if (rhs == 0m)
        {
            if (lhs == 0m) throw policy.SignalError("InvalidOperation", DivisionUndefined(span), span);
            if (remainder) throw policy.SignalError("InvalidOperation", InvalidOperationSignal(span), span);
            if (operation == "divmod()") throw policy.DivmodZeroError(context, span);
            throw policy.SignalError("DivisionByZero", DivisionByZero("decimal floor division by zero", span), span);
        }

        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var (a, ad) = ExactDecimalParts(lhs);
        var (b, bd) = ExactDecimalParts(rhs);
        var quotient = a * bd / (b * ad);
        if (BigInteger.Abs(quotient) >= BigInteger.Pow(10, policy.Precision))
            throw policy.DivisionImpossibleError(context, span);
        if (!remainder)
        {
            var result = (decimal)quotient;
            return new PyDecimal(quotient.IsZero && IsSigned(lhs) != IsSigned(rhs) ? decimal.Negate(result) : result);
        }
        return RoundRatio(a * bd - quotient * b * ad, ad * bd,
            Math.Min(GetOperandExponent(left, lhs), GetOperandExponent(right, rhs)), IsSigned(lhs), policy, context, span);
    }

    private static PyDecimal RoundRatio(BigInteger numerator, BigInteger denominator, int preferredExponent,
        bool negativeZero, PyDecimalContext policy, LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        DecimalRoundingMode? rounding = null)
    {
        if (numerator.IsZero)
            return new PyDecimal(negativeZero ? decimal.Negate(0m) : 0m, preferredExponent);

        var negative = numerator.Sign != denominator.Sign;
        numerator = BigInteger.Abs(numerator);
        denominator = BigInteger.Abs(denominator);
        // Power callers admit the larger magnitude/rendering overlap as well.
        // Exact decimal digit lengths avoid approximate logarithmic decisions.
        var numeratorDigits = numerator.ToString(CultureInfo.InvariantCulture).Length;
        context.CheckExecution(span);
        var denominatorDigits = denominator.ToString(CultureInfo.InvariantCulture).Length;
        context.CheckExecution(span);
        var order = numeratorDigits - denominatorDigits;
        if (order > 29) throw DecimalOverflow(span);
        if (order < -29) throw InvalidOperation("Decimal result is outside Lython's finite scale.", span);
        if (order >= 0 ? numerator < denominator * BigInteger.Pow(10, order)
                       : numerator * BigInteger.Pow(10, -order) < denominator)
            order--;
        if (order > 28) throw DecimalOverflow(span);
        if (order < -29) throw InvalidOperation("Decimal result is outside Lython's finite scale.", span);
        var exponent = order - policy.Precision + 1;
        var scaledNumerator = exponent < 0 ? numerator * BigInteger.Pow(10, -exponent) : numerator;
        var scaledDenominator = exponent > 0 ? denominator * BigInteger.Pow(10, exponent) : denominator;
        var coefficient = RoundCoefficient(scaledNumerator, scaledDenominator, negative, rounding ?? policy.Rounding, out var inexact);
        var rounded = inexact || exponent > preferredExponent;
        if (coefficient >= BigInteger.Pow(10, policy.Precision))
        {
            coefficient /= 10;
            exponent++;
        }
        // Exact quotients retain only the zeros requested by the operands.
        while (!inexact && exponent < preferredExponent && coefficient % 10 == 0)
        {
            coefficient /= 10;
            exponent++;
        }
        if (exponent < -28)
            throw InvalidOperation("Decimal result is outside Lython's finite scale.", span);
        if (exponent > 0) coefficient *= BigInteger.Pow(10, exponent);
        if (coefficient > (BigInteger)decimal.MaxValue) throw DecimalOverflow(span);
        var result = new decimal((int)(uint)(coefficient & uint.MaxValue),
            (int)(uint)((coefficient >> 32) & uint.MaxValue), (int)(uint)(coefficient >> 64), negative, (byte)Math.Max(0, -exponent));
        context.CheckExecution(span);
        policy.RecordRounding(inexact, rounded, context, span);
        return new PyDecimal(result, exponent);
    }

    // Nonnegative exact ratio; callers admit its bounded temporary storage.
    private static BigInteger RoundCoefficient(BigInteger numerator, BigInteger denominator,
        bool negative, DecimalRoundingMode mode, out bool inexact)
    {
        var coefficient = BigInteger.DivRem(numerator, denominator, out var remainder);
        inexact = !remainder.IsZero;
        if (!inexact) return coefficient;
        var half = (remainder * 2).CompareTo(denominator);
        var increment = mode switch
        {
            DecimalRoundingMode.Down => false,
            DecimalRoundingMode.Up => true,
            DecimalRoundingMode.Ceiling => !negative,
            DecimalRoundingMode.Floor => negative,
            DecimalRoundingMode.HalfEven => half > 0 || half == 0 && !coefficient.IsEven,
            DecimalRoundingMode.HalfUp => half >= 0,
            DecimalRoundingMode.HalfDown => half > 0,
            DecimalRoundingMode.ZeroFiveUp => coefficient % 10 is var digit && (digit.IsZero || digit == 5),
            _ => throw new InvalidOperationException("Unknown decimal rounding mode."),
        };
        return increment ? coefficient + 1 : coefficient;
    }
}
