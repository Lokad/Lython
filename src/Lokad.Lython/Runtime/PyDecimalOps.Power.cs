using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    public static object Power(object left, object right, LythonSourceSpan span, string operation, LythonRuntime.ExecutionContext context)
    {
        if (!TryAsDecimal(left, out var value) || !TryGetIntegerExponent(right, out var exponent))
            throw RuntimeErrors.UnsupportedOperands(operation, left, right, span);
        context.CheckExecution(span);
        var policy = context.DecimalContext;
        policy.ValidateSignalMaps(span);
        if (exponent.IsZero)
        {
            if (value == 0m) throw policy.SignalError("InvalidOperation", InvalidOperationSignal(span), span);
            return new PyDecimal(1m);
        }
        if (value == 0m)
        {
            if (exponent.Sign < 0)
                throw new LythonRuntimeException("NotImplementedError", "Zero to a negative Decimal power requires an unsupported infinity.", span);
            return new PyDecimal(IsSigned(value) && !exponent.IsEven ? decimal.Negate(0m) : 0m);
        }

        var preferred = PowerExponent(GetOperandExponent(left, value), exponent);
        var negativeZero = IsSigned(value) && !exponent.IsEven;
        if (decimal.Abs(value) == 1m && preferred == 0)
            return new PyDecimal(negativeZero ? -1m : 1m);

        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var (numerator, denominator) = ExactDecimalParts(value);
        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        numerator /= divisor;
        denominator /= divisor;
        if (BigInteger.Abs(numerator).IsOne && denominator.IsOne)
            return RoundRatio(numerator.Sign < 0 && !exponent.IsEven ? -1 : 1, BigInteger.One,
                preferred, false, policy, context, span);

        var magnitude = BigInteger.Abs(exponent);
        var count = magnitude > long.MaxValue ? long.MaxValue : (long)magnitude;
        var numeratorBits = BigInteger.Abs(numerator).IsOne ? 0 : RuntimeMemoryEstimates.GetMagnitudeBitLength(numerator);
        var denominatorBits = denominator.IsOne ? 0 : denominator.GetBitLength();
        var powerBytes = RuntimeMemoryEstimates.SaturatingAdd(
            RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(RuntimeMemoryEstimates.SaturatingMultiply(numeratorBits, count)),
            RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(RuntimeMemoryEstimates.SaturatingMultiply(denominatorBits, count)));
        // Include replacement products, division and decimal rendering work.
        // This temporary estimate borrows the same allowance as the execution.
        scratch.Grow(RuntimeMemoryEstimates.SaturatingMultiply(32, RuntimeMemoryEstimates.SaturatingAdd(powerBytes, magnitude.GetByteCount())), span);
        var poweredNumerator = IntegerPower(numerator, magnitude, context, span);
        var poweredDenominator = IntegerPower(denominator, magnitude, context, span);
        context.CheckExecution(span);
        return exponent.Sign < 0
            ? RoundRatio(poweredDenominator, poweredNumerator, preferred, negativeZero, policy, context, span)
            : RoundRatio(poweredNumerator, poweredDenominator, preferred, negativeZero, policy, context, span);
    }

    private static int PowerExponent(int source, BigInteger exponent)
    {
        if (exponent.Sign < 0 || source == 0) return 0;
        // Only preferred trailing zeros are saturated, not numeric precision.
        // Rounding needs to distinguish "more than precision", not retain an
        // arbitrarily large metadata product for a unit-valued result.
        if (exponent > int.MaxValue) return source < 0 ? int.MinValue : int.MaxValue;
        return (int)Math.Clamp((long)source * (int)exponent, int.MinValue, int.MaxValue);
    }

    private static BigInteger IntegerPower(BigInteger factor, BigInteger exponent, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (factor.IsOne) return BigInteger.One;
        var result = BigInteger.One;
        while (exponent > BigInteger.Zero)
        {
            context.CheckExecution(span);
            if (!exponent.IsEven) result *= factor;
            context.CheckExecution(span);
            exponent >>= 1;
            if (exponent > BigInteger.Zero) factor *= factor;
        }
        context.CheckExecution(span);
        return result;
    }

    public static object PowerMod(object value, object exponentValue, object modulusValue, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (!IsIntegerOperand(value) || !IsIntegerOperand(exponentValue) || !IsIntegerOperand(modulusValue))
            throw new LythonRuntimeException("TypeError", $"unsupported operand type(s) for ** or pow(): '{RuntimeErrors.OperandTypeName(value)}', '{RuntimeErrors.OperandTypeName(exponentValue)}', '{RuntimeErrors.OperandTypeName(modulusValue)}'", span);
        context.CheckExecution(span);
        var policy = context.DecimalContext;
        policy.ValidateSignalMaps(span);
        if (!TryGetIntegerOperand(value, out var integerBase) || !TryGetIntegerOperand(exponentValue, out var exponent) ||
            !TryGetIntegerOperand(modulusValue, out var modulus) || exponent.Sign < 0 || modulus.IsZero ||
            integerBase.IsZero && exponent.IsZero || BigInteger.Abs(modulus) >= BigInteger.Pow(10, policy.Precision))
            throw policy.SignalError("InvalidOperation", InvalidOperationSignal(span), span);

        var sourceBytes = RuntimeMemoryEstimates.SaturatingAdd(integerBase.GetByteCount(), exponent.GetByteCount());
        using var scratch = context.MemoryGovernor.ReserveTemporary(RuntimeMemoryEstimates.SaturatingAdd(ContextScratchBytes,
            RuntimeMemoryEstimates.SaturatingMultiply(32, sourceBytes)), span);
        var result = BigInteger.One % modulus;
        var factor = integerBase % modulus;
        while (exponent > BigInteger.Zero)
        {
            context.CheckExecution(span);
            if (!exponent.IsEven) result = result * factor % modulus;
            context.CheckExecution(span);
            exponent >>= 1;
            if (exponent > BigInteger.Zero) factor = factor * factor % modulus;
        }
        context.CheckExecution(span);
        return new PyDecimal((decimal)result);
    }
}
