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

    private sealed class PercentStringFormatter
    {
        private readonly string _format;
        private readonly object _arguments;
        private readonly PyTuple? _tupleArguments;
        private readonly GovernedByteBuilder _builder;
        private readonly PercentSpecifierParser _parser;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private int _argumentIndex;
        private bool _sawMappingKey;

        public PercentStringFormatter(
            PyString template,
            object arguments,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            _format = template.AsString();
            _arguments = arguments;
            _tupleArguments = arguments as PyTuple;
            _builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            _parser = new PercentSpecifierParser(_format, NextStarInteger, span);
            _context = context;
            _span = span;
        }

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

                var specifier = _parser.Parse(ref index);
                _sawMappingKey |= specifier.MappingKey is not null;
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
            return _arguments;
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

            var runtimeKey = PyString.FromString(key, _context.MemoryGovernor, _span);
            return _arguments switch
            {
                PyDict dict => dict.TryGetValue(runtimeKey, out var value)
                    ? value
                    : throw new LythonRuntimeException("KeyError", key, _span),
                PyDefaultDict defaultDict => defaultDict.GetOrCreate(runtimeKey, _context, _span),
                PyCounter counter => counter.GetCount(runtimeKey),
                IPySubscriptableValue mapping => mapping.GetSubscript(runtimeKey, _span),
                PyInstance instance => GetUserItem(instance, runtimeKey, _context, _span),
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
                's' => ToInterpolatedPyString(value, _context),
                'r' => ToReprPyString(value, _context),
                'a' => PyString.FromString(
                    EscapeNonAscii(ToReprPyString(value, _context).AsString()),
                    _context.MemoryGovernor,
                    _span),
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
                _context.MemoryGovernor.EnsureCanReserve(precision, _span);
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
            _context.MemoryGovernor.EnsureCanReserve((specifier.Precision ?? 6) + 32L, _span);
            var negative = !double.IsNaN(floating) && double.IsNegative(floating);
            var magnitude = Math.Abs(floating);
            var digits = PercentFloatingFormatter.Format(magnitude, specifier);

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
                    throw new LythonRuntimeException("OverflowError", "%c arg not in range(0x110000)", _span);
                }

                character = PyString.FromString(
                    new Rune((int)integer).ToString(),
                    _context.MemoryGovernor,
                    _span);
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
                    throw new LythonRuntimeException("OverflowError", "cannot convert float infinity to integer", _span);
                }

                if (double.IsNaN(floating))
                {
                    throw new LythonRuntimeException("ValueError", "cannot convert float NaN to integer", _span);
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
                if (instance.TryGetAttribute(methodName, _context, _span, out var member) && member is ICallable callable)
                {
                    var converted = callable.Invoke([], _span, _context);
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
                            throw new LythonRuntimeException("OverflowError", "int too large to convert to float", _span);
                        }

                        return converted;
                    }
                case bool boolean:
                    return boolean ? 1.0 : 0.0;
                case PyDecimal decimalValue:
                    return (double)decimalValue.Value;
                case PyInstance instance when
                    instance.TryGetAttribute("__float__", _context, _span, out var member) &&
                    member is ICallable callable:
                    {
                        var converted = callable.Invoke([], _span, _context);
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

        private LythonRuntimeException PercentTypeError(string message)
            => new("TypeError", message, _span);
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
