using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    // Finite CLR inputs bound these ratios, scales and short-lived BigInteger
    // backing. Admit their simultaneous construction before decoding operands.
    private const long ContextScratchBytes = 4096;

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
        if (arithmetic == '/' && rhs == 0m)
            throw lhs == 0m ? DivisionUndefined(span) : DivisionByZero("decimal division by zero", span);

        context.CheckExecution(span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var policy = context.DecimalContext;
        policy.ValidateSignalMaps(span);
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

    private static PyDecimal RoundRatio(BigInteger numerator, BigInteger denominator, int preferredExponent,
        bool negativeZero, PyDecimalContext policy, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (numerator.IsZero)
            return new PyDecimal(negativeZero ? decimal.Negate(0m) : 0m, preferredExponent);

        var negative = numerator.Sign != denominator.Sign;
        numerator = BigInteger.Abs(numerator);
        denominator = BigInteger.Abs(denominator);
        // Decimal inputs bound these strings to a few dozen characters. The
        // exact comparison corrects the candidate decimal order, without logs.
        var order = numerator.ToString(CultureInfo.InvariantCulture).Length - denominator.ToString(CultureInfo.InvariantCulture).Length;
        if (order >= 0 ? numerator < denominator * BigInteger.Pow(10, order)
                       : numerator * BigInteger.Pow(10, -order) < denominator)
            order--;
        var exponent = order - policy.Precision + 1;
        var scaledNumerator = exponent < 0 ? numerator * BigInteger.Pow(10, -exponent) : numerator;
        var scaledDenominator = exponent > 0 ? denominator * BigInteger.Pow(10, exponent) : denominator;
        var coefficient = BigInteger.DivRem(scaledNumerator, scaledDenominator, out var remainder);
        var inexact = !remainder.IsZero;
        var rounded = inexact || exponent > preferredExponent;
        if (inexact)
        {
            var half = (remainder * 2).CompareTo(scaledDenominator);
            var increment = policy.Rounding switch
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
            if (increment) coefficient++;
        }
        if (coefficient >= BigInteger.Pow(10, policy.Precision))
        {
            coefficient /= 10;
            exponent++;
        }
        // Exact quotients retain only the zeros requested by the operands.
        while (exponent < preferredExponent && coefficient % 10 == 0)
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
}
