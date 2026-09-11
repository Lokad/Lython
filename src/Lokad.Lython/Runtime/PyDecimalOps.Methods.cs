using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    public static PyDecimal Quantize(PyDecimal value, PyDecimal exponent, object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        if (exponent.Exponent is < -28 or > 28)
        {
            throw InvalidOperation("Decimal quantize exponent is outside Lython's 28-digit fixed-precision scale.", span);
        }

        try
        {
            if (exponent.Exponent <= 0)
            {
                var rounded = Round(value.Value, -exponent.Exponent, rounding, context, span);
                var integerDigits = rounded == 0m ? 1 : new BigInteger(decimal.Truncate(decimal.Abs(rounded))).ToString(CultureInfo.InvariantCulture).Length;
                if (integerDigits - exponent.Exponent > 29)
                {
                    throw InvalidOperation("Decimal quantize result is outside Lython's fixed-precision Decimal range.", span);
                }

                return new PyDecimal(rounded, exponent.Exponent);
            }

            var factor = Pow(10m, exponent.Exponent);
            return new PyDecimal(Round(value.Value / factor, 0, rounding, context, span) * factor, exponent.Exponent);
        }
        catch (OverflowException)
        {
            throw InvalidOperation("Decimal quantize result is outside Lython's fixed-precision Decimal range.", span);
        }
    }

    public static PyDecimal Normalize(PyDecimal value)
        => new(decimal.Parse(value.Value.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

    public static PyDecimal Unary(string name, PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}([context]) expects zero or one argument.", span);
        }

        return name switch
        {
            "sqrt" => new PyDecimal((decimal)Math.Sqrt((double)value.Value)),
            "exp" => FromDouble(Math.Exp((double)value.Value), span),
            "ln" => new PyDecimal((decimal)Math.Log((double)value.Value)),
            "log10" => new PyDecimal((decimal)Math.Log10((double)value.Value)),
            "copy_abs" => new PyDecimal(decimal.Abs(value.Value), value.Exponent),
            "copy_negate" => new PyDecimal(decimal.Negate(value.Value), value.Exponent),
            "normalize" => Normalize(value),
            _ => throw new InvalidOperationException($"Unknown decimal unary op: {name}")
        };
    }

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
            ? decimal.Abs(value.Value) / Pow(10m, value.Exponent)
            : decimal.Abs(value.Value) * Pow(10m, -value.Exponent);
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

    public static BigInteger Adjusted(PyDecimal value)
    {
        var tuple = AsTuple(value);
        if (value.Value == 0m)
        {
            return tuple.Exponent;
        }

        return new BigInteger(tuple.Digits.Count - 1) + tuple.Exponent;
    }

    public static PyDecimal CompareValue(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2 || !TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.compare(other[, context]) expects one Decimal-compatible argument.", span);
        }

        return new PyDecimal(value.Value.CompareTo(other));
    }

    public static PyDecimal CompareTotal(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length != 1 || arguments[0] is not PyDecimal other)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.compare_total(other) expects one Decimal argument.", span);
        }

        var numeric = value.Value.CompareTo(other.Value);
        if (numeric != 0)
        {
            return new PyDecimal(numeric);
        }

        var left = AsTuple(value);
        var right = AsTuple(other);
        var sign = left.Sign.CompareTo(right.Sign);
        if (sign != 0)
        {
            return new PyDecimal(sign);
        }

        var exponent = left.Exponent.CompareTo(right.Exponent);
        if (exponent != 0)
        {
            return new PyDecimal(exponent);
        }

        return new PyDecimal(CompareDigitTuples(left.Digits, right.Digits));
    }

    public static PyDecimal ToIntegral(PyDecimal value, object[] arguments, PyDecimalContext defaultContext, LythonSourceSpan span)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "Decimal.to_integral_value([rounding][, context]) expects zero to two arguments.", span);
        }

        var rounding = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
        var context = arguments.Length >= 2 ? ExpectContextOrNone(arguments[1], span) ?? defaultContext : defaultContext;
        return new PyDecimal(Round(value.Value, 0, rounding, context, span), 0);

        static PyDecimalContext? ExpectContextOrNone(object value, LythonSourceSpan span)
            => value is PyNone
                ? null
                : value as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal method context argument expects a Context or None.", span);
    }

    public static PyDecimal ScaleB(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2 || !Numbers.PyNumberOps.TryAsInteger(arguments[0], out var exponent))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.scaleb(other[, context]) expects one integer exponent.", span);
        }

        if (exponent < int.MinValue || exponent > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Decimal.scaleb exponent is too large.", span);
        }

        var shift = (int)exponent;
        var factor = Pow(10m, Math.Abs(shift));
        return new PyDecimal(shift >= 0 ? value.Value * factor : value.Value / factor, checked(value.Exponent + shift));
    }

    public static PyDecimal Shift(PyDecimal value, object[] arguments, LythonSourceSpan span)
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
        var shifted = amount >= 0
            ? digits + new string('0', amount)
            : digits.Length <= -amount ? "0" : digits[..^(-amount)];
        return FromTuple(new PyDecimalTuple(tuple.Sign, DigitsFromString(shifted), tuple.Exponent), span);
    }

    public static PyDecimal Rotate(PyDecimal value, object[] arguments, LythonSourceSpan span)
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
        return FromTuple(new PyDecimalTuple(tuple.Sign, DigitsFromString(rotated), tuple.Exponent), span);
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
        if (arguments.Length is < 1 or > 2 || !TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.remainder_near(other[, context]) expects one Decimal-compatible argument.", span);
        }

        if (other == 0m)
        {
            throw DivisionByZero("decimal remainder_near by zero", span);
        }

        var quotient = decimal.Round(value.Value / other, 0, MidpointRounding.ToEven);
        return new PyDecimal(value.Value - quotient * other);
    }

    public static PyDecimal MinMax(PyDecimal value, object[] arguments, string name, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2 || !TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}(other[, context]) expects one Decimal-compatible argument.", span);
        }

        return name switch
        {
            "min" => value.Value <= other ? value : new PyDecimal(other, GetOperandExponent(arguments[0], other)),
            "max" => value.Value >= other ? value : new PyDecimal(other, GetOperandExponent(arguments[0], other)),
            "min_mag" => decimal.Abs(value.Value) <= decimal.Abs(other) ? value : new PyDecimal(other, GetOperandExponent(arguments[0], other)),
            "max_mag" => decimal.Abs(value.Value) >= decimal.Abs(other) ? value : new PyDecimal(other, GetOperandExponent(arguments[0], other)),
            _ => throw new InvalidOperationException($"Unknown decimal min/max op: {name}")
        };
    }
}
