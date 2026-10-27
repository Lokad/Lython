using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    public static PyDecimal Unary(string name, PyDecimal value, object[] arguments,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length > 1)
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}([context]) expects zero or one argument.", span);
        var policy = arguments.Length == 1 ? ExpectContextOrNone(arguments[0], span) ?? context.DecimalContext : context.DecimalContext;
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        if (name == "sqrt") return SquareRoot(value, policy, context, span);
        if (name == "exp")
        {
            if (value.Value == 0m) return new PyDecimal(1m);
        }
        else if (name is "ln" or "log10")
        {
            // Python's logarithm of zero needs infinity without a signal.
            if (value.Value == 0m)
                throw new LythonRuntimeException("NotImplementedError", "Nonfinite Decimal logarithms are unsupported.", span);
            if (value.Value < 0m)
                throw policy.SignalError("InvalidOperation", InvalidOperationSignal(span), span);
            if (name == "ln" && value.Value == 1m) return new PyDecimal(0m);
            if (name == "log10")
            {
                using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
                var (coefficient, _) = ExactDecimalParts(value.Value);
                var exponent = -GetScale(value.Value);
                while (coefficient % 10 == 0)
                {
                    context.CheckExecution(span);
                    coefficient /= 10;
                    exponent++;
                }
                if (coefficient.IsOne)
                    return RoundRatio(new BigInteger(exponent), BigInteger.One, 0, false,
                        policy, context, span, DecimalRoundingMode.HalfEven);
            }
        }
        else throw new InvalidOperationException($"Unknown Decimal function: {name}");
        throw new LythonRuntimeException("NotImplementedError",
            $"Decimal.{name} supports exact constants only; general transcendental Decimal results are unsupported.", span);
    }

    private static PyDecimal SquareRoot(PyDecimal value, PyDecimalContext policy,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var idealExponent = value.Exponent >> 1;
        if (value.Value == 0m)
        {
            if (idealExponent < policy.Emin - policy.Precision + 1 || idealExponent > policy.Emax)
                throw new LythonRuntimeException("NotImplementedError", "Decimal zero-exponent clamping is unsupported.", span);
            return FromCoefficient(BigInteger.Zero, idealExponent, value.IsSigned, span);
        }
        if (value.Value < 0m)
            throw policy.SignalError("InvalidOperation", InvalidOperationSignal(span), span);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var coefficient = CoefficientAtExponent(value, value.Exponent, DecimalRoundingMode.Down, out _);
        if ((value.Exponent & 1) != 0) coefficient *= 10;
        var pairs = (coefficient.ToString(CultureInfo.InvariantCulture).Length + 1) / 2;
        // One guard digit. Native coefficients and precision <= 28 bound this
        // scaled radicand to 58 digits; the existing isqrt admits its overlap.
        var shift = policy.Precision + 1 - pairs;
        var exact = true;
        if (shift >= 0) coefficient *= BigInteger.Pow(100, shift);
        else
        {
            coefficient = BigInteger.DivRem(coefficient, BigInteger.Pow(100, -shift), out var remainder);
            exact = remainder.IsZero;
        }
        var root = LythonRuntime.IntegerSquareRoot(coefficient, context, span);
        exact &= root * root == coefficient;
        var exponent = idealExponent - shift;
        if (exact)
        {
            root = shift >= 0 ? root / BigInteger.Pow(10, shift) : root * BigInteger.Pow(10, -shift);
            exponent = idealExponent;
        }
        else if (root % 5 == 0)
            root++; // Keep discarded information across the final half-even round.
        context.CheckExecution(span);
        var numerator = exponent >= 0 ? root * BigInteger.Pow(10, exponent) : root;
        var denominator = exponent < 0 ? BigInteger.Pow(10, -exponent) : BigInteger.One;
        return RoundRatio(numerator, denominator, exponent, false,
            policy, context, span, DecimalRoundingMode.HalfEven);
    }
}
