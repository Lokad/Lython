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
        Latin1,
    }

    private readonly record struct BoundOpenArguments(object[] Values, ArgumentPresence Assigned, int Count)
    {
        public static BoundOpenArguments From(BoundCallArguments bound)
        {
            var count = bound.Assigned.Length;
            while (count > 0 && !bound.Assigned[count - 1])
            {
                count--;
            }

            return new BoundOpenArguments(bound.Values, bound.Assigned, count);
        }
    }

    internal enum TextFileOperation
    {
        Read,
        Write,
        Append,
    }

    private readonly record struct TextOpenArguments(
        PyString Path,
        TextFileOperation Operation,
        TextEncodingMode EncodingMode,
        TextErrorMode Errors,
        TextNewlineMode Newline);

    private static object Open(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => Open(BindPositionalOpenArguments(arguments, span), span, context);

    private static object Open(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, operation, encodingMode, errors, newline) = ParseOpenArguments(arguments, span, context);
        return operation switch
        {
            TextFileOperation.Read => ExecutionContext.TextFileHandle.ForRead(path.AsString(), context, encodingMode, errors, newline),
            TextFileOperation.Write => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode, errors, newline),
            TextFileOperation.Append => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode, errors, newline),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown text file operation."),
        };
    }

    private static async ValueTask<object> OpenAsync(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, operation, encodingMode, errors, newline) = ParseOpenArguments(arguments, span, context);
        return operation switch
        {
            TextFileOperation.Read => await ExecutionContext.TextFileHandle.ForReadAsync(path.AsString(), context, encodingMode, errors, newline).ConfigureAwait(false),
            TextFileOperation.Write => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode, errors, newline),
            TextFileOperation.Append => await ExecutionContext.TextFileHandle.ForAppendAsync(path.AsString(), context, encodingMode, errors, newline).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown text file operation."),
        };
    }

    private static TextOpenArguments ParseOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span, ExecutionContext context)
    {
        var arguments = boundArguments.Values;
        if (boundArguments.Count is < 1 or > 8)
        {
            throw new LythonRuntimeException("TypeError", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) expects a string file/path plus supported text-mode options.", span);
        }

        var path = CoercePathLike(arguments[0], context, span, "open()");

        var mode = boundArguments.Assigned[1]
            ? arguments[1] switch
            {
                PyString text => text,
                _ => throw new LythonRuntimeException("TypeError", "open(file/path, mode) expects mode to be a string.", span)
            }
            : PyString.FromString("r");

        if (boundArguments.Count >= 3)
        {
            ValidateTextBuffering(arguments[2], "open()", span);
        }

        var encodingMode = boundArguments.Count >= 4
            ? ParseTextEncoding(arguments[3], "open()", span)
            : TextEncodingMode.Utf8;
        var errors = boundArguments.Count >= 5
            ? ParseTextErrors(arguments[4], "open()", span)
            : TextErrorMode.Strict;
        var newline = boundArguments.Count >= 6
            ? ParseTextNewline(arguments[5], "open()", span)
            : TextNewlineMode.TranslateUniversal;

        if (boundArguments.Count >= 7)
        {
            ValidateCloseFd(arguments[6], "open()", span);
        }

        if (boundArguments.Count == 8)
        {
            ValidateOpener(arguments[7], "open()", span);
        }

        return new TextOpenArguments(path, ParseTextOpenMode(mode, "open()", span), encodingMode, errors, newline);
    }

    private static BoundOpenArguments BindPositionalOpenArguments(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 8)
        {
            throw new LythonRuntimeException("TypeError", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) received too many positional arguments.", span);
        }

        var bound = new object[8];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new ArgumentPresence(8);
        for (var i = 0; i < arguments.Length; i++)
        {
            bound[i] = arguments[i];
            assigned[i] = true;
        }

        return new BoundOpenArguments(bound, assigned, arguments.Length);
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

    private static object Str(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return PyString.Empty;
        }

        if (arguments.Length == 1)
        {
            return PyRendering.ToInterpolatedPyString(arguments[0], new PyRenderingContext(context));
        }

        if (arguments.Length > 3 || arguments[0] is not PyBytes bytes)
        {
            throw new LythonRuntimeException("TypeError", "str(object='', encoding='utf-8', errors='strict') expects bytes when encoding or errors are provided.", span);
        }

        var encoding = ParseTextEncoding(arguments[1], "str()", span);
        var errors = arguments.Length == 3
            ? ParseTextErrors(arguments[2], "str()", span)
            : TextErrorMode.Strict;
        return DecodeText(bytes.ToArray(), encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
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
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "bool([value]) expects at most one argument.", span);
        }

        return arguments.Length == 1 && IsTruthy(arguments[0], context, span);
    }

    private static object Int(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "int([x[, base]]) expects at most two arguments.", span);
        }

        if (arguments.Length == 0)
        {
            return BigInteger.Zero;
        }

        if (arguments.Length == 1 && arguments[0] is PyInstance intInstance)
        {
            return InvokeIntegerConversion(intInstance, "__int__", context, span);
        }

        var numberBase = 10;
        if (arguments.Length == 2)
        {
            var requestedBase = CoerceIndexProtocol(arguments[1], context, span) switch
            {
                BigInteger big => big,
                int small => new BigInteger(small),
                bool flag => flag ? BigInteger.One : BigInteger.Zero,
                _ => throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(arguments[1], context) + "' object cannot be interpreted as an integer", span),
            };

            if (requestedBase < 0 || requestedBase > 36 || requestedBase == 1)
            {
                throw new LythonRuntimeException("ValueError", "int() base must be >= 2 and <= 36, or 0", span);
            }

            numberBase = (int)requestedBase;
            if (arguments[0] is not PyString and not PyBytes)
            {
                throw new LythonRuntimeException("TypeError", "int() can't convert non-string with explicit base", span);
            }
        }

        try
        {
            return arguments[0] switch
            {
                BigInteger integer => integer,
                double floating => OwnHeapInteger(FloatToInteger(floating, "int", span, Math.Truncate), context.MemoryGovernor, span),
                PyDecimal decimalValue => OwnHeapInteger(new BigInteger(decimal.Truncate(decimalValue.Value)), context.MemoryGovernor, span),
                PyString text => OwnHeapInteger(ParsePythonIntegerText(text.AsString(), numberBase, span, PyRendering.ToReprPyString(text, new PyRenderingContext(context)).AsString()), context.MemoryGovernor, span),
                PyBytes bytes => OwnHeapInteger(ParsePythonIntegerText(System.Text.Encoding.ASCII.GetString(bytes.Bytes), numberBase, span, PyRendering.ToReprPyString(bytes, new PyRenderingContext(context)).AsString()), context.MemoryGovernor, span),
                bool boolean => boolean ? BigInteger.One : BigInteger.Zero,
                _ => throw new LythonRuntimeException("TypeError", "int() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object Abs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "abs(x) expects one argument.", span);
        }

        if (arguments[0] is PyInstance absInstance &&
            TryInvokeUnarySpecialMethod(absInstance, "__abs__", context, span, out var absolute))
        {
            return absolute;
        }

        if (arguments[0] is PyDecimal decimalValue)
        {
            return OwnDecimalValue(new PyDecimal(decimal.Abs(decimalValue.Value), decimalValue.Exponent), context, span);
        }

        if (!PyNumberOps.TryAsNumber(arguments[0], out var number))
        {
            throw new LythonRuntimeException("TypeError", "abs(x) expects a numeric value.", span);
        }

        return number.IsFloat ? Math.Abs(number.Floating) : OwnHeapInteger(BigInteger.Abs(number.Integer), context.MemoryGovernor, span);
    }

    private static object Pow(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "pow(base, exp[, mod]) expects two or three arguments.", span);
        }

        if (arguments.Length == 3 && arguments[2] is not PyNone)
        {
            var integerBase = RuntimeArgumentValidation.ExpectInteger(arguments[0], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            var exponent = RuntimeArgumentValidation.ExpectInteger(arguments[1], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            var modulus = RuntimeArgumentValidation.ExpectInteger(arguments[2], "pow(base, exp, mod) expects integer arguments when mod is provided.", span);
            if (modulus == BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "pow() 3rd argument cannot be 0", span);
            }

            if (exponent < BigInteger.Zero)
            {
                integerBase = ModularInverse(integerBase, BigInteger.Abs(modulus), span);
                exponent = -exponent;
            }

            var absModulus = BigInteger.Abs(modulus);
            var normalizedBase = integerBase % absModulus;
            if (normalizedBase < BigInteger.Zero)
            {
                normalizedBase += absModulus;
            }

            var result = BigInteger.ModPow(normalizedBase, exponent, absModulus);
            return OwnHeapInteger(modulus < BigInteger.Zero && result != BigInteger.Zero ? result - absModulus : result, context.MemoryGovernor, span);
        }

        return EvaluatePower(arguments[0], arguments[1], context, span);
    }

    private static BigInteger ModularInverse(BigInteger value, BigInteger modulus, LythonSourceSpan span)
    {
        var oldR = value % modulus;
        var r = modulus;
        var oldS = BigInteger.One;
        var s = BigInteger.Zero;
        while (r != BigInteger.Zero)
        {
            var quotient = oldR / r;
            (oldR, r) = (r, oldR - quotient * r);
            (oldS, s) = (s, oldS - quotient * s);
        }

        if (BigInteger.Abs(oldR) != BigInteger.One)
        {
            throw new LythonRuntimeException("ValueError", "base is not invertible for the given modulus", span);
        }

        var inverse = oldS % modulus;
        return inverse < BigInteger.Zero ? inverse + modulus : inverse;
    }

    private static object Bin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        return FormatIntegerBase(arguments, "bin(number) expects an integer.", "0b", 2, lower: true, span, context.MemoryGovernor);
    }

    private static object Oct(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        return FormatIntegerBase(arguments, "oct(number) expects an integer.", "0o", 8, lower: true, span, context.MemoryGovernor);
    }

    private static object Hex(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        return FormatIntegerBase(arguments, "hex(number) expects an integer.", "0x", 16, lower: true, span, context.MemoryGovernor);
    }

    private static object Chr(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "chr(i) expects one integer argument.", span);
        }

        var codePoint = RuntimeArgumentValidation.ExpectInteger(arguments[0], "chr(i) expects one integer argument.", span);
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
        if (arguments.Length == 1 && arguments[0] is PyBytes bytes && bytes.Length == 1)
        {
            return new BigInteger(bytes.Bytes[0]);
        }

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
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "callable(object) expects one argument.", span);
        }

        return arguments[0] is PyInstance instance
            ? instance.TryGetAttribute("__call__", context, span, out var member) && member is ICallable
            : arguments[0] is ICallable;
    }

    private static object Hash(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "hash(object) expects one argument.", span);
        }

        if (arguments[0] is PyInstance instance &&
            instance.TryGetAttribute("__hash__", context, span, out var hashMember) &&
            hashMember is ICallable hashCallable)
        {
            var hashValue = hashCallable.Invoke([], span, context);
            if (!PyNumberOps.TryAsInteger(hashValue, out var integerHash))
            {
                throw new LythonRuntimeException("TypeError", "__hash__ method should return an integer", span);
            }

            return integerHash;
        }

        try
        {
            return new BigInteger(PyValueComparer.Instance.GetHashCode(arguments[0]));
        }
        catch (InvalidOperationException)
        {
            throw RuntimeErrors.UnhashableType(arguments[0], span);
        }
    }

    private static int ToInt32(BigInteger value, string owner, LythonSourceSpan span)
    {
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", $"{owner} integer argument is too large.", span);
        }

        return (int)value;
    }

    private static object FormatIntegerBase(object[] arguments, string message, string prefix, int radix, bool lower, LythonSourceSpan span, MemoryGovernor governor)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        var integer = RuntimeArgumentValidation.ExpectInteger(arguments[0], message, span);
        var sign = integer < BigInteger.Zero ? "-" : string.Empty;
        var digits = ToUnsignedBaseString(BigInteger.Abs(integer), radix, upper: !lower);
        return PyString.FromString(sign + prefix + digits, governor, span);
    }

    private static BigInteger ParsePythonIntegerText(string text, int numberBase, LythonSourceSpan span, string literal)
    {
        var value = text.Trim();
        var negative = false;
        if (value.StartsWith('+') || value.StartsWith('-'))
        {
            negative = value[0] == '-';
            value = value[1..];
        }

        var detectedBase = numberBase;
        if (value.Length >= 2 && value[0] == '0')
        {
            var prefixBase = char.ToLowerInvariant(value[1]) switch
            {
                'b' => 2,
                'o' => 8,
                'x' => 16,
                _ => 0,
            };
            if (prefixBase != 0 && (numberBase == 0 || numberBase == prefixBase))
            {
                detectedBase = prefixBase;
                value = value[2..];
                if (value.StartsWith('_'))
                {
                    value = value[1..];
                }
            }
        }

        detectedBase = detectedBase == 0 ? 10 : detectedBase;
        if (value.Length == 0 || value.StartsWith('_') || value.EndsWith('_') || value.Contains("__", StringComparison.Ordinal))
        {
            throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base {numberBase}: {literal}", span);
        }

        var result = BigInteger.Zero;
        var sawNonZero = false;
        foreach (var character in value)
        {
            if (character == '_')
            {
                continue;
            }

            var digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= 'a' and <= 'z' => character - 'a' + 10,
                >= 'A' and <= 'Z' => character - 'A' + 10,
                _ => -1,
            };
            if (digit < 0 || digit >= detectedBase)
            {
                throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base {numberBase}: {literal}", span);
            }

            sawNonZero |= digit != 0;
            result = result * detectedBase + digit;
        }

        if (numberBase == 0 && detectedBase == 10 && value.Length > 1 && value[0] == '0' && sawNonZero)
        {
            throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base 0: {literal}", span);
        }

        return negative ? -result : result;
    }

    private static BigInteger InvokeIntegerConversion(
        PyInstance instance,
        string methodName,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute(methodName, context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "int() argument must be a number or a string", span);
        }

        var converted = callable.Invoke([], span, context);
        if (!PyNumberOps.TryAsInteger(converted, out var integer))
        {
            throw new LythonRuntimeException("TypeError", methodName + " returned non-int", span);
        }

        return integer;
    }

    private static BigInteger FloatToInteger(
        double value,
        string owner,
        LythonSourceSpan span,
        Func<double, double> transform)
    {
        if (double.IsNaN(value))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} cannot convert float NaN to integer.", span);
        }

        if (double.IsInfinity(value))
        {
            throw new LythonRuntimeException("OverflowError", $"{owner} cannot convert float infinity to integer.", span);
        }

        try
        {
            return new BigInteger(transform(value));
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

}
