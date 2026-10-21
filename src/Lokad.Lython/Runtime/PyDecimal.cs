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
        return PyString.FromString($"Decimal('{PyDecimalOps.Format(this)}')", context.Context.MemoryGovernor);
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        return PyString.FromString(PyDecimalOps.Format(this), context.Context.MemoryGovernor);
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

    public static PyDecimal Parse(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        switch (value)
        {
            case PyDecimal pyDecimal:
                return pyDecimal;
            case PyDecimalTuple decimalTuple:
                return FromTuple(decimalTuple, span, context);
            case bool boolean:
                return new PyDecimal(boolean ? 1m : 0m);
            case BigInteger integer when integer >= (BigInteger)decimal.MinValue && integer <= (BigInteger)decimal.MaxValue:
                return new PyDecimal((decimal)integer);
            case BigInteger:
                throw PyDecimalOps.InvalidOperation("Decimal integer value is outside Lython's fixed-precision Decimal range.", span);
            case double floating when double.IsFinite(floating):
                try
                {
                    return new PyDecimal((decimal)floating);
                }
                catch (OverflowException)
                {
                    throw PyDecimalOps.InvalidOperation("Decimal float value is outside Lython's fixed-precision Decimal range.", span);
                }

            case double:
                throw new LythonRuntimeException(
                    "InvalidOperation",
                    "NaN, sNaN, and Infinity are not supported by Lython's fixed-precision Decimal.",
                    span);
            default:
                if (PyStringOps.TryAsString(value, out var text))
                {
                    context.CheckExecution(span);
                    using var storage = context.MemoryGovernor.ReserveTemporary(128L + 2L * text.Utf8Bytes.Length, span);
                    var characters = new char[text.Utf8Bytes.Length];
                    var raw = characters.AsSpan(0, text.CopyUtf16To(characters)).Trim();
                    context.CheckExecution(span);
                    if (IsUnsupportedSpecialValue(raw))
                    {
                        throw new LythonRuntimeException(
                            "InvalidOperation",
                            "NaN, sNaN, and Infinity are not supported by Lython's fixed-precision Decimal.",
                            span);
                    }

                    if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        var stringExponent = ParseExponent(raw, span);
                        if (stringExponent < -28 && parsed != 0m)
                        {
                            // TryParse rounded an over-precise fraction; keep
                            // the honest scale instead of an unusable exponent.
                            // Zero keeps its exponent (harmless without scaling).
                            stringExponent = -PyDecimalOps.GetScale(parsed);
                        }

                        return new PyDecimal(parsed, stringExponent);
                    }

                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        throw PyDecimalOps.InvalidOperation("Decimal string value is outside Lython's fixed-precision Decimal range.", span);
                    }
                }

                throw new LythonRuntimeException("TypeError", "Decimal(...) expects a decimal-compatible string, tuple, or number.", span);
        }

        static bool IsUnsupportedSpecialValue(ReadOnlySpan<char> text)
        {
            var normalized = text.TrimStart("+-");
            return normalized.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("snan", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("inf", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("infinity", StringComparison.OrdinalIgnoreCase);
        }

        static int ParseExponent(ReadOnlySpan<char> text, LythonSourceSpan span)
        {
            try
            {
                var exponentMarker = text.IndexOfAny('e', 'E');
                var mantissa = exponentMarker < 0 ? text : text[..exponentMarker];
                var explicitExponent = exponentMarker < 0
                    ? 0
                    : int.Parse(text[(exponentMarker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                var decimalPoint = mantissa.IndexOf('.');
                var fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
                return checked(explicitExponent - fractionalDigits);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw PyDecimalOps.InvalidOperation("Decimal exponent is outside Lython's 28-digit fixed-precision scale.", span);
            }
        }
    }

    public static async ValueTask<PyDecimalTuple> CreateTupleAsync(object signValue, object digitsValue, object exponentValue,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
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

        using var digits = asynchronous
            ? await PyIteration.DrainLeasedAsync(ValidateDigitsAsync(PyIteration.ToSequenceAsync(digitsValue, span, context), span), span, context).ConfigureAwait(false)
            : PyIteration.DrainLeased(ValidateDigits(PyIteration.ToSequence(digitsValue, span, context), span), span, context);
        return new PyDecimalTuple(sign, new PyTuple(digits.Items, context.MemoryGovernor, span), exponent);
    }

    private static PyDecimal FromTuple(PyDecimalTuple tuple, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var exponent = tuple.Exponent;
        if (exponent < -28 || exponent > 28)
        {
            throw PyDecimalOps.InvalidOperation("DecimalTuple exponent is outside Lython's 28-digit fixed-precision scale.", span);
        }

        context.CheckExecution(span);
        var count = Math.Max(1, tuple.Digits.Count);
        var point = (long)count + (int)exponent;
        var length = (tuple.Sign == 1 ? 1L : 0) + (exponent >= 0 ? point : point <= 0 ? 2L - (int)exponent : count + 1L);
        using var storage = context.MemoryGovernor.ReserveTemporary(128L + 2L * length, span);
        if (length > int.MaxValue) throw RuntimeErrors.Memory("DecimalTuple text exceeds supported storage size", span);
        var characters = new char[(int)length];
        var position = 0;
        if (tuple.Sign == 1) characters[position++] = '-';
        if (point <= 0)
        {
            characters[position++] = '0';
            characters[position++] = '.';
            characters.AsSpan(position, (int)-point).Fill('0');
            position += (int)-point;
        }
        for (var index = 0; index < count; index++)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (exponent < 0 && point > 0 && index == point) characters[position++] = '.';
            var digit = BigInteger.Zero;
            if (tuple.Digits.Count != 0 && !Numbers.PyNumberOps.TryAsInteger(tuple.Digits[index], out digit))
                throw new InvalidOperationException("Invalid stored decimal digit.");
            characters[position++] = (char)('0' + (int)digit);
        }
        if (exponent >= 0) characters.AsSpan(position).Fill('0');
        context.CheckExecution(span);
        return decimal.TryParse(characters.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? new PyDecimal(parsed, (int)exponent)
            : throw PyDecimalOps.InvalidOperation("DecimalTuple is outside Lython's fixed-precision Decimal range.", span);
    }


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

    private static object ValidateDigit(object item, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(item, out var digit) || digit < 0 || digit > 9)
        {
            throw new LythonRuntimeException("ValueError", "DecimalTuple digits must be integers from 0 to 9.", span);
        }
        return item;
    }

    private static IEnumerable<object> ValidateDigits(IEnumerable<object> items, LythonSourceSpan span)
    {
        foreach (var item in items) yield return ValidateDigit(item, span);
    }

    private static async IAsyncEnumerable<object> ValidateDigitsAsync(IAsyncEnumerable<object> items, LythonSourceSpan span)
    {
        await foreach (var item in items.ConfigureAwait(false)) yield return ValidateDigit(item, span);
    }

    internal static string DigitsToString(PyTuple digits)
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

}
