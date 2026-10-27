using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    public static PyDecimal Quantize(PyDecimal value, long exponent, object? rounding, PyDecimalContext policy,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        var mode = ResolveRounding(rounding, policy, span);
        if (exponent is < -28 or > 28)
        {
            throw InvalidOperation("Decimal quantize exponent is outside Lython's 28-digit fixed-precision scale.", span);
        }

        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var coefficient = CoefficientAtExponent(value, (int)exponent, mode, out var inexact);
        if (coefficient >= BigInteger.Pow(10, policy.Precision))
            throw policy.QuantizeError(context, span);
        var result = FromCoefficient(coefficient, (int)exponent, value.IsSigned, span);
        context.CheckExecution(span);
        policy.RecordRounding(inexact, inexact || value.Value != 0m && exponent > value.Exponent, context, span);
        return result;
    }

    public static PyDecimal Normalize(PyDecimal value, object[] arguments,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length > 1)
            throw new LythonRuntimeException("TypeError", "Decimal.normalize([context]) expects zero or one argument.", span);
        var policy = arguments.Length == 1 ? ExpectContextOrNone(arguments[0], span) ?? context.DecimalContext : context.DecimalContext;
        var rounded = ApplyContext(value, policy, context, span);
        if (rounded.Value == 0m) return new PyDecimal(rounded.Value, 0);
        // Only bounded coefficient metadata remains after admitted arithmetic.
        var (coefficient, _) = ExactDecimalParts(rounded.Value);
        var exponent = -GetScale(rounded.Value);
        while (coefficient % 10 == 0)
        {
            context.CheckExecution(span);
            coefficient /= 10;
            exponent++;
        }
        return new PyDecimal(rounded.Value, exponent);
    }

    public static PyDecimal Unary(string name, PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}([context]) expects zero or one argument.", span);
        }

        if (arguments.Length == 1)
        {
            ExpectContextOrNone(arguments[0], span);
        }

        return name switch
        {
            "sqrt" => new PyDecimal((decimal)Math.Sqrt((double)value.Value)),
            "exp" => FromDouble(Math.Exp((double)value.Value), span),
            "ln" => new PyDecimal((decimal)Math.Log((double)value.Value)),
            "log10" => new PyDecimal((decimal)Math.Log10((double)value.Value)),
            _ => throw new InvalidOperationException($"Unknown decimal unary op: {name}")
        };
    }

    // copy_abs and copy_negate take no context in CPython (unlike the other
    // Unary ops), so they validate strictly through this dedicated path.
    public static PyDecimal CopyAbsNegate(PyDecimal value, object[] arguments, bool negate, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", negate ? "Decimal.copy_negate() expects no arguments." : "Decimal.copy_abs() expects no arguments.", span);
        }

        var magnitude = decimal.Abs(value.Value);
        return new PyDecimal(negate ? decimal.Negate(magnitude) : magnitude, value.Exponent);
    }

    internal static PyDecimalContext? ExpectContextOrNone(object value, LythonSourceSpan span)
        => value is PyNone
            ? null
            : value as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal method context argument expects a Context or None.", span);

    public static PyDecimal CopySign(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length != 1 || !TryAsDecimal(arguments[0], out var sign))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.copy_sign(other) expects one Decimal-compatible argument.", span);
        }

        var magnitude = decimal.Abs(value.Value);
        return new PyDecimal(IsSigned(sign) ? decimal.Negate(magnitude) : magnitude, value.Exponent);
    }

    private static PyDecimal FromDouble(double result, LythonSourceSpan span)
    {
        try
        {
            return new PyDecimal((decimal)result);
        }
        catch (OverflowException)
        {
            throw DecimalOverflow(span);
        }
    }

    public static PyDecimalTuple AsTuple(PyDecimal value)
        => AsTuple(value, null, null);

    public static PyDecimalTuple AsTuple(PyDecimal value, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (value.Value == 0m)
        {
            // Zero needs no scaling powers, so any exponent is representable.
            var zeroDigits = new PyTuple(new object[] { BigInteger.Zero });
            if (governor is not null)
            {
                zeroDigits = new PyTuple(new object[] { BigInteger.Zero }, governor, span);
            }

            return new PyDecimalTuple(IsSigned(value.Value) ? 1 : 0, zeroDigits, new BigInteger(value.Exponent));
        }

        var coefficient = value.Exponent >= 0
            ? decimal.Abs(value.Value) / PowerOfTen(value.Exponent)
            : decimal.Abs(value.Value) * PowerOfTen(-value.Exponent);
        var digitsText = decimal.Truncate(coefficient).ToString("0", CultureInfo.InvariantCulture).TrimStart('0');
        if (digitsText.Length == 0)
        {
            digitsText = "0";
        }

        var digits = new object[digitsText.Length];
        for (var i = 0; i < digitsText.Length; i++)
        {
            digits[i] = new BigInteger(digitsText[i] - '0');
        }

        var digitsTuple = governor is null
            ? new PyTuple(digits)
            : new PyTuple(digits, governor, span);
        return new PyDecimalTuple(IsSigned(value.Value) ? 1 : 0, digitsTuple, new BigInteger(value.Exponent));
    }

    // Matches the CPython statistics Decimal(Fraction) constructor form used
    // for exact mean/variance quotients: trailing zeros are not significant.
    // String-based and exact for finite decimals; bounded by the fixed scale.
    internal static decimal StripTrailingZeros(decimal value)
    {
        if (value == 0m)
        {
            return 0m;
        }

        var text = value.ToString(CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        if (dot < 0)
        {
            return value;
        }

        var end = text.Length - 1;
        while (end > dot && text[end] == '0')
        {
            end--;
        }

        var normalized = end == dot ? text.Substring(0, dot) : text.Substring(0, end + 1);
        return decimal.Parse(normalized, CultureInfo.InvariantCulture);
    }

    public static BigInteger Adjusted(PyDecimal value)
    {
        var tuple = AsTuple(value);
        if (value.Value == 0m)
        {
            return tuple.Exponent;
        }

        return new BigInteger(tuple.Digits.Count - 1) + tuple.Exponent;
    }

    public static PyDecimal CompareValue(PyDecimal value, object[] arguments, string name,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}(other[, context]) expects one Decimal-compatible argument.", span);
        }

        var policy = arguments.Length == 2 ? ExpectContextOrNone(arguments[1], span) ?? context.DecimalContext : context.DecimalContext;
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);

        if (!TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}(other[, context]) expects one Decimal-compatible argument.", span);
        }

        return new PyDecimal(value.Value.CompareTo(other));
    }

    public static PyDecimal CompareTotal(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length != 1 || arguments[0] is not PyDecimal other)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.compare_total(other) expects one Decimal argument.", span);
        }

        return new PyDecimal(FiniteTotalOrder(value, other));
    }

    private static int FiniteTotalOrder(PyDecimal value, PyDecimal other)
    {
        var numeric = value.Value.CompareTo(other.Value);
        if (numeric != 0)
        {
            return numeric;
        }

        if (value.IsSigned != other.IsSigned) return value.IsSigned ? -1 : 1;
        var exponent = value.Exponent.CompareTo(other.Exponent);
        return value.IsSigned ? -exponent : exponent;
    }

    public static PyDecimal ToIntegral(PyDecimal value, object[] arguments, bool exact,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.to_integral_value([rounding][, context]) expects zero to two arguments.", span);
        }

        var rounding = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
        var policy = arguments.Length >= 2 ? ExpectContextOrNone(arguments[1], span) ?? context.DecimalContext : context.DecimalContext;
        context.CheckExecution(span);
        policy.ValidateSignalMaps(span);
        var mode = ResolveRounding(rounding, policy, span);
        // Integral values with a positive quantum keep it; precision is ignored.
        if (value.Exponent >= 0) return new PyDecimal(value.Value, value.Exponent);
        using var scratch = context.MemoryGovernor.ReserveTemporary(ContextScratchBytes, span);
        var coefficient = CoefficientAtExponent(value, 0, mode, out var inexact);
        var result = FromCoefficient(coefficient, 0, value.IsSigned, span);
        context.CheckExecution(span);
        if (exact) policy.RecordRounding(inexact, value.Value != 0m, context, span);
        return result;
    }

    private static BigInteger CoefficientAtExponent(PyDecimal value, int exponent, DecimalRoundingMode mode, out bool inexact)
    {
        var (numerator, denominator) = ExactDecimalParts(value.Value);
        numerator = BigInteger.Abs(numerator);
        if (exponent < 0) numerator *= BigInteger.Pow(10, -exponent);
        else if (exponent > 0) denominator *= BigInteger.Pow(10, exponent);
        return RoundCoefficient(numerator, denominator, value.IsSigned, mode, out inexact);
    }

    private static PyDecimal FromCoefficient(BigInteger coefficient, int exponent, bool negative, LythonSourceSpan span)
    {
        if (exponent > 0) coefficient *= BigInteger.Pow(10, exponent);
        if (coefficient > (BigInteger)decimal.MaxValue)
            throw InvalidOperation("Decimal quantize result is outside Lython's fixed-precision Decimal range.", span);
        var result = new decimal((int)(uint)(coefficient & uint.MaxValue),
            (int)(uint)((coefficient >> 32) & uint.MaxValue), (int)(uint)(coefficient >> 64), negative, (byte)Math.Max(0, -exponent));
        return new PyDecimal(result, exponent);
    }

    public static PyDecimal ScaleB(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.scaleb(other[, context]) expects one integer exponent.", span);
        }

        if (arguments.Length == 2)
        {
            ExpectContextOrNone(arguments[1], span);
        }

        if (!Numbers.PyNumberOps.TryAsInteger(arguments[0], out var exponent))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.scaleb(other[, context]) expects one integer exponent.", span);
        }

        if (exponent < int.MinValue || exponent > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal.scaleb exponent is too large.", span);
        }

        var shift = (int)exponent;
        var factor = PowerOfTen(Math.Abs(shift));
        return new PyDecimal(shift >= 0 ? value.Value * factor : value.Value / factor, checked(value.Exponent + shift));
    }

    public static PyDecimal Shift(PyDecimal value, object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1 || !Numbers.PyNumberOps.TryAsInteger(arguments[0], out var shift))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.shift(other) expects one integer argument.", span);
        }

        if (shift < int.MinValue || shift > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal.shift argument is too large.", span);
        }

        var tuple = AsTuple(value);
        var digits = DigitsToString(tuple.Digits);
        var amount = (int)shift;
        using var storage = context.MemoryGovernor.ReserveTemporary(128L + 64L * (digits.Length + Math.Max(0L, amount)), span);
        var shifted = amount >= 0
            ? digits + new string('0', amount)
            : digits.Length <= -(long)amount ? "0" : digits[..^(-amount)];
        return FromTuple(new PyDecimalTuple(tuple.Sign, DigitsFromString(shifted), tuple.Exponent), span, context);
    }

    public static PyDecimal Rotate(PyDecimal value, object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1 || !Numbers.PyNumberOps.TryAsInteger(arguments[0], out var shift))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.rotate(other) expects one integer argument.", span);
        }

        if (shift < int.MinValue || shift > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal.rotate argument is too large.", span);
        }

        var tuple = AsTuple(value);
        var digits = DigitsToString(tuple.Digits);
        if (digits.Length <= 1)
        {
            return value;
        }

        var amount = (int)(shift % digits.Length);
        if (amount < 0)
        {
            amount += digits.Length;
        }

        var rotated = digits[^amount..] + digits[..^amount];
        return FromTuple(new PyDecimalTuple(tuple.Sign, DigitsFromString(rotated), tuple.Exponent), span, context);
    }

    public static bool SameQuantum(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length != 1 || arguments[0] is not PyDecimal other)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.same_quantum(other) expects one Decimal argument.", span);
        }

        return value.Exponent == other.Exponent;
    }

    public static PyDecimal RemainderNear(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.remainder_near(other[, context]) expects one Decimal-compatible argument.", span);
        }

        if (arguments.Length == 2)
        {
            ExpectContextOrNone(arguments[1], span);
        }

        if (!TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.remainder_near(other[, context]) expects one Decimal-compatible argument.", span);
        }

        if (other == 0m)
        {
            if (value.Value == 0m)
            {
                throw DivisionUndefined(span);
            }

            throw InvalidOperationSignal(span);
        }

        var quotient = decimal.Round(value.Value / other, 0, MidpointRounding.ToEven);
        return new PyDecimal(value.Value - quotient * other);
    }

    public static PyDecimal FusedMultiplyAdd(PyDecimal value, object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.fma(other, third[, context]) expects two Decimal-compatible arguments.", span);
        }

        if (!TryAsDecimal(arguments[0], out _) || !TryAsDecimal(arguments[1], out _))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.fma(other, third[, context]) expects two Decimal-compatible arguments.", span);
        }

        var policy = arguments.Length == 3 ? ExpectContextOrNone(arguments[2], span) ?? context.DecimalContext : context.DecimalContext;
        return ContextFusedMultiplyAdd(value, arguments[0], arguments[1], policy, context, span);
    }

    public static PyDecimal LogB(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.logb([context]) expects zero or one argument.", span);
        }

        if (arguments.Length == 1)
        {
            ExpectContextOrNone(arguments[0], span);
        }

        if (value.Value == 0m)
        {
            throw DivisionByZero("decimal division by zero", span);
        }

        return new PyDecimal((decimal)Adjusted(value));
    }

    public static PyTuple AsIntegerRatio(PyDecimal value, object[] arguments, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.as_integer_ratio() expects no arguments.", span);
        }

        var (numerator, denominator) = ExactDecimalParts(value.Value);
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return new PyTuple([numerator / divisor, denominator / divisor], governor, span);
    }

    public static PyDecimal MinMax(PyDecimal value, object[] arguments, string name,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}(other[, context]) expects one Decimal-compatible argument.", span);
        }

        var policy = arguments.Length == 2 ? ExpectContextOrNone(arguments[1], span) ?? context.DecimalContext : context.DecimalContext;

        if (!TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}(other[, context]) expects one Decimal-compatible argument.", span);
        }

        var candidate = arguments[0] as PyDecimal ?? new PyDecimal(other);
        var comparison = name switch
        {
            "min" or "max" => value.Value.CompareTo(other),
            "min_mag" or "max_mag" => decimal.Abs(value.Value).CompareTo(decimal.Abs(other)),
            _ => throw new InvalidOperationException($"Unknown decimal min/max op: {name}")
        };
        if (comparison == 0) comparison = FiniteTotalOrder(value, candidate);
        var selected = (name is "min" or "min_mag" ? comparison <= 0 : comparison >= 0) ? value : candidate;
        return ApplyContext(selected, policy, context, span);
    }
}
