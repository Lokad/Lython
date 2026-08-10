using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDecimal : IPyTruthyValue, IPyRenderableValue, IPyHashableValue
{
    public PyDecimal(decimal value)
        : this(value, -PyDecimalOps.GetScale(value))
    {
    }

    public PyDecimal(decimal value, int exponent)
    {
        Value = value;
        Exponent = exponent;
    }

    public decimal Value { get; }

    public int Exponent { get; }

    public bool IsTruthy() => Value != 0m;

    public bool IsSigned => PyDecimalOps.IsSigned(Value);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"Decimal('{PyDecimalOps.Format(this)}')");
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(PyDecimalOps.Format(this));
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

internal static partial class PyDecimalOps
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
                        return new PyDecimal(parsed, ParseExponent(raw));
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

    private static PyDecimal FromTuple(PyDecimalTuple tuple, LythonSourceSpan span)
    {
        var digits = DigitsToString(tuple.Digits);
        var exponent = tuple.Exponent;
        if (exponent < -28 || exponent > 28 || exponent < int.MinValue || exponent > int.MaxValue)
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
            ? new PyDecimal(parsed, (int)exponent)
            : throw new LythonRuntimeException("InvalidOperation", "DecimalTuple is outside Lython's fixed-precision Decimal range.", span);
    }

    private static int ParseExponent(string text)
    {
        var exponentMarker = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentMarker < 0 ? text : text[..exponentMarker];
        var explicitExponent = exponentMarker < 0
            ? 0
            : int.Parse(text[(exponentMarker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var decimalPoint = mantissa.IndexOf('.');
        var fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
        return checked(explicitExponent - fractionalDigits);
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
