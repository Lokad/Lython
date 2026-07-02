using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal enum TextEncodingMode
    {
        Utf8,
        Utf8Bom,
    }

    private readonly record struct BoundOpenArguments(object[] Values, bool[] Assigned, int Count);

    private static object Open(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => Open(BindPositionalOpenArguments(arguments, span), span, context);

    private static object Open(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode) = ParseOpenArguments(arguments, span);
        var modeText = mode.AsString();
        return modeText switch
        {
            "r" => ExecutionContext.TextFileHandle.ForRead(path.AsString(), context, encodingMode),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode),
            "a" => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static async ValueTask<object> OpenAsync(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode) = ParseOpenArguments(arguments, span);
        var modeText = mode.AsString();
        return modeText switch
        {
            "r" => await ExecutionContext.TextFileHandle.ForReadAsync(path.AsString(), context, encodingMode).ConfigureAwait(false),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode),
            "a" => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static (PyString Path, PyString Mode, TextEncodingMode EncodingMode) ParseOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span)
    {
        var arguments = boundArguments.Values;
        if (boundArguments.Count is < 1 or > 5 || !PyStringOps.TryAsString(arguments[0], out var path))
        {
            throw new LythonRuntimeException("TypeError", "open(file/path[, mode][, encoding][, errors][, newline]) expects a string file/path plus supported text-mode options.", span);
        }

        var mode = boundArguments.Assigned[1]
            ? arguments[1] switch
            {
                PyString text => text,
                _ => throw new LythonRuntimeException("TypeError", "open(file/path, mode) expects mode to be a string.", span)
            }
            : PyString.FromString("r");

        var encodingMode = TextEncodingMode.Utf8;
        if (boundArguments.Count >= 3)
        {
            encodingMode = ParseTextEncoding(arguments[2], "open()", span);
        }

        if (boundArguments.Count >= 4)
        {
            ValidateOpenTextErrors(arguments[3], span);
        }

        if (boundArguments.Count == 5)
        {
            ValidateOpenNewline(arguments[4], span);
        }

        var modeText = mode.AsString();
        if (modeText.Contains('b'))
        {
            throw new LythonRuntimeException("ValueError", "open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", span);
        }

        return (path, mode, encodingMode);
    }

    private static BoundOpenArguments BindPositionalOpenArguments(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 5)
        {
            throw new LythonRuntimeException("TypeError", "open(file/path[, mode][, encoding][, errors][, newline]) received too many positional arguments.", span);
        }

        var bound = new object[5];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new bool[5];
        for (var i = 0; i < arguments.Length; i++)
        {
            bound[i] = arguments[i];
            assigned[i] = true;
        }

        return new BoundOpenArguments(bound, assigned, arguments.Length);
    }

    private static void ValidateOpenTextErrors(object value, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return;
        }

        if (!PyStringOps.TryAsString(value, out var errors) ||
            !errors.AsString().Equals("strict", StringComparison.OrdinalIgnoreCase))
        {
            throw new LythonRuntimeException("ValueError", "open() only supports errors='strict'.", span);
        }
    }

    private static void ValidateOpenNewline(object value, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return;
        }

        if (!PyStringOps.TryAsString(value, out var newline) || newline.Length != 0)
        {
            throw new LythonRuntimeException("ValueError", "open() only supports newline=''.", span);
        }
    }

    private static object Input(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "input([prompt]) expects zero or one string argument.", span);
        }

        if (arguments.Length == 1)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var prompt))
            {
                throw new LythonRuntimeException("TypeError", "input(prompt) expects prompt to be a string.", span);
            }

            _ = context.State.Stdout.Write(prompt, span);
        }

        var line = context.State.Stdin.ReadLine(span);
        return PyStringOps.TrimTrailingNewline(line);
    }

    private static async ValueTask<object> InputAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "input([prompt]) expects zero or one string argument.", span);
        }

        if (arguments.Length == 1)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var prompt))
            {
                throw new LythonRuntimeException("TypeError", "input(prompt) expects prompt to be a string.", span);
            }

            _ = await context.State.Stdout.WriteAsync(prompt, span).ConfigureAwait(false);
        }

        var line = await context.State.Stdin.ReadLineAsync(span).ConfigureAwait(false);
        return PyStringOps.TrimTrailingNewline(line);
    }

    private static TextEncodingMode ParseTextEncoding(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return TextEncodingMode.Utf8;
        }

        if (!PyStringOps.TryAsString(value, out var encoding))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} only supports encoding='utf-8' or 'utf-8-sig'.", span);
        }

        return encoding.AsString().ToLowerInvariant() switch
        {
            "utf-8" => TextEncodingMode.Utf8,
            "utf-8-sig" => TextEncodingMode.Utf8Bom,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} only supports encoding='utf-8' or 'utf-8-sig'.", span)
        };
    }

    private static object Str(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "str(value) expects one argument.", span);
        }

        return ToPythonPyString(arguments[0], context);
    }

    private static object Repr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "repr(value) expects one argument.", span);
        }

        return ToReprPyString(arguments[0], context);
    }

    private static object Ascii(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "ascii(value) expects one argument.", span);
        }

        return PyString.FromString(EscapeNonAscii(ToReprPyString(arguments[0], context).AsString()), context.MemoryGovernor, span);
    }

    private static object Format(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "format(value[, format_spec]) expects one or two arguments.", span);
        }

        var spec = string.Empty;
        if (arguments.Length == 2)
        {
            if (!PyStringOps.TryAsString(arguments[1], out var formatSpec))
            {
                throw new LythonRuntimeException("TypeError", "format(value[, format_spec]) expects format_spec to be a string.", span);
            }

            spec = formatSpec.AsString();
        }

        return PyString.FromString(FormatInterpolatedStringValue(arguments[0], spec, context, span), context.MemoryGovernor, span);
    }

    private static object Bool(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "bool(value) expects one argument.", span);
        }

        return IsTruthy(arguments[0]);
    }

    private static object Int(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "int(value) expects one argument.", span);
        }

        try
        {
            return arguments[0] switch
            {
                BigInteger integer => integer,
                double floating => new BigInteger(floating),
                PyDecimal decimalValue => new BigInteger(decimal.Truncate(decimalValue.Value)),
                PyString text => BigInteger.Parse(text.AsString(), CultureInfo.InvariantCulture),
                string text => BigInteger.Parse(text, CultureInfo.InvariantCulture),
                bool boolean => boolean ? BigInteger.One : BigInteger.Zero,
                _ => throw new LythonRuntimeException("TypeError", "int() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static object Abs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "abs(x) expects one argument.", span);
        }

        if (arguments[0] is PyDecimal decimalValue)
        {
            return new PyDecimal(decimal.Abs(decimalValue.Value));
        }

        if (!PyNumberOps.TryAsNumber(arguments[0], out var number))
        {
            throw new LythonRuntimeException("TypeError", "abs(x) expects a numeric value.", span);
        }

        return number.IsFloat ? Math.Abs(number.Floating) : BigInteger.Abs(number.Integer);
    }

    private static object Pow(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "pow(base, exp[, mod]) expects two or three arguments.", span);
        }

        if (arguments.Length == 3 && arguments[2] is not PyNone)
        {
            var integerBase = ExpectBuiltinInteger(arguments[0], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            var exponent = ExpectBuiltinInteger(arguments[1], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            var modulus = ExpectBuiltinInteger(arguments[2], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            if (modulus == BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "pow() 3rd argument cannot be 0.", span);
            }

            if (exponent < BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "pow() modular exponent must be non-negative in Lython.", span);
            }

            var absModulus = BigInteger.Abs(modulus);
            var normalizedBase = integerBase % absModulus;
            if (normalizedBase < BigInteger.Zero)
            {
                normalizedBase += absModulus;
            }

            var result = BigInteger.ModPow(normalizedBase, exponent, absModulus);
            return modulus < BigInteger.Zero && result != BigInteger.Zero ? result - absModulus : result;
        }

        if (arguments[0] is PyDecimal || arguments[1] is PyDecimal)
        {
            return PyDecimalOps.Power(arguments[0], arguments[1], span);
        }

        if (!PyNumberOps.TryAsNumber(arguments[0], out var lhs) ||
            !PyNumberOps.TryAsNumber(arguments[1], out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "pow(base, exp[, mod]) expects numeric arguments.", span);
        }

        return PyNumberOps.Power(lhs, rhs);
    }

    private static object Round(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "round(number[, ndigits]) expects one or two arguments.", span);
        }

        var hasDigits = arguments.Length == 2 && arguments[1] is not PyNone;
        var digits = hasDigits
            ? ToInt32(ExpectBuiltinInteger(arguments[1], "round(number[, ndigits]) expects ndigits to be an integer.", span), "round(number[, ndigits])", span)
            : 0;

        return arguments[0] switch
        {
            bool boolean => RoundInteger(boolean ? BigInteger.One : BigInteger.Zero, hasDigits, digits, span),
            BigInteger integer => RoundInteger(integer, hasDigits, digits, span),
            double floating => RoundFloat(floating, hasDigits, digits, span),
            PyDecimal decimalValue => RoundDecimal(decimalValue, hasDigits, digits, span),
            _ => throw new LythonRuntimeException("TypeError", "round(number[, ndigits]) expects a numeric value.", span)
        };
    }

    private static object Bin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return FormatIntegerBase(arguments, "bin(number) expects an integer.", "0b", 2, lower: true, span);
    }

    private static object Oct(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return FormatIntegerBase(arguments, "oct(number) expects an integer.", "0o", 8, lower: true, span);
    }

    private static object Hex(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return FormatIntegerBase(arguments, "hex(number) expects an integer.", "0x", 16, lower: true, span);
    }

    private static object Chr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "chr(i) expects one integer argument.", span);
        }

        var codePoint = ExpectBuiltinInteger(arguments[0], "chr(i) expects one integer argument.", span);
        if (codePoint < BigInteger.Zero || codePoint > new BigInteger(0x10FFFF))
        {
            throw new LythonRuntimeException("ValueError", "chr() arg not in range(0x110000).", span);
        }

        var value = (int)codePoint;
        if (!Rune.IsValid(value))
        {
            throw new LythonRuntimeException("ValueError", "chr() arg is not a valid Unicode scalar value.", span);
        }

        return PyString.FromString(new Rune(value).ToString(), context.MemoryGovernor, span);
    }

    private static object Ord(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text) || text.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "ord(c) expects a character.", span);
        }

        var source = text.AsString();
        var status = Rune.DecodeFromUtf16(source, out var rune, out var consumed);
        if (status != OperationStatus.Done || consumed != source.Length)
        {
            throw new LythonRuntimeException("TypeError", "ord(c) expects a character.", span);
        }

        return new BigInteger(rune.Value);
    }

    private static object Callable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "callable(object) expects one argument.", span);
        }

        return arguments[0] is ICallable;
    }

    private static object Hash(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "hash(object) expects one argument.", span);
        }

        try
        {
            return new BigInteger(PyValueComparer.Instance.GetHashCode(arguments[0]));
        }
        catch (InvalidOperationException)
        {
            throw new LythonRuntimeException("TypeError", "unhashable type", span);
        }
    }

    private static BigInteger ExpectBuiltinInteger(object value, string message, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return integer;
    }

    private static int ToInt32(BigInteger value, string owner, LythonSourceSpan span)
    {
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", $"{owner} integer argument is too large.", span);
        }

        return (int)value;
    }

    private static object FormatIntegerBase(object[] arguments, string message, string prefix, int radix, bool lower, LythonSourceSpan span)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        var integer = ExpectBuiltinInteger(arguments[0], message, span);
        var sign = integer < BigInteger.Zero ? "-" : string.Empty;
        var digits = ToUnsignedBaseString(BigInteger.Abs(integer), radix, upper: !lower);
        return PyString.FromString(sign + prefix + digits);
    }

    private static object RoundInteger(BigInteger value, bool hasDigits, int digits, LythonSourceSpan span)
    {
        if (!hasDigits || digits >= 0)
        {
            return value;
        }

        var factor = BigInteger.Pow(10, checked(-digits));
        var sign = value < BigInteger.Zero ? -1 : 1;
        var quotient = BigInteger.DivRem(BigInteger.Abs(value), factor, out var remainder);
        var comparison = (remainder * 2).CompareTo(factor);
        if (comparison > 0 || (comparison == 0 && !quotient.IsEven))
        {
            quotient += BigInteger.One;
        }

        return quotient * factor * sign;
    }

    private static object RoundFloat(double value, bool hasDigits, int digits, LythonSourceSpan span)
    {
        if (!hasDigits)
        {
            return new BigInteger(Math.Round(value, MidpointRounding.ToEven));
        }

        if (digits is >= 0 and <= 15)
        {
            return Math.Round(value, digits, MidpointRounding.ToEven);
        }

        if (digits > 15)
        {
            return value;
        }

        var factor = Math.Pow(10.0, -digits);
        return Math.Round(value / factor, MidpointRounding.ToEven) * factor;
    }

    private static object RoundDecimal(PyDecimal value, bool hasDigits, int digits, LythonSourceSpan span)
    {
        if (!hasDigits)
        {
            return new BigInteger(decimal.Round(value.Value, 0, MidpointRounding.ToEven));
        }

        if (digits is >= 0 and <= 28)
        {
            return new PyDecimal(decimal.Round(value.Value, digits, MidpointRounding.ToEven));
        }

        if (digits > 28)
        {
            return value;
        }

        var factor = DecimalPowerOfTen(checked(-digits), span);
        return new PyDecimal(decimal.Round(value.Value / factor, 0, MidpointRounding.ToEven) * factor);
    }

    private static decimal DecimalPowerOfTen(int exponent, LythonSourceSpan span)
    {
        try
        {
            var result = 1m;
            for (var i = 0; i < exponent; i++)
            {
                result *= 10m;
            }

            return result;
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static string EscapeNonAscii(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value <= 0x7F)
            {
                builder.Append(rune.ToString());
            }
            else if (rune.Value <= 0xFF)
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\x{rune.Value:x2}");
            }
            else if (rune.Value <= 0xFFFF)
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{rune.Value:x4}");
            }
            else
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\U{rune.Value:x8}");
            }
        }

        return builder.ToString();
    }

    private static object Float(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "float(value) expects one argument.", span);
        }

        try
        {
            return arguments[0] switch
            {
                double floating => floating,
                BigInteger integer => (double)integer,
                PyDecimal decimalValue => (double)decimalValue.Value,
                PyString text => double.Parse(text.AsString(), CultureInfo.InvariantCulture),
                string text => double.Parse(text, CultureInfo.InvariantCulture),
                bool boolean => boolean ? 1.0 : 0.0,
                _ => throw new LythonRuntimeException("TypeError", "float() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static object Bytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes([iterable]) expects zero or one argument.", span);
        }

        return arguments[0] switch
        {
            PyBytes bytes => CreateBytes(bytes.ToArray(), context, span),
            PyString text => CreateBytes(PyStringOps.EncodeUtf8(text), context, span),
            string text => CreateBytes(PyStringOps.EncodeUtf8(PyString.FromString(text)), context, span),
            _ => CreateBytes(ToByteArray(arguments[0], span), context, span)
        };
    }

    private static byte[] ToByteArray(object value, LythonSourceSpan span)
    {
        var bytes = new List<byte>();
        foreach (var item in ToSequence(value, span))
        {
            bytes.Add(ToByte(item, span));
        }

        return [.. bytes];
    }

    private static object Property(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "property([fget][, fset][, fdel]) expects zero to three callable arguments.", span);
        }

        var getter = arguments.Length >= 1 ? ParsePropertyCallable(arguments[0], "fget", span) : null;
        var setter = arguments.Length >= 2 ? ParsePropertyCallable(arguments[1], "fset", span) : null;
        var deleter = arguments.Length >= 3 ? ParsePropertyCallable(arguments[2], "fdel", span) : null;
        return new PyProperty(getter, setter, deleter);
    }

    private static object StaticMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "staticmethod(func) expects one callable argument.", span);
        }

        return new PyStaticMethod(callable);
    }

    private static object ClassMethod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "classmethod(func) expects one callable argument.", span);
        }

        return new PyClassMethod(callable);
    }

    private static object ObjectNew(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0 || arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
        }

        return new PyInstance(type);
    }

    private sealed class ObjectNewMethod : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            if (arguments.Length == 0 || arguments[0].Value is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "object.__new__(cls, ...) expects the first argument to be a class.", span);
            }

            return new PyInstance(type);
        }
    }

    private sealed class ObjectInitMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not PyInstance)
            {
                throw new LythonRuntimeException("TypeError", "object.__init__(self) does not accept additional arguments.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectInitSubclassMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length == 0 || arguments[0].Name is not null || arguments[0].Value is not PyType)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__(cls) expects a class receiver.", span);
            }

            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "object.__init_subclass__ does not accept keyword arguments in Lython.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectSetAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 3 ||
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
                arguments[2].Name is not null ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__setattr__(self, name, value) expects an instance, a string name, and a value.", span);
            }

            if (instance.Type.TryLookupInMro(name.AsString(), 0, out var rawValue, out _) &&
                PyAttributeLookup.TrySetDescriptorValue(rawValue, instance, arguments[2].Value, context, span))
            {
                return PyNone.Instance;
            }

            instance.SetAttribute(name.AsString(), arguments[2].Value);
            return PyNone.Instance;
        }
    }

    private sealed class ObjectDelAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__delattr__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (instance.Type.TryLookupInMro(memberName, 0, out var rawValue, out _) &&
                PyAttributeLookup.TryDeleteDescriptorValue(rawValue, instance, context, span))
            {
                return PyNone.Instance;
            }

            if (!instance.RemoveAttribute(memberName))
            {
                throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{memberName}'.", span);
            }

            return PyNone.Instance;
        }
    }

    private sealed class ObjectGetAttrMethod : IPyBindableCallable
    {
        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                arguments[0].Name is not null ||
                arguments[1].Name is not null ||
                arguments[0].Value is not PyInstance instance ||
                !PyStringOps.TryAsString(arguments[1].Value, out var name))
            {
                throw new LythonRuntimeException("TypeError", "object.__getattribute__(self, name) expects an instance and a string name.", span);
            }

            var memberName = name.AsString();
            if (PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, memberName, context, span, out var value))
            {
                return value;
            }

            throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{memberName}'.", span);
        }
    }

    private static ICallable? ParsePropertyCallable(object value, string parameterName, LythonSourceSpan span)
    {
        return value switch
        {
            null or PyNone => null,
            ICallable callable => callable,
            _ => throw new LythonRuntimeException("TypeError", $"property({parameterName}=...) expects a callable or None.", span)
        };
    }

    private static byte ToByte(object value, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "bytes(iterable) expects integers between 0 and 255.", span);
        }

        if (integer < byte.MinValue || integer > byte.MaxValue)
        {
            throw new LythonRuntimeException("ValueError", "bytes(iterable) expects integers between 0 and 255.", span);
        }

        return (byte)integer;
    }

    private static object Super(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            if (context.ImplicitSuperAnchorType is null || context.ImplicitSuperReceiver is null)
            {
                throw new LythonRuntimeException("TypeError", "zero-argument super() is only supported inside instance methods, classmethods, and property accessors in Lython.", span);
            }

            return context.ImplicitSuperReceiver switch
            {
                PyInstance instance => new PySuper(context.ImplicitSuperAnchorType, instance, instance.Type),
                PyType type => new PySuper(context.ImplicitSuperAnchorType, type, type),
                _ => throw new LythonRuntimeException("TypeError", "zero-argument super() could not resolve the current receiver.", span)
            };
        }

        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects exactly two arguments in Lython.", span);
        }

        if (arguments[0] is not PyType anchorType)
        {
            throw new LythonRuntimeException("TypeError", "super(type, object) expects the first argument to be a class.", span);
        }

        return arguments[1] switch
        {
            PyInstance instance when instance.Type.IsSubtypeOf(anchorType) => new PySuper(anchorType, instance, instance.Type),
            PyType type when type.IsSubtypeOf(anchorType) => new PySuper(anchorType, type, type),
            PyInstance => throw new LythonRuntimeException("TypeError", "super(type, object) expects the instance to be an instance of the given class or its subclass.", span),
            PyType => throw new LythonRuntimeException("TypeError", "super(type, object) expects the class argument to be a subclass of the given class.", span),
            _ => throw new LythonRuntimeException("TypeError", "super(type, object) expects the second argument to be an instance or class.", span)
        };
    }

    private static object List(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "list(iterable) expects one argument.", span);
        }

        var result = new PyList(ToSequence(arguments[0], span), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Tuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return PyTuple.Empty;
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "tuple(iterable) expects one argument.", span);
        }

        var result = new PyTuple(ToSequence(arguments[0], span), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Dict(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            return new PyDict(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects one argument.", span);
        }

        if (arguments[0] is PyDict sourceDict)
        {
            var copied = new PyDict(sourceDict, context.MemoryGovernor, span);
            context.ObserveCollectionCount(copied.Count, span);
            return copied;
        }

        var result = new PyDict(context.MemoryGovernor, span);
        foreach (var pair in ToSequence(arguments[0], span))
        {
            using var enumerator = ToSequence(pair, span).GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var key = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            var value = enumerator.Current;
            if (enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            result.SetItem(ValidateDictionaryKey(key, span, context.MemoryGovernor), value);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Set(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PySet(context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "set(iterable) expects one argument.", span);
        }

        var result = new PySet(context.MemoryGovernor, span);
        foreach (var item in ToSequence(arguments[0], span))
        {
            result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static object IsInstance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects two arguments.", span);
        }

        return IsInstanceOf(arguments[0], arguments[1], span);
    }

    private static object IsSubclass(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects two arguments.", span);
        }

        if (arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects the first argument to be a class.", span);
        }

        return IsSubclassOf(type, arguments[1], span);
    }

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects a class or tuple of classes.", span);
    }

    private static bool IsSubclassOf(PyType type, object baseSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(baseSpec, candidate => IsSubclassAgainstSingleType(type, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects a class or tuple of classes.", span);
    }

    private static bool TryMatchTypeTuple(object typeSpec, Func<object, bool> predicate, out bool matched)
    {
        if (typeSpec is PyTuple tuple)
        {
            foreach (var candidate in tuple)
            {
                if (!IsSupportedTypeSpecifier(candidate))
                {
                    matched = false;
                    return false;
                }

                if (predicate(candidate))
                {
                    matched = true;
                    return true;
                }
            }

            matched = false;
            return true;
        }

        matched = predicate(typeSpec);
        return matched || IsSupportedTypeSpecifier(typeSpec);
    }

    private static bool IsSupportedTypeSpecifier(object typeSpec)
    {
        return typeSpec switch
        {
            PyType => true,
            PyNamedTupleType => true,
            BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => true,
            PyBuiltinRuntimeType builtinType when IsBuiltinTypeName(builtinType.Name) => true,
            INamedRuntimeCallable namedCallable when IsBuiltinTypeName(namedCallable.Name) => true,
            _ => false
        };
    }

    private static bool IsInstanceAgainstSingleType(object value, object typeSpec)
    {
        return typeSpec switch
        {
            PyType runtimeType => value switch
            {
                PyInstance instance => instance.Type.IsSubtypeOf(runtimeType),
                PyType typeValue => typeValue.MetaType is not null && typeValue.MetaType.IsSubtypeOf(runtimeType),
                _ => false
            },
            PyNamedTupleType namedTupleType => value is PyNamedTupleObject namedTuple && ReferenceEquals(namedTuple.Type, namedTupleType),
            BuiltinCallable builtin => DoesObjectMatchBuiltinType(builtin.Name, value),
            PyBuiltinRuntimeType builtinType => DoesObjectMatchBuiltinType(builtinType.Name, value),
            INamedRuntimeCallable namedCallable => DoesObjectMatchBuiltinType(namedCallable.Name, value),
            _ => false
        };
    }

    private static bool IsSubclassAgainstSingleType(PyType type, object baseSpec)
    {
        return baseSpec switch
        {
            PyType runtimeType => type.IsSubtypeOf(runtimeType),
            _ => false
        };
    }

    private static bool IsBuiltinTypeName(string name)
    {
        return name is
            "bool" or
            "int" or
            "float" or
            "list" or
            "tuple" or
            "dict" or
            "set" or
            "str" or
            "bytes" or
            "pathlib.Path" or
            "pathlib.PurePath" or
            "pathlib.PurePosixPath" or
            "pathlib.PosixPath" or
            "datetime.timedelta" or
            "datetime.date" or
            "datetime.time" or
            "datetime.datetime" or
            "datetime.timezone" or
            "datetime.tzinfo" or
            "statistics.NormalDist" or
            "random.Random";
    }

    private static bool DoesObjectMatchBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "bool" => value is bool,
            "int" => value is BigInteger or int or bool,
            "float" => value is double,
            "list" => value is PyList,
            "tuple" => value is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject,
            "dict" => value is PyDict,
            "set" => value is PySet,
            "str" => value is PyString or string,
            "bytes" => value is PyBytes,
            "pathlib.Path" or "pathlib.PurePath" or "pathlib.PurePosixPath" or "pathlib.PosixPath" => value is PyPath,
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            "datetime.tzinfo" => value is PyTimezone,
            "statistics.NormalDist" => value is StatisticsModule.PyNormalDist,
            "random.Random" => value is RandomModule.PyRandom,
            _ => false
        };
    }

    private static object GetAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "getattr(object, name[, default]) expects two or three arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "getattr(object, name[, default])", span);
        try
        {
            if (PyMemberAccess.TryResolve(arguments[0], name, context, span, out var value))
            {
                return value;
            }
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError" && arguments.Length == 3)
        {
            return arguments[2];
        }

        if (arguments.Length == 3)
        {
            return arguments[2];
        }

        throw PyMemberAccess.CreateMissingMemberError(arguments[0], name, span);
    }

    private static object HasAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "hasattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "hasattr(object, name)", span);
        try
        {
            return PyMemberAccess.TryResolve(arguments[0], name, context, span, out _);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError")
        {
            return false;
        }
    }

    private static object SetAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "setattr(object, name, value) expects three arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "setattr(object, name, value)", span);
        if (!PyMemberAccess.TryAssign(arguments[0], name, arguments[2], context, span))
        {
            throw new LythonRuntimeException("AttributeError", $"Object has no writable attribute '{name}'.", span);
        }

        return PyNone.Instance;
    }

    private static object DelAttr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "delattr(object, name) expects two arguments.", span);
        }

        var name = ExpectAttributeName(arguments[1], "delattr(object, name)", span);
        if (!PyMemberAccess.TryDelete(arguments[0], name, context, span))
        {
            throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{name}'.", span);
        }

        return PyNone.Instance;
    }

    private static object Dir(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "dir() without an object is not supported by Lython.", span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "dir([object]) expects zero or one arguments.", span);
        }

        var names = EnumerateDirNames(arguments[0]);
        if (names is null)
        {
            throw new LythonRuntimeException("TypeError", "dir(object) is not supported for this object.", span);
        }

        var sortedNames = names
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => PyString.FromString(name, context.MemoryGovernor, span));
        return new PyList(sortedNames, context.MemoryGovernor, span);
    }

    private static object Vars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "vars() without an object is not supported by Lython.", span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "vars([object]) expects zero or one arguments.", span);
        }

        var result = new PyDict(context.MemoryGovernor, span);
        switch (arguments[0])
        {
            case PyInstance instance:
                foreach (var pair in instance.EnumerateOwnAttributes())
                {
                    result.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                    context.ObserveCollectionCount(result.Count, span);
                }

                return result;

            case PyType type:
                foreach (var pair in type.EnumerateOwnMembers())
                {
                    result.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                    context.ObserveCollectionCount(result.Count, span);
                }

                return result;

            case PyModule module:
                foreach (var name in module.MemberNames)
                {
                    if (module.TryGetMember(name, out var value))
                    {
                        result.SetItem(PyString.FromString(name, context.MemoryGovernor, span), value);
                        context.ObserveCollectionCount(result.Count, span);
                    }
                }

                return result;

            default:
                throw new LythonRuntimeException("TypeError", "vars(object) expects an object with a Python-shaped attribute dictionary.", span);
        }
    }

    private static string ExpectAttributeName(object value, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var name))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects name to be a string.", span);
        }

        return name.AsString();
    }

    private static List<string>? EnumerateDirNames(object value)
    {
        var names = new List<string>();
        switch (value)
        {
            case PyInstance instance:
                foreach (var pair in instance.EnumerateOwnAttributes())
                {
                    names.Add(pair.Key);
                }

                names.Add("__class__");
                foreach (var name in instance.Type.EnumerateMemberNames())
                {
                    names.Add(name);
                }

                return names;

            case PyType type:
                foreach (var name in BuiltinTypeMemberNames)
                {
                    names.Add(name);
                }

                foreach (var name in type.EnumerateMemberNames())
                {
                    names.Add(name);
                }

                return names;

            case PyModule module:
                foreach (var name in module.MemberNames)
                {
                    names.Add(name);
                }

                return names;

            case PySlice:
                names.Add("start");
                names.Add("stop");
                names.Add("step");
                return names;

            case PyException:
                names.Add("args");
                names.Add("message");
                names.Add("type");
                return names;

            default:
                return null;
        }
    }

    private static readonly string[] BuiltinTypeMemberNames =
    [
        "__base__",
        "__bases__",
        "__class__",
        "__mro__",
        "__name__",
        "__qualname__"
    ];

    private static object Len(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "len(value) expects one argument.", span);
        }

        return arguments[0] switch
        {
            PyString text => new BigInteger(text.Length),
            PyBytes bytes => new BigInteger(bytes.Length),
            string text => new BigInteger(PyString.FromString(text).Length),
            ReFindAllResult matches => new BigInteger(matches.Items.Count),
            PySet set => new BigInteger(set.Count),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            PyDict dict => new BigInteger(dict.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            _ => throw new LythonRuntimeException("TypeError", "Object has no len().", span)
        };
    }

    private static object Sorted(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = new List<object>();
        foreach (var item in ToSequence(arguments[0], span))
        {
            values.Add(item);
        }
        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            if (arguments[2] is not bool reverseFlag)
            {
                throw new LythonRuntimeException("TypeError", "sorted(..., reverse=...) expects a bool.", span);
            }

            reverse = reverseFlag;
        }

        var keyed = new List<SortKeyValue>(values.Count);
        foreach (var item in values)
        {
            keyed.Add(new SortKeyValue(
                item,
                keyCallable is ICallable callable
                    ? callable.Invoke([new CallArgumentValue(null, item)], span, context)
                    : item));
        }

        for (var i = 1; i < keyed.Count; i++)
        {
            var current = keyed[i];
            var j = i - 1;
            while (j >= 0 && CompareSortKeys(keyed[j].Key, current.Key, span, context) > 0)
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
        }
        if (reverse)
        {
            keyed.Reverse();
        }

        var items = new object[keyed.Count];
        for (var i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].Value;
        }

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> SortedAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = await MaterializeSequenceAsync(arguments[0], span).ConfigureAwait(false);
        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            if (arguments[2] is not bool reverseFlag)
            {
                throw new LythonRuntimeException("TypeError", "sorted(..., reverse=...) expects a bool.", span);
            }

            reverse = reverseFlag;
        }

        var keyed = new List<SortKeyValue>(values.Count);
        foreach (var item in values)
        {
            keyed.Add(new SortKeyValue(
                item,
                keyCallable is ICallable callable
                    ? await callable.InvokeAsync([new CallArgumentValue(null, item)], span, context).ConfigureAwait(false)
                    : item));
        }

        for (var i = 1; i < keyed.Count; i++)
        {
            var current = keyed[i];
            var j = i - 1;
            while (j >= 0 && await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) > 0)
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
        }
        if (reverse)
        {
            keyed.Reverse();
        }

        var items = new object[keyed.Count];
        for (var i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].Value;
        }

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static object Min(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "min(iterable) expects one argument.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new LythonRuntimeException("ValueError", "min() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            if (Compare(candidate, best, span) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static object Max(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "max(iterable) expects one argument.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new LythonRuntimeException("ValueError", "max() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            if (Compare(candidate, best, span) > 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        foreach (var item in ToSequence(arguments[0], span))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static void EnsureSummableValue(object value, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out _) || value is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() does not support string or bytes operands.", span);
        }
    }

    private static object DivMod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "divmod(a, b) expects two arguments.", span);
        }

        if (arguments[0] is PyTimedelta leftDelta && arguments[1] is PyTimedelta rightDelta)
        {
            return PyDateTimeOps.DivMod(leftDelta, rightDelta, context, span);
        }

        return new PyTuple(
            [
                EvaluateFloorDivide(arguments[0], arguments[1], span),
                EvaluateModulo(arguments[0], arguments[1], span)
            ],
            context.MemoryGovernor,
            span);
    }

    private static int CompareSortKeys(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            var result = leftKey.Comparer.Invoke(
                [new CallArgumentValue(null, leftKey.Value), new CallArgumentValue(null, rightKey.Value)],
                span,
                context);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign;
        }

        return Compare(left, right, span);
    }

    private static async ValueTask<int> CompareSortKeysAsync(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            var result = await leftKey.Comparer.InvokeAsync(
                    [new CallArgumentValue(null, leftKey.Value), new CallArgumentValue(null, rightKey.Value)],
                    span,
                    context)
                .ConfigureAwait(false);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign;
        }

        return Compare(left, right, span);
    }

    private static async ValueTask<List<object>> MaterializeSequenceAsync(object value, LythonSourceSpan span)
    {
        if (value is PyGeneratorExpression generator)
        {
            return await generator.IterateAsync().ConfigureAwait(false);
        }

        return [.. ToSequence(value, span)];
    }

    private static object Range(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        BigInteger start;
        BigInteger stop;
        BigInteger step;

        if (arguments.Length == 1 && arguments[0] is BigInteger stopOnly)
        {
            start = BigInteger.Zero;
            stop = stopOnly;
            step = BigInteger.One;
        }
        else if (arguments.Length == 2 && arguments[0] is BigInteger startArg && arguments[1] is BigInteger stopArg)
        {
            start = startArg;
            stop = stopArg;
            step = BigInteger.One;
        }
        else if (arguments.Length == 3 && arguments[0] is BigInteger startValue && arguments[1] is BigInteger stopValue && arguments[2] is BigInteger stepValue)
        {
            start = startValue;
            stop = stopValue;
            step = stepValue;
        }
        else
        {
            throw new LythonRuntimeException("TypeError", "range(stop), range(start, stop), or range(start, stop, step) expects integer arguments.", span);
        }

        if (step == BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "range() arg 3 must not be zero", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        if (step > BigInteger.Zero)
        {
            for (var current = start; current < stop; current += step)
            {
                result.Add(current);
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        else
        {
            for (var current = start; current > stop; current += step)
            {
                result.Add(current);
                context.ObserveCollectionCount(result.Count, span);
            }
        }

        return result;
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        var index = arguments.Length == 2 && arguments[1] is BigInteger start
            ? start
            : arguments.Length == 1
                ? BigInteger.Zero
                : throw new LythonRuntimeException("TypeError", "enumerate(iterable, start) expects an integer start.", span);

        foreach (var item in ToSequence(arguments[0], span))
        {
            result.Add(CreateTuple(2, i => i == 0 ? index : item, context, span));
            context.ObserveCollectionCount(result.Count, span);
            index += BigInteger.One;
        }

        return result;
    }

    private static object Zip(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, span);
        if (arguments.Length == 0)
        {
            return result;
        }

        var enumerators = new System.Collections.IEnumerator[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            enumerators[i] = ToSequence(arguments[i], span).GetEnumerator();
        }

        try
        {
            while (true)
            {
                var ready = true;
                foreach (var enumerator in enumerators)
                {
                    if (enumerator.MoveNext())
                    {
                        continue;
                    }

                    ready = false;
                    break;
                }

                if (!ready)
                {
                    return result;
                }

                result.Add(CreateTuple(enumerators.Length, i => RuntimeValue(enumerators[i].Current), context, span));
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }

    private static object Iter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 1)
        {
            return arguments[0] is IPyIteratorValue iterator
                ? iterator
                : new PyEnumerableIterator(ToSequence(arguments[0], span));
        }

        if (arguments.Length == 2)
        {
            if (arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "iter(callable, sentinel) expects the first argument to be callable.", span);
            }

            return new PyCallableSentinelIterator(callable, arguments[1], context, span);
        }

        throw new LythonRuntimeException("TypeError", "iter(object[, sentinel]) expects one or two arguments.", span);
    }

    private static object Reversed(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "reversed(sequence) expects one argument.", span);
        }

        var target = arguments[0];
        if (PyMemberAccess.TryResolve(target, "__reversed__", context, span, out var reversedMember))
        {
            if (reversedMember is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "__reversed__ must be callable.", span);
            }

            return RuntimeValue(callable.Invoke([], span, context));
        }

        if (target is IPyIndexableValue indexable)
        {
            return new PyReversedIterator(indexable.Length, indexable.GetIndex);
        }

        if (PyStringOps.TryAsString(target, out var text))
        {
            return new PyReversedIterator(text.Length, text.Index);
        }

        throw new LythonRuntimeException("TypeError", "reversed(sequence) expects a reversible sequence.", span);
    }

    private static object Map(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 2)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects a callable and at least one iterable.", span);
        }

        if (arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects function to be callable.", span);
        }

        var iterables = new IEnumerable<object>[arguments.Length - 1];
        for (var i = 1; i < arguments.Length; i++)
        {
            iterables[i - 1] = ToSequence(arguments[i], span);
        }

        return new PyMapIterator(callable, iterables, context, span);
    }

    private static object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects two arguments.", span);
        }

        var function = arguments[0] switch
        {
            PyNone => null,
            ICallable callable => callable,
            _ => throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects function to be callable or None.", span)
        };

        return new PyFilterIterator(function, ToSequence(arguments[1], span), context, span);
    }

    private static object Slice(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return arguments.Length switch
        {
            1 => new PySlice(PyNone.Instance, arguments[0], PyNone.Instance),
            2 => new PySlice(arguments[0], arguments[1], PyNone.Instance),
            3 => new PySlice(arguments[0], arguments[1], arguments[2]),
            _ => throw new LythonRuntimeException("TypeError", "slice(stop) or slice(start, stop[, step]) expects one to three arguments.", span)
        };
    }

    private static object Next(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        using var enumerator = ToSequence(arguments[0], span).GetEnumerator();
        if (enumerator.MoveNext())
        {
            return RuntimeValue(enumerator.Current);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIterableValue
        {
            private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

            private TextFileHandle(string path, string mode, PyString text, ExecutionContext context, TextEncodingMode encoding)
            {
                Path = path;
                Mode = mode;
                _text = text;
                _context = context;
                _encoding = encoding;
            }

            private PyString _text;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;

            public string Path { get; }

            public string Mode { get; }

            public bool IsClosed { get; private set; }

            public bool IsReadable()
            {
                EnsureOpen();
                return Mode == "r";
            }

            public bool IsWritable()
            {
                EnsureOpen();
                return Mode is "w" or "a";
            }

            public bool IsSeekable()
            {
                EnsureOpen();
                return false;
            }

            public BigInteger Tell()
            {
                EnsureOpen();
                return Mode == "r" ? BigInteger.Zero : new BigInteger(_text.Length);
            }

            public object Flush()
            {
                EnsureOpen();
                return PyNone.Instance;
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
            {
                var text = ReadGovernedHostText(path, context, null);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    var decoded = text.AsString();
                    if (decoded.Length > 0 && decoded[0] == '\uFEFF')
                    {
                        text = PyString.FromString(decoded[1..]);
                    }
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding);
            }

            public static async ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
            {
                var text = await ReadGovernedHostTextAsync(path, context, null).ConfigureAwait(false);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    var decoded = text.AsString();
                    if (decoded.Length > 0 && decoded[0] == '\uFEFF')
                    {
                        text = PyString.FromString(decoded[1..]);
                    }
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding);
            }

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
                => new(path, "w", PyString.Empty, context, encoding);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding = TextEncodingMode.Utf8)
                => new(path, "a", PyString.Empty, context, encoding);

            public object Enter() => this;

            object IPyContextManager.Enter() => Enter();

            ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

            bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
            {
                _ = Exit();
                return false;
            }

            public object Exit()
            {
                if (IsClosed)
                {
                    return false;
                }

                if (Mode == "w")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    var utf8 = PyStringOps.EncodeUtf8(_text);
                    if (_encoding == TextEncodingMode.Utf8Bom)
                    {
                        utf8 = [.. Utf8Bom, .. utf8];
                    }

                    _context.WriteTextUtf8(Path, utf8, null);
                }
                else if (Mode == "a")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    _context.AppendTextUtf8(Path, PyStringOps.EncodeUtf8(_text), null);
                }

                IsClosed = true;
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                if (Mode == "w")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    var utf8 = PyStringOps.EncodeUtf8(_text);
                    if (_encoding == TextEncodingMode.Utf8Bom)
                    {
                        utf8 = [.. Utf8Bom, .. utf8];
                    }

                    await _context.WriteTextUtf8Async(Path, utf8, null).ConfigureAwait(false);
                }
                else if (Mode == "a")
                {
                    _context.ObserveString(_text, null);
                    _context.RegisterHostCall(null);
                    await _context.AppendTextUtf8Async(Path, PyStringOps.EncodeUtf8(_text), null).ConfigureAwait(false);
                }

                IsClosed = true;
                return false;
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            public PyString Read()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                return _text;
            }

            public PyString ReadLine()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var lines = SplitLinesPreservingNewlines(_text);
                return lines.Count == 0 ? PyString.Empty : lines[0];
            }

            public PyList ReadLines()
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var lines = SplitLinesPreservingNewlines(_text);
                var items = new object[lines.Count];
                for (var i = 0; i < lines.Count; i++)
                {
                    items[i] = lines[i];
                }

                return new PyList(items, _context.MemoryGovernor, null);
            }

            public IEnumerable<object> Iterate() => ReadLines();

            public BigInteger Write(PyString text)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                text = PyStringOps.NormalizeNewlines(text);
                _text = _text.Concat(text);
                _context.ObserveString(_text, null);
                return new BigInteger(text.Length);
            }

            public object WriteLines(object value, LythonSourceSpan span)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                foreach (var item in ToSequence(value, span))
                {
                    if (!PyStringOps.TryAsString(item, out var text))
                    {
                        throw new LythonRuntimeException("TypeError", "file.writelines(lines) expects an iterable of strings.", span);
                    }

                    _ = Write(text);
                }

                return PyNone.Instance;
            }

            private void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }

            private static IReadOnlyList<PyString> SplitLinesPreservingNewlines(PyString text)
            {
                var source = text.Utf8Bytes.Span;
                if (source.Length == 0)
                {
                    return Array.Empty<PyString>();
                }

                var lines = new List<PyString>();
                var start = 0;
                for (var i = 0; i < source.Length; i++)
                {
                    if (source[i] == (byte)'\n')
                    {
                        lines.Add(text.SliceByByteRange(start, i + 1));
                        start = i + 1;
                    }
                }

                if (start < source.Length)
                {
                    lines.Add(text.SliceByByteRange(start, source.Length));
                }

                return lines;
            }
        }
    }
}
