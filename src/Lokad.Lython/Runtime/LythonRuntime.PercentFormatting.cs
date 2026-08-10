using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyString FormatPercentString(
        PyString template,
        object arguments,
        ExecutionContext context,
        LythonSourceSpan span)
        => new PercentStringFormatter(template, arguments, context, span).Format();

    private sealed class PercentStringFormatter(
        PyString template,
        object arguments,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        private readonly string _format = template.AsString();
        private readonly PyTuple? _tupleArguments = arguments as PyTuple;
        private readonly GovernedByteBuilder _builder = new(context.MemoryGovernor, span);
        private int _argumentIndex;
        private bool _sawMappingKey;

        public PyString Format()
        {
            var literalStart = 0;
            var index = 0;
            while (index < _format.Length)
            {
                if (_format[index] != '%')
                {
                    index++;
                    continue;
                }

                _builder.AppendString(_format[literalStart..index]);
                index++;
                if (index < _format.Length && _format[index] == '%')
                {
                    _builder.Append((byte)'%');
                    index++;
                    literalStart = index;
                    continue;
                }

                var specifier = ParseSpecifier(ref index);
                var value = specifier.MappingKey is null
                    ? NextArgument()
                    : ResolveMappingValue(specifier.MappingKey);
                AppendFormatted(value, specifier);
                literalStart = index;
            }

            _builder.AppendString(_format[literalStart..]);
            EnsureAllArgumentsConsumed();
            return _builder.ToPyStringAndRelease();
        }

        private PercentSpecifier ParseSpecifier(ref int index)
        {
            if (index >= _format.Length)
            {
                throw PercentValueError("incomplete format");
            }

            string? mappingKey = null;
            if (_format[index] == '(')
            {
                mappingKey = ParseMappingKey(ref index);
                _sawMappingKey = true;
            }

            var alternate = false;
            var zeroPad = false;
            var leftAdjust = false;
            var plusSign = false;
            var spaceSign = false;
            while (index < _format.Length)
            {
                switch (_format[index])
                {
                    case '#':
                        alternate = true;
                        index++;
                        continue;
                    case '0':
                        zeroPad = true;
                        index++;
                        continue;
                    case '-':
                        leftAdjust = true;
                        index++;
                        continue;
                    case '+':
                        plusSign = true;
                        index++;
                        continue;
                    case ' ':
                        spaceSign = true;
                        index++;
                        continue;
                }

                break;
            }

            int? width = null;
            if (index < _format.Length && _format[index] == '*')
            {
                index++;
                var starWidth = NextStarInteger();
                if (starWidth < 0)
                {
                    leftAdjust = true;
                    starWidth = -starWidth;
                }

                width = ToFormatSize(starWidth, "width");
            }
            else if (index < _format.Length && char.IsAsciiDigit(_format[index]))
            {
                width = ParseFormatSize(ref index, "width");
            }

            int? precision = null;
            if (index < _format.Length && _format[index] == '.')
            {
                index++;
                if (index < _format.Length && _format[index] == '*')
                {
                    index++;
                    precision = ToFormatSize(BigInteger.Max(BigInteger.Zero, NextStarInteger()), "precision");
                }
                else
                {
                    precision = index < _format.Length && char.IsAsciiDigit(_format[index])
                        ? ParseFormatSize(ref index, "precision")
                        : 0;
                }
            }

            while (index < _format.Length && _format[index] is 'h' or 'l' or 'L')
            {
                index++;
            }

            if (index >= _format.Length)
            {
                throw PercentValueError("incomplete format");
            }

            var conversion = _format[index++];
            if (conversion is not ('s' or 'r' or 'a' or 'd' or 'i' or 'u' or 'o' or 'x' or 'X' or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or 'c'))
            {
                throw PercentValueError($"unsupported format character '{conversion}'");
            }

            return new PercentSpecifier(
                mappingKey,
                alternate,
                zeroPad,
                leftAdjust,
                plusSign,
                spaceSign,
                width,
                precision,
                conversion);
        }

        private string ParseMappingKey(ref int index)
        {
            index++;
            var start = index;
            var depth = 1;
            while (index < _format.Length)
            {
                if (_format[index] == '(')
                {
                    depth++;
                }
                else if (_format[index] == ')' && --depth == 0)
                {
                    var key = _format[start..index];
                    index++;
                    return key;
                }

                index++;
            }

            throw PercentValueError("incomplete format key");
        }

        private int ParseFormatSize(ref int index, string owner)
        {
            var value = BigInteger.Zero;
            while (index < _format.Length && char.IsAsciiDigit(_format[index]))
            {
                value = value * 10 + (_format[index] - '0');
                index++;
                if (value > int.MaxValue)
                {
                    throw PercentValueError($"{owner} too big");
                }
            }

            return (int)value;
        }

        private object NextArgument()
        {
            if (_tupleArguments is not null)
            {
                if (_argumentIndex >= _tupleArguments.Count)
                {
                    throw PercentTypeError("not enough arguments for format string");
                }

                return _tupleArguments[_argumentIndex++];
            }

            if (_argumentIndex != 0 || _sawMappingKey)
            {
                throw PercentTypeError("not enough arguments for format string");
            }

            _argumentIndex = 1;
            return arguments;
        }

        private BigInteger NextStarInteger()
        {
            var value = NextArgument();
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw PercentTypeError("* wants int");
            }

            return integer;
        }

        private object ResolveMappingValue(string key)
        {
            if (_tupleArguments is not null)
            {
                throw PercentTypeError("format requires a mapping");
            }

            var runtimeKey = PyString.FromString(key, context.MemoryGovernor, span);
            return arguments switch
            {
                PyDict dict => dict.TryGetValue(runtimeKey, out var value)
                    ? value
                    : throw new LythonRuntimeException("KeyError", key, span),
                PyDefaultDict defaultDict => defaultDict.GetOrCreate(runtimeKey, context, span),
                PyCounter counter => counter.GetCount(runtimeKey),
                IPySubscriptableValue mapping => mapping.GetSubscript(runtimeKey, span),
                PyInstance instance => GetUserItem(instance, runtimeKey, context, span),
                _ => throw PercentTypeError("format requires a mapping")
            };
        }

        private void AppendFormatted(object value, PercentSpecifier specifier)
        {
            switch (specifier.Conversion)
            {
                case 's':
                case 'r':
                case 'a':
                    AppendText(value, specifier);
                    return;
                case 'd':
                case 'i':
                case 'u':
                case 'o':
                case 'x':
                case 'X':
                    AppendInteger(value, specifier);
                    return;
                case 'e':
                case 'E':
                case 'f':
                case 'F':
                case 'g':
                case 'G':
                    AppendFloating(value, specifier);
                    return;
                case 'c':
                    AppendCharacter(value, specifier);
                    return;
            }
        }

        private void AppendText(object value, PercentSpecifier specifier)
        {
            var rendered = specifier.Conversion switch
            {
                's' => ToInterpolatedPyString(value, context),
                'r' => ToReprPyString(value, context),
                'a' => PyString.FromString(
                    EscapeNonAscii(ToReprPyString(value, context).AsString()),
                    context.MemoryGovernor,
                    span),
                _ => PyString.Empty
            };

            if (specifier.Precision is { } precision && rendered.Length > precision)
            {
                rendered = rendered.Slice(new PyIndexing.SliceBounds(0, precision, 1));
            }

            AppendPadded(rendered, specifier.Width, specifier.LeftAdjust);
        }

        private void AppendInteger(object value, PercentSpecifier specifier)
        {
            var integer = CoerceInteger(value, allowFloat: specifier.Conversion is 'd' or 'i' or 'u');
            var negative = integer.Sign < 0;
            var magnitude = BigInteger.Abs(integer);
            var upper = specifier.Conversion == 'X';
            var radix = specifier.Conversion switch
            {
                'o' => 8,
                'x' or 'X' => 16,
                _ => 10
            };
            var digits = radix == 10
                ? magnitude.ToString(CultureInfo.InvariantCulture)
                : ToUnsignedBaseString(magnitude, radix, upper);
            if (specifier.Precision is { } precision && digits.Length < precision)
            {
                context.MemoryGovernor.EnsureCanReserve(precision, span);
                digits = new string('0', precision - digits.Length) + digits;
            }

            var sign = NumericSign(negative, specifier);
            var prefix = specifier.Alternate
                ? specifier.Conversion switch
                {
                    'o' => "0o",
                    'x' => "0x",
                    'X' => "0X",
                    _ => string.Empty
                }
                : string.Empty;
            AppendNumeric(sign + prefix + digits, sign.Length + prefix.Length, specifier);
        }

        private void AppendFloating(object value, PercentSpecifier specifier)
        {
            var floating = CoerceFloat(value);
            context.MemoryGovernor.EnsureCanReserve((specifier.Precision ?? 6) + 32L, span);
            var negative = !double.IsNaN(floating) && double.IsNegative(floating);
            var magnitude = Math.Abs(floating);
            var upper = specifier.Conversion is 'E' or 'F' or 'G';
            string digits;
            if (double.IsNaN(magnitude))
            {
                digits = upper ? "NAN" : "nan";
            }
            else if (double.IsPositiveInfinity(magnitude))
            {
                digits = upper ? "INF" : "inf";
            }
            else
            {
                digits = specifier.Conversion switch
                {
                    'f' or 'F' => FormatFixed(magnitude, specifier.Precision ?? 6, specifier.Alternate),
                    'e' or 'E' => FormatExponential(magnitude, specifier.Precision ?? 6, specifier.Alternate, upper),
                    'g' or 'G' => FormatGeneral(magnitude, specifier.Precision ?? 6, specifier.Alternate, upper),
                    _ => string.Empty
                };
            }

            var sign = NumericSign(negative, specifier);
            AppendNumeric(sign + digits, sign.Length, specifier);
        }

        private void AppendCharacter(object value, PercentSpecifier specifier)
        {
            PyString character;
            if (PyStringOps.TryAsString(value, out var text))
            {
                if (text.Length != 1)
                {
                    throw PercentTypeError("%c requires int or char");
                }

                character = text;
            }
            else
            {
                var integer = CoerceInteger(value, allowFloat: false);
                if (integer < 0 || integer > 0x10ffff || integer >= 0xd800 && integer <= 0xdfff)
                {
                    throw new LythonRuntimeException("OverflowError", "%c arg not in range(0x110000)", span);
                }

                character = PyString.FromString(
                    new Rune((int)integer).ToString(),
                    context.MemoryGovernor,
                    span);
            }

            AppendPadded(character, specifier.Width, specifier.LeftAdjust);
        }

        private BigInteger CoerceInteger(object value, bool allowFloat)
        {
            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return integer;
            }

            if (allowFloat && value is double floating)
            {
                if (double.IsPositiveInfinity(floating) || double.IsNegativeInfinity(floating))
                {
                    throw new LythonRuntimeException("OverflowError", "cannot convert float infinity to integer", span);
                }

                if (double.IsNaN(floating))
                {
                    throw new LythonRuntimeException("ValueError", "cannot convert float NaN to integer", span);
                }

                return new BigInteger(floating);
            }

            if (allowFloat && value is PyDecimal decimalValue)
            {
                return new BigInteger(decimal.Truncate(decimalValue.Value));
            }

            if (value is PyInstance instance)
            {
                var methodName = allowFloat ? "__int__" : "__index__";
                if (instance.TryGetAttribute(methodName, context, span, out var member) && member is ICallable callable)
                {
                    var converted = callable.Invoke([], span, context);
                    if (PyNumberOps.TryAsInteger(converted, out integer))
                    {
                        return integer;
                    }

                    throw PercentTypeError($"{methodName} returned non-int");
                }
            }

            throw PercentTypeError(allowFloat
                ? "%d format: a number is required"
                : "integer format: an integer is required");
        }

        private double CoerceFloat(object value)
        {
            switch (value)
            {
                case double floating:
                    return floating;
                case BigInteger integer:
                    {
                        var converted = (double)integer;
                        if (double.IsInfinity(converted))
                        {
                            throw new LythonRuntimeException("OverflowError", "int too large to convert to float", span);
                        }

                        return converted;
                    }
                case bool boolean:
                    return boolean ? 1.0 : 0.0;
                case PyDecimal decimalValue:
                    return (double)decimalValue.Value;
                case PyInstance instance when
                    instance.TryGetAttribute("__float__", context, span, out var member) &&
                    member is ICallable callable:
                    {
                        var converted = callable.Invoke([], span, context);
                        if (converted is double result)
                        {
                            return result;
                        }

                        throw PercentTypeError("__float__ returned non-float");
                    }
                default:
                    throw PercentTypeError("must be real number, not non-numeric value");
            }
        }

        private void AppendPadded(PyString value, int? width, bool leftAdjust)
        {
            var padding = Math.Max(0, (width ?? 0) - value.Length);
            if (!leftAdjust)
            {
                _builder.AppendRepeated((byte)' ', padding);
            }

            _builder.Append(value);
            if (leftAdjust)
            {
                _builder.AppendRepeated((byte)' ', padding);
            }
        }

        private void AppendNumeric(string value, int prefixLength, PercentSpecifier specifier)
        {
            var padding = Math.Max(0, (specifier.Width ?? 0) - value.Length);
            if (specifier.LeftAdjust)
            {
                _builder.AppendAscii(value);
                _builder.AppendRepeated((byte)' ', padding);
                return;
            }

            if (specifier.ZeroPad)
            {
                _builder.AppendAscii(value[..prefixLength]);
                _builder.AppendRepeated((byte)'0', padding);
                _builder.AppendAscii(value[prefixLength..]);
                return;
            }

            _builder.AppendRepeated((byte)' ', padding);
            _builder.AppendAscii(value);
        }

        private void EnsureAllArgumentsConsumed()
        {
            if (_tupleArguments is not null)
            {
                if (_argumentIndex != _tupleArguments.Count)
                {
                    throw PercentTypeError("not all arguments converted during string formatting");
                }

                return;
            }

            if (_argumentIndex == 0 && !_sawMappingKey)
            {
                throw PercentTypeError("not all arguments converted during string formatting");
            }
        }

        private static string NumericSign(bool negative, PercentSpecifier specifier)
            => negative
                ? "-"
                : specifier.PlusSign
                    ? "+"
                    : specifier.SpaceSign
                        ? " "
                        : string.Empty;

        private static string FormatFixed(double value, int precision, bool alternate)
        {
            var text = value.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return alternate && precision == 0 ? text + "." : text;
        }

        private static string FormatExponential(double value, int precision, bool alternate, bool upper)
        {
            var text = value.ToString("E" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var exponentIndex = text.IndexOf('E');
            var mantissa = text[..exponentIndex];
            if (alternate && precision == 0)
            {
                mantissa += ".";
            }

            var exponent = int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
            return mantissa + (upper ? "E" : "e") + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string FormatGeneral(double value, int precision, bool alternate, bool upper)
        {
            precision = precision == 0 ? 1 : precision;
            var rounded = double.Parse(
                value.ToString("G" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            var exponent = rounded == 0.0 ? 0 : (int)Math.Floor(Math.Log10(rounded));
            var scientific = exponent < -4 || exponent >= precision;
            if (!scientific)
            {
                var decimals = Math.Max(0, precision - exponent - 1);
                var fixedText = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                var integerDigits = fixedText.TrimStart('0').TakeWhile(char.IsAsciiDigit).Count();
                if (integerDigits <= precision)
                {
                    return alternate ? EnsureDecimalPoint(fixedText) : TrimFractionZeros(fixedText);
                }
            }

            var exponential = FormatExponential(value, precision - 1, alternate, upper);
            if (alternate)
            {
                return exponential;
            }

            var marker = upper ? 'E' : 'e';
            var markerIndex = exponential.IndexOf(marker);
            return TrimFractionZeros(exponential[..markerIndex]) + exponential[markerIndex..];
        }

        private static string EnsureDecimalPoint(string value)
            => value.Contains('.', StringComparison.Ordinal) ? value : value + ".";

        private static string TrimFractionZeros(string value)
            => value.Contains('.', StringComparison.Ordinal)
                ? value.TrimEnd('0').TrimEnd('.')
                : value;

        private int ToFormatSize(BigInteger value, string owner)
        {
            if (value > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", $"{owner} too big", span);
            }

            return (int)value;
        }

        private LythonRuntimeException PercentTypeError(string message)
            => new("TypeError", message, span);

        private LythonRuntimeException PercentValueError(string message)
            => new("ValueError", message, span);
    }

    private sealed record PercentSpecifier(
        string? MappingKey,
        bool Alternate,
        bool ZeroPad,
        bool LeftAdjust,
        bool PlusSign,
        bool SpaceSign,
        int? Width,
        int? Precision,
        char Conversion);
}
