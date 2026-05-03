using System.Globalization;
using System.Numerics;
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

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"Decimal('{Value.ToString(CultureInfo.InvariantCulture)}')");

    public PyString RenderInterpolated(PyRenderingContext context) => PyString.FromString(Value.ToString(CultureInfo.InvariantCulture));

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
            case bool boolean:
                return new PyDecimal(boolean ? 1m : 0m);
            case BigInteger integer when integer >= (BigInteger)decimal.MinValue && integer <= (BigInteger)decimal.MaxValue:
                return new PyDecimal((decimal)integer);
            case double floating when double.IsFinite(floating):
                return new PyDecimal((decimal)floating);
            default:
                if (PyStringOps.TryAsString(value, out var text) &&
                    decimal.TryParse(text.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return new PyDecimal(parsed);
                }

                throw new LythonRuntimeException("TypeError", "Decimal(...) expects a decimal-compatible string or number.", span);
        }
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

    public static PyDecimal Quantize(PyDecimal value, PyDecimal exponent, object? rounding, LythonSourceSpan span)
    {
        var scale = GetScale(exponent.Value);
        return new PyDecimal(Round(value.Value, scale, rounding, span));
    }

    public static PyDecimal Normalize(PyDecimal value)
    {
        return new PyDecimal(decimal.Parse(value.Value.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }

    public static PyDecimal Unary(string name, PyDecimal value, object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"Decimal.{name}() expects no arguments.", span);
        }

        return name switch
        {
            "sqrt" => new PyDecimal((decimal)Math.Sqrt((double)value.Value)),
            "exp" => new PyDecimal((decimal)Math.Exp((double)value.Value)),
            "ln" => new PyDecimal((decimal)Math.Log((double)value.Value)),
            "log10" => new PyDecimal((decimal)Math.Log10((double)value.Value)),
            "copy_abs" => new PyDecimal(decimal.Abs(value.Value)),
            "copy_negate" => new PyDecimal(-value.Value),
            "normalize" => Normalize(value),
            "to_integral_value" => new PyDecimal(decimal.Truncate(value.Value)),
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
        return new PyDecimal(sign < 0m ? -magnitude : magnitude);
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

    private static int GetScale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 31;

    private static decimal Round(decimal value, int scale, object? rounding, LythonSourceSpan span)
    {
        if (rounding is null or PyNone)
        {
            return decimal.Round(value, scale, MidpointRounding.ToEven);
        }

        if (!PyStringOps.TryAsString(rounding, out var mode))
        {
            throw new LythonRuntimeException("TypeError", "Decimal.quantize(..., rounding=...) expects a rounding constant.", span);
        }

        return mode.AsString() switch
        {
            "ROUND_HALF_EVEN" => decimal.Round(value, scale, MidpointRounding.ToEven),
            "ROUND_DOWN" => TruncateToScale(value, scale),
            "ROUND_UP" => AwayFromZeroToScale(value, scale),
            _ => throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span),
        };
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
}
