using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDecimal : IPyTruthyValue, IPyRenderableValue, IPyHashableValue
{
    public PyDecimal(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public bool IsTruthy() => Value != 0m;

    public bool IsSigned => PyDecimalOps.IsSigned(Value);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"Decimal('{PyDecimalOps.Format(Value)}')");
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(PyDecimalOps.Format(Value));
    }

    public int GetPyHashCode()
    {
        if (decimal.Truncate(Value) == Value)
        {
            return new BigInteger(Value).GetHashCode();
        }

        return Value.GetHashCode();
    }
}

internal static class PyDecimalOps
{
    public static bool TryAsDecimal(object value, out decimal decimalValue)
    {
        switch (value)
        {
            case PyDecimal pyDecimal:
                decimalValue = pyDecimal.Value;
                return true;
            case bool boolean:
                decimalValue = boolean ? 1m : 0m;
                return true;
            case BigInteger integer when integer >= (BigInteger)decimal.MinValue && integer <= (BigInteger)decimal.MaxValue:
                decimalValue = (decimal)integer;
                return true;
            default:
                decimalValue = default;
                return false;
        }
    }

    public static PyDecimal Parse(object value, LythonSourceSpan span)
    {
        switch (value)
        {
            case PyDecimal pyDecimal:
                return pyDecimal;
            case PyDecimalTuple decimalTuple:
                return FromTuple(decimalTuple, span);
            case bool boolean:
                return new PyDecimal(boolean ? 1m : 0m);
            case BigInteger integer when integer >= (BigInteger)decimal.MinValue && integer <= (BigInteger)decimal.MaxValue:
                return new PyDecimal((decimal)integer);
            case double floating when double.IsFinite(floating):
                return new PyDecimal((decimal)floating);
            default:
                if (PyStringOps.TryAsString(value, out var text))
                {
                    var raw = text.AsString().Trim();
                    if (IsUnsupportedSpecialValue(raw))
                    {
                        throw new LythonRuntimeException(
                            "InvalidOperation",
                            "NaN, sNaN, and Infinity are not supported by Lython's fixed-precision Decimal.",
                            span);
                    }

                    if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return new PyDecimal(parsed);
                    }
                }

                throw new LythonRuntimeException("TypeError", "Decimal(...) expects a decimal-compatible string, tuple, or number.", span);
        }
    }

    public static PyDecimalTuple CreateTuple(object signValue, object digitsValue, object exponentValue, LythonSourceSpan span)
    {
        var sign = ExpectInt(signValue, "DecimalTuple(sign, digits, exponent) expects sign 0 or 1.", span);
        if (sign is not 0 and not 1)
        {
            throw new LythonRuntimeException("ValueError", "DecimalTuple sign must be 0 or 1.", span);
        }

        if (!Numbers.PyNumberOps.TryAsInteger(exponentValue, out var exponent))
        {
            throw new LythonRuntimeException("TypeError", "DecimalTuple exponent must be an integer.", span);
        }

        var digits = MaterializeDigits(digitsValue, span);
        return new PyDecimalTuple(sign, digits, exponent);
    }

    public static object Add(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs + rhs);

    public static object Subtract(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs - rhs);

    public static object Multiply(object left, object right, LythonSourceSpan span)
        => Binary(left, right, span, static (lhs, rhs) => lhs * rhs);

    public static object Divide(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw new LythonRuntimeException("DivisionByZero", "decimal division by zero", span);
        }

        return new PyDecimal(lhs / rhs);
    }

    public static object Modulo(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        if (rhs == 0m)
        {
            throw new LythonRuntimeException("DivisionByZero", "decimal modulo by zero", span);
        }

        return new PyDecimal(lhs % rhs);
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
                throw new LythonRuntimeException("DivisionByZero", "decimal division by zero", span);
            }

            return new PyDecimal(1m / Pow(lhs, -exponentInt));
        }

        return new PyDecimal(Pow(lhs, exponentInt));
    }

    public static int Compare(object left, object right, LythonSourceSpan span)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Values are not comparable.", span);
        }

        return lhs.CompareTo(rhs);
    }

    public static bool AreEqual(object left, object right)
        => TryAsDecimal(left, out var lhs) && TryAsDecimal(right, out var rhs) && lhs == rhs;

    public static PyDecimal Quantize(PyDecimal value, PyDecimal exponent, object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        var scale = GetScale(exponent.Value);
        return new PyDecimal(Round(value.Value, scale, rounding, context, span));
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
            "exp" => new PyDecimal((decimal)Math.Exp((double)value.Value)),
            "ln" => new PyDecimal((decimal)Math.Log((double)value.Value)),
            "log10" => new PyDecimal((decimal)Math.Log10((double)value.Value)),
            "copy_abs" => new PyDecimal(decimal.Abs(value.Value)),
            "copy_negate" => new PyDecimal(decimal.Negate(value.Value)),
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
        return new PyDecimal(IsSigned(sign) ? decimal.Negate(magnitude) : magnitude);
    }

    public static PyDecimalTuple AsTuple(PyDecimal value)
    {
        var scale = GetScale(value.Value);
        var formatted = decimal.Abs(value.Value).ToString($"F{scale}", CultureInfo.InvariantCulture);
        var digitsText = formatted.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digitsText.Length == 0)
        {
            digitsText = "0";
        }

        var digits = new object[digitsText.Length];
        for (var i = 0; i < digitsText.Length; i++)
        {
            digits[i] = new BigInteger(digitsText[i] - '0');
        }

        return new PyDecimalTuple(IsSigned(value.Value) ? 1 : 0, new PyTuple(digits), new BigInteger(-scale));
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
        return new PyDecimal(Round(value.Value, 0, rounding, context, span));
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
        return new PyDecimal(shift >= 0 ? value.Value * factor : value.Value / factor);
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

        return GetScale(value.Value) == GetScale(other.Value);
    }

    public static PyDecimal RemainderNear(PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2 || !TryAsDecimal(arguments[0], out var other))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.remainder_near(other[, context]) expects one Decimal-compatible argument.", span);
        }

        if (other == 0m)
        {
            throw new LythonRuntimeException("DivisionByZero", "decimal remainder_near by zero", span);
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
            "min" => new PyDecimal(value.Value <= other ? value.Value : other),
            "max" => new PyDecimal(value.Value >= other ? value.Value : other),
            "min_mag" => new PyDecimal(decimal.Abs(value.Value) <= decimal.Abs(other) ? value.Value : other),
            "max_mag" => new PyDecimal(decimal.Abs(value.Value) >= decimal.Abs(other) ? value.Value : other),
            _ => throw new InvalidOperationException($"Unknown decimal min/max op: {name}")
        };
    }

    public static PyString ToEngineeringString(PyDecimal value)
        => PyString.FromString(Format(value.Value));

    public static string Format(decimal value)
    {
        var text = decimal.Abs(value).ToString(CultureInfo.InvariantCulture);
        return IsSigned(value) && value == 0m ? "-" + text : value.ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsSigned(decimal value)
        => (decimal.GetBits(value)[3] & int.MinValue) != 0;

    public static int GetScale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 31;

    public static decimal Round(decimal value, int scale, object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        var mode = ResolveRounding(rounding, context, span);
        return mode switch
        {
            PyDecimalContext.RoundHalfEven => decimal.Round(value, scale, MidpointRounding.ToEven),
            PyDecimalContext.RoundHalfUp => RoundHalfUp(value, scale),
            PyDecimalContext.RoundHalfDown => RoundHalfDown(value, scale),
            PyDecimalContext.RoundDown => TruncateToScale(value, scale),
            PyDecimalContext.RoundUp => AwayFromZeroToScale(value, scale),
            PyDecimalContext.RoundCeiling => CeilingToScale(value, scale),
            PyDecimalContext.RoundFloor => FloorToScale(value, scale),
            PyDecimalContext.Round05Up => Round05Up(value, scale),
            _ => throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span),
        };
    }

    private static PyDecimal FromTuple(PyDecimalTuple tuple, LythonSourceSpan span)
    {
        var digits = DigitsToString(tuple.Digits);
        var exponent = tuple.Exponent;
        if (exponent < -28)
        {
            throw new LythonRuntimeException("InvalidOperation", "DecimalTuple exponent is outside Lython's 28-digit fixed-precision scale.", span);
        }

        var builder = new StringBuilder();
        if (tuple.Sign == 1)
        {
            builder.Append('-');
        }

        if (exponent >= 0)
        {
            builder.Append(digits);
            builder.Append('0', (int)exponent);
        }
        else
        {
            var scale = (int)-exponent;
            if (digits.Length <= scale)
            {
                builder.Append("0.");
                builder.Append('0', scale - digits.Length);
                builder.Append(digits);
            }
            else
            {
                builder.Append(digits[..^scale]);
                builder.Append('.');
                builder.Append(digits[^scale..]);
            }
        }

        return decimal.TryParse(builder.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? new PyDecimal(parsed)
            : throw new LythonRuntimeException("InvalidOperation", "DecimalTuple is outside Lython's fixed-precision Decimal range.", span);
    }

    private static object Binary(object left, object right, LythonSourceSpan span, Func<decimal, decimal, decimal> operation)
    {
        if (!TryAsDecimal(left, out var lhs) || !TryAsDecimal(right, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Decimal arithmetic requires Decimal and integer operands.", span);
        }

        return new PyDecimal(operation(lhs, rhs));
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

    private static string ResolveRounding(object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        if (rounding is null or PyNone)
        {
            return context?.Rounding ?? PyDecimalContext.RoundHalfEven;
        }

        if (!PyStringOps.TryAsString(rounding, out var mode))
        {
            throw new LythonRuntimeException("TypeError", "Decimal rounding argument expects a rounding constant.", span);
        }

        var text = mode.AsString();
        return PyDecimalContext.IsSupportedRounding(text)
            ? text
            : throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span);
    }

    private static decimal TruncateToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Truncate(value * factor) / factor;
    }

    private static decimal AwayFromZeroToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var truncated = decimal.Truncate(scaled);
        if (scaled == truncated)
        {
            return truncated / factor;
        }

        var adjusted = scaled > 0 ? truncated + 1 : truncated - 1;
        return adjusted / factor;
    }

    private static decimal CeilingToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Ceiling(value * factor) / factor;
    }

    private static decimal FloorToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Floor(value * factor) / factor;
    }

    private static decimal RoundHalfUp(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var sign = Math.Sign(scaled);
        return sign >= 0
            ? decimal.Floor(scaled + 0.5m) / factor
            : decimal.Ceiling(scaled - 0.5m) / factor;
    }

    private static decimal RoundHalfDown(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var truncated = decimal.Truncate(scaled);
        var fraction = decimal.Abs(scaled - truncated);
        if (fraction <= 0.5m)
        {
            return truncated / factor;
        }

        return (scaled > 0 ? truncated + 1 : truncated - 1) / factor;
    }

    private static decimal Round05Up(decimal value, int scale)
    {
        var truncated = TruncateToScale(value, scale);
        var factor = Pow(10m, scale);
        var lastKeptDigit = (int)(decimal.Abs(truncated * factor) % 10);
        return lastKeptDigit is 0 or 5 && truncated != value
            ? AwayFromZeroToScale(value, scale)
            : truncated;
    }

    private static PyDecimalContext? ExpectContextOrNone(object value, LythonSourceSpan span)
        => value is PyNone
            ? null
            : value as PyDecimalContext ?? throw new LythonRuntimeException("TypeError", "Decimal method context argument expects a Context or None.", span);

    private static int ExpectInt(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) ||
            integer < int.MinValue ||
            integer > int.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (int)integer;
    }

    private static PyTuple MaterializeDigits(object value, LythonSourceSpan span)
    {
        IEnumerable<object> items = value switch
        {
            PyTuple tuple => tuple,
            PyList list => list,
            IPyIterableValue iterable => iterable.Iterate(),
            _ => throw new LythonRuntimeException("TypeError", "DecimalTuple digits must be an iterable of digits.", span)
        };

        var materialized = new List<object>();
        foreach (var item in items)
        {
            if (!Numbers.PyNumberOps.TryAsInteger(item, out var digit) || digit < 0 || digit > 9)
            {
                throw new LythonRuntimeException("ValueError", "DecimalTuple digits must be integers from 0 to 9.", span);
            }

            materialized.Add(digit);
        }

        if (materialized.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", "DecimalTuple digits cannot be empty.", span);
        }

        return new PyTuple(materialized);
    }

    private static string DigitsToString(PyTuple digits)
    {
        var builder = new StringBuilder(digits.Count);
        foreach (var item in digits)
        {
            _ = Numbers.PyNumberOps.TryAsInteger(item, out var digit);
            builder.Append((char)('0' + (int)digit));
        }

        return builder.ToString();
    }

    private static PyTuple DigitsFromString(string digits)
    {
        var values = new object[digits.Length];
        for (var i = 0; i < digits.Length; i++)
        {
            values[i] = new BigInteger(digits[i] - '0');
        }

        return new PyTuple(values);
    }

    private static int CompareDigitTuples(PyTuple left, PyTuple right)
    {
        var count = Math.Min(left.Count, right.Count);
        for (var i = 0; i < count; i++)
        {
            _ = Numbers.PyNumberOps.TryAsInteger(left[i], out var leftDigit);
            _ = Numbers.PyNumberOps.TryAsInteger(right[i], out var rightDigit);
            var comparison = leftDigit.CompareTo(rightDigit);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    private static bool IsUnsupportedSpecialValue(string text)
    {
        var normalized = text.TrimStart('+', '-');
        return normalized.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("snan", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("inf", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("infinity", StringComparison.OrdinalIgnoreCase);
    }
}
