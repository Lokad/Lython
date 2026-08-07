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

    private readonly record struct BoundOpenArguments(object[] Values, bool[] Assigned, int Count);

    private static object Open(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => Open(BindPositionalOpenArguments(arguments, span), span, context);

    private static object Open(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode, errors, newline) = ParseOpenArguments(arguments, span, context);
        return mode switch
        {
            "r" => ExecutionContext.TextFileHandle.ForRead(path.AsString(), context, encodingMode, errors, newline),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode, errors, newline),
            "a" => ExecutionContext.TextFileHandle.ForAppend(path.AsString(), context, encodingMode, errors, newline),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static async ValueTask<object> OpenAsync(BoundOpenArguments arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (path, mode, encodingMode, errors, newline) = ParseOpenArguments(arguments, span, context);
        return mode switch
        {
            "r" => await ExecutionContext.TextFileHandle.ForReadAsync(path.AsString(), context, encodingMode, errors, newline).ConfigureAwait(false),
            "w" => ExecutionContext.TextFileHandle.ForWrite(path.AsString(), context, encodingMode, errors, newline),
            "a" => await ExecutionContext.TextFileHandle.ForAppendAsync(path.AsString(), context, encodingMode, errors, newline).ConfigureAwait(false),
            _ => throw new LythonRuntimeException("ValueError", "open() only supports modes 'r', 'w', and 'a'.", span)
        };
    }

    private static (PyString Path, string Mode, TextEncodingMode EncodingMode, TextErrorMode Errors, TextNewlineMode Newline) ParseOpenArguments(BoundOpenArguments boundArguments, LythonSourceSpan span, ExecutionContext context)
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

        return (path, ParseTextOpenMode(mode, "open()", span), encodingMode, errors, newline);
    }

    private static BoundOpenArguments BindPositionalOpenArguments(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length > 8)
        {
            throw new LythonRuntimeException("TypeError", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) received too many positional arguments.", span);
        }

        var bound = new object[8];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new bool[8];
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
        _ = context;
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "bool([value]) expects at most one argument.", span);
        }

        return arguments.Length == 1 && IsTruthy(arguments[0], context, span);
    }

    private static object Int(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
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
            if (!PyNumberOps.TryAsInteger(arguments[1], out var parsedBase) || parsedBase < 0 || parsedBase > 36 || parsedBase == 1)
            {
                throw new LythonRuntimeException("ValueError", "int() base must be >= 2 and <= 36, or 0", span);
            }

            numberBase = (int)parsedBase;
            if (arguments[0] is not PyString and not string and not PyBytes)
            {
                throw new LythonRuntimeException("TypeError", "int() can't convert non-string with explicit base", span);
            }
        }

        try
        {
            return arguments[0] switch
            {
                BigInteger integer => integer,
                double floating => FloatToInteger(floating, "int", span, Math.Truncate),
                PyDecimal decimalValue => new BigInteger(decimal.Truncate(decimalValue.Value)),
                PyString text => ParsePythonIntegerText(text.AsString(), numberBase, span),
                string text => ParsePythonIntegerText(text, numberBase, span),
                PyBytes bytes => ParsePythonIntegerText(System.Text.Encoding.ASCII.GetString(bytes.Bytes), numberBase, span),
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
            return new PyDecimal(decimal.Abs(decimalValue.Value), decimalValue.Exponent);
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
            return modulus < BigInteger.Zero && result != BigInteger.Zero ? result - absModulus : result;
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

    private static object Round(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
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
            PyDecimal decimalValue => RoundDecimal(decimalValue, hasDigits, digits, context.DecimalContext, span),
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
        _ = context;
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
            return FloatToInteger(value, "round", span, static number => Math.Round(number, MidpointRounding.ToEven));
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

    private static BigInteger ParsePythonIntegerText(string text, int numberBase, LythonSourceSpan span)
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
            throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base {numberBase}", span);
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
                throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base {numberBase}", span);
            }

            sawNonZero |= digit != 0;
            result = result * detectedBase + digit;
        }

        if (numberBase == 0 && detectedBase == 10 && value.Length > 1 && value[0] == '0' && sawNonZero)
        {
            throw new LythonRuntimeException("ValueError", "invalid literal for int() with base 0", span);
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

    private static object RoundDecimal(PyDecimal value, bool hasDigits, int digits, PyDecimalContext context, LythonSourceSpan span)
    {
        if (!hasDigits)
        {
            return new BigInteger(decimal.Round(value.Value, 0, MidpointRounding.ToEven));
        }

        if (digits is >= 0 and <= 28)
        {
            return new PyDecimal(PyDecimalOps.Round(value.Value, digits, PyNone.Instance, context, span));
        }

        if (digits > 28)
        {
            return value;
        }

        var factor = DecimalPowerOfTen(checked(-digits), span);
        return new PyDecimal(PyDecimalOps.Round(value.Value / factor, 0, PyNone.Instance, context, span) * factor);
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
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "float([value]) expects at most one argument.", span);
        }

        if (arguments.Length == 0)
        {
            return 0.0;
        }

        if (arguments[0] is PyInstance floatInstance)
        {
            if (!floatInstance.TryGetAttribute("__float__", context, span, out var member) || member is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "float() argument must be a real number", span);
            }

            var converted = callable.Invoke([], span, context);
            if (converted is not double floating)
            {
                throw new LythonRuntimeException("TypeError", "__float__ returned non-float", span);
            }

            return floating;
        }

        try
        {
            return arguments[0] switch
            {
                double floating => floating,
                BigInteger integer => (double)integer,
                PyDecimal decimalValue => (double)decimalValue.Value,
                PyString text => ParsePythonFloatText(text.AsString()),
                string text => ParsePythonFloatText(text),
                bool boolean => boolean ? 1.0 : 0.0,
                _ => throw new LythonRuntimeException("TypeError", "float() does not support this value.", span)
            };
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static double ParsePythonFloatText(string text)
    {
        var normalized = text.Trim().Replace("_", string.Empty, StringComparison.Ordinal);
        return normalized.ToLowerInvariant() switch
        {
            "inf" or "+inf" or "infinity" or "+infinity" => double.PositiveInfinity,
            "-inf" or "-infinity" => double.NegativeInfinity,
            "nan" or "+nan" => double.NaN,
            "-nan" => -double.NaN,
            _ => double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture),
        };
    }

    private static object Bytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "bytes([source[, encoding[, errors]]]) expects zero to three arguments.", span);
        }

        if (arguments.Length >= 2)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "bytes(source, encoding[, errors]) expects source to be a string.", span);
            }

            var encoding = ParseTextEncoding(arguments[1], "bytes()", span);
            var errors = arguments.Length == 3 ? ParseTextErrors(arguments[2], "bytes()", span) : TextErrorMode.Strict;
            return CreateBytes(
                EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span),
                context,
                span);
        }

        return arguments[0] switch
        {
            PyBytes bytes => CreateBytes(bytes.ToArray(), context, span),
            PyString => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            string => throw new LythonRuntimeException("TypeError", "string argument without an encoding", span),
            BigInteger size => CreateZeroBytes(size, context, span),
            int size => CreateZeroBytes(new BigInteger(size), context, span),
            bool size => CreateZeroBytes(size ? BigInteger.One : BigInteger.Zero, context, span),
            _ => CreateBytes(ToByteArray(arguments[0], span), context, span)
        };
    }

    private static PyBytes CreateZeroBytes(BigInteger size, ExecutionContext context, LythonSourceSpan span)
    {
        if (size < 0)
        {
            throw new LythonRuntimeException("ValueError", "negative count", span);
        }

        if (size > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "bytes object is too large", span);
        }

        return CreateBytes(new byte[(int)size], context, span);
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

        var result = new PyList(ToSequence(arguments[0], span, context), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> ListAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "list(iterable) expects one argument.", span);
        }

        var items = await PyIteration.MaterializeAsync(arguments[0], span).ConfigureAwait(false);
        var result = new PyList(items, context.MemoryGovernor, span);
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

        var result = new PyTuple(ToSequence(arguments[0], span, context), context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> TupleAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

        var items = await PyIteration.MaterializeAsync(arguments[0], span).ConfigureAwait(false);
        var result = new PyTuple(items, context.MemoryGovernor, span);
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
        foreach (var pair in ToSequence(arguments[0], span, context))
        {
            using var enumerator = ToSequence(pair, span, context).GetEnumerator();
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

    private static async ValueTask<object> DictAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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
        await foreach (var pair in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            var values = await PyIteration.MaterializeAsync(pair, span).ConfigureAwait(false);
            if (values.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", "dict(iterable_of_pairs) expects key-value pairs.", span);
            }

            result.SetItem(ValidateDictionaryKey(values[0], span, context.MemoryGovernor), values[1]);
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
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            result.Add(ValidateSetItem(item, span, context.MemoryGovernor));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static async ValueTask<object> SetAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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
        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
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

        return IsSubclassOf(arguments[0], arguments[1], span);
    }

    private static bool IsInstanceOf(object value, object typeSpec, LythonSourceSpan span)
    {
        if (TryMatchTypeTuple(typeSpec, candidate => IsInstanceAgainstSingleType(value, candidate), out var matched))
        {
            return matched;
        }

        throw new LythonRuntimeException("TypeError", "isinstance(value, type) expects a class or tuple of classes.", span);
    }

    private static bool IsSubclassOf(object type, object baseSpec, LythonSourceSpan span)
    {
        if (!IsSupportedTypeSpecifier(type))
        {
            throw new LythonRuntimeException("TypeError", "issubclass(type, base) expects the first argument to be a class.", span);
        }

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
            TimeStructTimeType => true,
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
            TimeStructTimeType => value is TimeStructTimeValue,
            BuiltinCallable builtin => DoesObjectMatchBuiltinType(builtin.Name, value),
            PyBuiltinRuntimeType builtinType => DoesObjectMatchBuiltinType(builtinType.Name, value),
            INamedRuntimeCallable namedCallable => DoesObjectMatchBuiltinType(namedCallable.Name, value),
            _ => false
        };
    }

    private static bool IsSubclassAgainstSingleType(object type, object baseSpec)
    {
        if (type is PyType runtimeSubject)
        {
            return baseSpec is PyType runtimeBase && runtimeSubject.IsSubtypeOf(runtimeBase);
        }

        var subjectName = GetBuiltinTypeName(type);
        var baseName = GetBuiltinTypeName(baseSpec);
        if (subjectName is null || baseName is null)
        {
            return false;
        }

        return subjectName == baseName ||
               subjectName == "bool" && baseName == "int" ||
               baseName == "object";
    }

    private static object Dict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.Name is null).ToArray();
        if (positional.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "dict expected at most 1 positional argument", span);
        }

        var result = new PyDict(context.MemoryGovernor, span);
        if (positional.Length == 1)
        {
            UpdateDictionaryFromSource(result, positional[0].Value, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.Name is not null)
            {
                result.SetItem(PyString.FromString(argument.Name, context.MemoryGovernor, span), argument.Value);
            }
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static void UpdateDictionaryFromSource(PyDict target, object source, ExecutionContext context, LythonSourceSpan span)
    {
        if (source is PyDict mapping)
        {
            foreach (var pair in mapping)
            {
                target.SetItem(pair.Key, pair.Value);
            }

            return;
        }

        foreach (var pair in ToSequence(source, span, context))
        {
            var values = ToSequence(pair, span, context).ToArray();
            if (values.Length != 2)
            {
                throw new LythonRuntimeException("ValueError", "dictionary update sequence element has length other than 2", span);
            }

            target.SetItem(ValidateDictionaryKey(values[0], span, context.MemoryGovernor), values[1]);
        }
    }

    private static object UpdateDictionary(
        PyDict target,
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var positional = arguments.Where(argument => argument.Name is null).ToArray();
        if (positional.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "dict.update expected at most 1 positional argument", span);
        }

        target.AttachMemoryGovernor(context.MemoryGovernor, span);
        if (positional.Length == 1)
        {
            UpdateDictionaryFromSource(target, positional[0].Value, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.Name is not null)
            {
                target.SetItem(PyString.FromString(argument.Name, context.MemoryGovernor, span), argument.Value);
            }
        }

        context.ObserveCollectionCount(target.Count, span);
        return PyNone.Instance;
    }

    private static string? GetBuiltinTypeName(object value) => value switch
    {
        PyType type when type.Name is "object" or "type" => type.Name,
        BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        PyBuiltinRuntimeType builtin when IsBuiltinTypeName(builtin.Name) => builtin.Name,
        INamedRuntimeCallable callable when IsBuiltinTypeName(callable.Name) => callable.Name,
        _ => null,
    };

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
            "tuple" => value is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject or TimeStructTimeValue,
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
        if (arguments[0] is PyException && name == "message")
        {
            return false;
        }
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
            return CreateNameList(
                EnumerateCurrentLocalNames(context),
                context,
                span);
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

        return CreateNameList(names, context, span);
    }

    private static object Vars(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 0)
        {
            var locals = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in context.Variables)
            {
                if (!ExecutionState.BuiltinNames.Contains(pair.Key))
                {
                    locals.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                }
            }

            if (context.CurrentExecutableFrame is not null)
            {
                foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
                {
                    locals.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
                }
            }

            context.ObserveCollectionCount(locals.Count, span);
            return locals;
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

            case PyString or string:
                names.AddRange(StringDirNames);
                return names;

            case PyBytes:
                names.AddRange(BytesDirNames);
                return names;

            case PyList:
                names.AddRange(ListDirNames);
                return names;

            case PyDict:
                names.AddRange(DictDirNames);
                return names;

            case PySet:
                names.AddRange(SetDirNames);
                return names;

            case BigInteger or int or double or bool:
                names.Add("__class__");
                return names;

            case BuiltinCallable builtin when IsBuiltinTypeName(builtin.Name):
                names.AddRange(BuiltinTypeMemberNames);
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
            IPySizedValue sized => new BigInteger(sized.Length),
            string text => new BigInteger(PyString.FromString(text).Length),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            PyInstance instance => GetInstanceLength(instance, context, span),
            _ => throw new LythonRuntimeException("TypeError", "Object has no len().", span)
        };
    }

    private static BigInteger GetInstanceLength(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute("__len__", context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"object of type '{instance.Type.Name}' has no len()", span);
        }

        var value = callable.Invoke([], span, context);
        if (!PyNumberOps.TryAsInteger(value, out var length))
        {
            throw new LythonRuntimeException("TypeError", "__len__() should return an integer", span);
        }

        if (length < 0)
        {
            throw new LythonRuntimeException("ValueError", "__len__() should return >= 0", span);
        }

        return length;
    }

    private static object Sorted(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = new List<object>();
        foreach (var item in ToSequence(arguments[0], span, context))
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
            reverse = IsTruthy(arguments[2]);
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
            while (j >= 0 && (reverse
                ? CompareSortKeys(keyed[j].Key, current.Key, span, context) < 0
                : CompareSortKeys(keyed[j].Key, current.Key, span, context) > 0))
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
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
            reverse = IsTruthy(arguments[2]);
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
            while (j >= 0 && (reverse
                ? await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) < 0
                : await CompareSortKeysAsync(keyed[j].Key, current.Key, span, context).ConfigureAwait(false) > 0))
            {
                keyed[j + 1] = keyed[j];
                j--;
            }

            keyed[j + 1] = current;
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

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<object> AnyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
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

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<object> AllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static object MinMax(CallArgumentValue[] arguments, bool isMin, LythonSourceSpan span, ExecutionContext context)
    {
        var (positional, keyCallable, hasDefault, defaultValue) = BindMinMaxArguments(arguments, isMin ? "min" : "max", span);
        using var enumerator = (positional.Count == 1 ? ToSequence(positional[0], span) : positional).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            if (hasDefault)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{(isMin ? "min" : "max")}() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        var bestKey = keyCallable is null
            ? best
            : keyCallable.Invoke([new CallArgumentValue(null, best)], span, context);
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            var candidateKey = keyCallable is null
                ? candidate
                : keyCallable.Invoke([new CallArgumentValue(null, candidate)], span, context);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static PyList CreateNameList(IEnumerable<string> names, ExecutionContext context, LythonSourceSpan span)
        => new(
            names.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name => PyString.FromString(name, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);

    private static IEnumerable<string> EnumerateCurrentLocalNames(ExecutionContext context)
    {
        foreach (var name in context.Variables.Keys)
        {
            if (!ExecutionState.BuiltinNames.Contains(name))
            {
                yield return name;
            }
        }

        if (context.CurrentExecutableFrame is not null)
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
            {
                yield return pair.Key;
            }
        }
    }

    private static readonly string[] StringDirNames = ["capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "index", "isalnum", "isalpha", "isascii", "isdigit", "islower", "isspace", "istitle", "isupper", "join", "lower", "lstrip", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "upper", "zfill"];
    private static readonly string[] BytesDirNames = ["decode", "hex"];
    private static readonly string[] ListDirNames = ["append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"];
    private static readonly string[] DictDirNames = ["clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"];
    private static readonly string[] SetDirNames = ["add", "clear", "copy", "difference", "difference_update", "discard", "intersection", "intersection_update", "isdisjoint", "issubset", "issuperset", "pop", "remove", "symmetric_difference", "symmetric_difference_update", "union", "update"];

    private static async ValueTask<object> MinMaxAsync(CallArgumentValue[] arguments, bool isMin, LythonSourceSpan span, ExecutionContext context)
    {
        var (positional, keyCallable, hasDefault, defaultValue) = BindMinMaxArguments(arguments, isMin ? "min" : "max", span);
        if (positional.Count > 1)
        {
            return await MinMaxValuesAsync(positional, keyCallable, isMin, span, context).ConfigureAwait(false);
        }

        await using var cursor = PyIteration.Cursor.Create(positional[0], span);
        var (hasValue, best) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            if (hasDefault)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{(isMin ? "min" : "max")}() arg is an empty sequence", span);
        }

        var bestKey = keyCallable is null
            ? best
            : await keyCallable.InvokeAsync([new CallArgumentValue(null, best)], span, context).ConfigureAwait(false);
        while (true)
        {
            var (hasCandidate, candidate) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasCandidate)
            {
                break;
            }

            var candidateKey = keyCallable is null
                ? candidate
                : await keyCallable.InvokeAsync([new CallArgumentValue(null, candidate)], span, context).ConfigureAwait(false);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static async ValueTask<object> MinMaxValuesAsync(
        IReadOnlyList<object> values,
        ICallable? keyCallable,
        bool isMin,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var best = values[0];
        var bestKey = keyCallable is null
            ? best
            : await keyCallable.InvokeAsync([new CallArgumentValue(null, best)], span, context).ConfigureAwait(false);
        for (var i = 1; i < values.Count; i++)
        {
            var candidate = values[i];
            var candidateKey = keyCallable is null
                ? candidate
                : await keyCallable.InvokeAsync([new CallArgumentValue(null, candidate)], span, context).ConfigureAwait(false);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static (IReadOnlyList<object> Positional, ICallable? Key, bool HasDefault, object Default) BindMinMaxArguments(
        CallArgumentValue[] arguments,
        string name,
        LythonSourceSpan span)
    {
        var positional = new List<object>();
        ICallable? key = null;
        var sawKey = false;
        var hasDefault = false;
        object defaultValue = PyNone.Instance;
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                positional.Add(argument.Value);
                continue;
            }

            if (argument.Name == "key")
            {
                if (sawKey)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() got multiple values for keyword argument 'key'", span);
                }

                sawKey = true;
                if (!ReferenceEquals(argument.Value, PyNone.Instance) && argument.Value is not ICallable)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() key must be callable or None", span);
                }

                key = argument.Value as ICallable;
                continue;
            }

            if (argument.Name == "default")
            {
                if (hasDefault)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() got multiple values for keyword argument 'default'", span);
                }

                hasDefault = true;
                defaultValue = argument.Value;
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"{name}() got an unexpected keyword argument '{argument.Name}'", span);
        }

        if (positional.Count == 0)
        {
            throw new LythonRuntimeException("TypeError", $"{name} expected at least 1 argument, got 0", span);
        }

        if (positional.Count > 1 && hasDefault)
        {
            throw new LythonRuntimeException("TypeError", $"Cannot specify a default for {name}() with multiple positional arguments", span);
        }

        return (positional, key, hasDefault, defaultValue);
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static async ValueTask<object> SumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
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
                EvaluateModulo(arguments[0], arguments[1], context, span)
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
            return leftKey.CompareTo(rightKey, span, context);
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
            return await generator.MaterializeAsync().ConfigureAwait(false);
        }

        return await PyIteration.MaterializeAsync(value, span).ConfigureAwait(false);
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

        return new PyRange(start, stop, step);
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        var index = arguments.Length == 2 && arguments[1] is BigInteger start
            ? start
            : arguments.Length == 1
                ? BigInteger.Zero
                : throw new LythonRuntimeException("TypeError", "enumerate(iterable, start) expects an integer start.", span);

        return new PyEnumerateIterator(arguments[0], index, span);
    }

    private static object Zip(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        var iterables = new List<object>();
        var strict = false;
        var sawStrict = false;
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                iterables.Add(argument.Value);
                continue;
            }

            if (argument.Name != "strict" || sawStrict)
            {
                throw new LythonRuntimeException("TypeError", $"zip() got an unexpected keyword argument '{argument.Name}'", span);
            }

            sawStrict = true;
            strict = IsTruthy(argument.Value);
        }

        return new PyZipIterator([.. iterables], strict, span);
    }

    private static async ValueTask<object> EnumerateAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
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
            enumerators[i] = ToSequence(arguments[i], span, context).GetEnumerator();
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

    private static async ValueTask<object> ZipAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, span);
        if (arguments.Length == 0)
        {
            return result;
        }

        var cursors = new PyIteration.Cursor[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            cursors[i] = PyIteration.Cursor.Create(arguments[i], span);
        }

        try
        {
            while (true)
            {
                var items = new object[cursors.Length];
                for (var i = 0; i < cursors.Length; i++)
                {
                    var (hasValue, value) = await cursors[i].TryMoveNextAsync().ConfigureAwait(false);
                    if (!hasValue)
                    {
                        return result;
                    }

                    items[i] = RuntimeValue(value);
                }

                result.Add(new PyTuple(items, context.MemoryGovernor, span));
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        finally
        {
            foreach (var cursor in cursors)
            {
                await cursor.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static object Iter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 1)
        {
            return arguments[0] is IPyIteratorValue iterator
                ? iterator
                : new PyEnumerableIterator(arguments[0], span);
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

        var iterables = new object[arguments.Length - 1];
        for (var i = 1; i < arguments.Length; i++)
        {
            _ = PyIteration.Cursor.Create(arguments[i], span);
            iterables[i - 1] = arguments[i];
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

        return new PyFilterIterator(function, arguments[1], context, span);
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

        if (arguments[0] is not IPyIteratorValue iterator)
        {
            throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span);
        }

        if (iterator.TryMoveNext(out var value))
        {
            return RuntimeValue(value);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    private static async ValueTask<object> NextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        var (hasValue, value) = arguments[0] switch
        {
            IPyAsyncIteratorValue asyncIterator => await asyncIterator.TryMoveNextAsync().ConfigureAwait(false),
            IPyIteratorValue iterator => iterator.TryMoveNext(out var item)
                ? (true, item)
                : (false, PyNone.Instance),
            _ => throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span),
        };
        if (hasValue)
        {
            return RuntimeValue(value);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    internal sealed partial class ExecutionContext
    {
        internal sealed class TextFileHandle : IPyAsyncContextManager, IPyIteratorValue
        {
            private TextFileHandle(string path, string mode, PyString text, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline) : this(path, mode, text, context, encoding, errors, newline, default, null) { }

            private TextFileHandle(string path, string mode, PyString text, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors, TextNewlineMode newline, BigInteger appendBasePosition) : this(path, mode, text, context, encoding, errors, newline, appendBasePosition, null) { }

            private TextFileHandle(
                string path,
                string mode,
                PyString text,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline,
                BigInteger appendBasePosition,
                byte[]? appendPrefix)
            {
                Path = path;
                Mode = mode;
                _text = text;
                _context = context;
                _encoding = encoding;
                _errors = errors;
                _newline = newline;
                _appendBasePosition = appendBasePosition;
                _appendPrefix = appendPrefix ?? [];
                _writeBuffer = mode == "r" ? null : new Utf8ValueBuilder(context.MemoryGovernor);
            }

            private readonly PyString _text;
            private readonly Utf8ValueBuilder? _writeBuffer;
            private readonly ExecutionContext _context;
            private readonly TextEncodingMode _encoding;
            private readonly TextErrorMode _errors;
            private readonly TextNewlineMode _newline;
            private readonly BigInteger _appendBasePosition;
            private readonly byte[] _appendPrefix;
            private int _readCursorByte;
            private int _flushedAppendByteLength;
            private long _bufferedRuneLength;

            public string Path { get; }

            public string Mode { get; }

            public string EncodingName => _encoding switch
            {
                TextEncodingMode.Utf8Bom => "utf-8-sig",
                TextEncodingMode.Latin1 => "iso8859-1",
                _ => "utf-8"
            };

            public string ErrorsName => _errors switch
            {
                TextErrorMode.Ignore => "ignore",
                TextErrorMode.Replace => "replace",
                TextErrorMode.BackslashReplace => "backslashreplace",
                _ => "strict"
            };

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
                return Mode switch
                {
                    "r" => new BigInteger(_readCursorByte),
                    "w" => EncodedTextLength(),
                    "a" => _appendBasePosition + EncodedTextLength(),
                    _ => BigInteger.Zero
                };
            }

            public object Flush()
            {
                EnsureOpen();
                FlushCore(null);
                return PyNone.Instance;
            }

            public async ValueTask<object> FlushAsync()
            {
                EnsureOpen();
                await FlushCoreAsync(null).ConfigureAwait(false);
                return PyNone.Instance;
            }

            public object Seek(LythonSourceSpan span)
                => throw new LythonRuntimeException("NotImplementedError", "file.seek(...) is not supported by Lython text handles.", span);

            public static TextFileHandle ForRead(string path, ExecutionContext context)
                => ForRead(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForRead(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForRead(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForRead(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                if (encoding == TextEncodingMode.Latin1)
                {
                    var latin1Text = DecodeText(
                        ReadGovernedHostBytes(path, context, null),
                        encoding,
                        context,
                        null,
                        errors,
                        newline);
                    context.ObserveString(latin1Text, null);
                    return new TextFileHandle(path, "r", latin1Text, context, encoding, errors, newline);
                }

                var text = ReadGovernedHostText(path, context, null, errors, newline);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    text = StripUtf8Bom(text, encoding);
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding, errors, newline);
            }

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context)
                => ForReadAsync(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForReadAsync(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForReadAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForReadAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static async ValueTask<TextFileHandle> ForReadAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                if (encoding == TextEncodingMode.Latin1)
                {
                    var latin1Text = DecodeText(
                        await ReadGovernedHostBytesAsync(path, context, null).ConfigureAwait(false),
                        encoding,
                        context,
                        null,
                        errors,
                        newline);
                    context.ObserveString(latin1Text, null);
                    return new TextFileHandle(path, "r", latin1Text, context, encoding, errors, newline);
                }

                var text = await ReadGovernedHostTextAsync(path, context, null, errors, newline).ConfigureAwait(false);
                if (encoding == TextEncodingMode.Utf8Bom)
                {
                    text = StripUtf8Bom(text, encoding);
                }

                context.ObserveString(text, null);
                return new TextFileHandle(path, "r", text, context, encoding, errors, newline);
            }

            public static TextFileHandle ForWrite(string path, ExecutionContext context)
                => ForWrite(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForWrite(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForWrite(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForWrite(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
                => new(path, "w", PyString.Empty, context, encoding, errors, newline);

            public static TextFileHandle ForAppend(string path, ExecutionContext context)
                => ForAppend(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForAppend(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForAppend(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static TextFileHandle ForAppend(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var stat = context.HostStat(path, null);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                var appendPrefix = encoding == TextEncodingMode.Latin1 && stat is { Exists: true, IsFile: true }
                    ? ReadGovernedHostBytes(path, context, null).ToArray()
                    : [];
                return new TextFileHandle(path, "a", PyString.Empty, context, encoding, errors, newline, appendBasePosition, appendPrefix);
            }

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context)
                => ForAppendAsync(path, context, TextEncodingMode.Utf8, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context, TextEncodingMode encoding)
                => ForAppendAsync(path, context, encoding, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

            public static ValueTask<TextFileHandle> ForAppendAsync(string path, ExecutionContext context, TextEncodingMode encoding, TextErrorMode errors)
                => ForAppendAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal);

            public static async ValueTask<TextFileHandle> ForAppendAsync(
                string path,
                ExecutionContext context,
                TextEncodingMode encoding,
                TextErrorMode errors,
                TextNewlineMode newline)
            {
                var stat = await context.HostStatAsync(path, null).ConfigureAwait(false);
                var appendBasePosition = stat is { Exists: true, IsFile: true }
                    ? stat.Size
                    : BigInteger.Zero;
                var appendPrefix = encoding == TextEncodingMode.Latin1 && stat is { Exists: true, IsFile: true }
                    ? (await ReadGovernedHostBytesAsync(path, context, null).ConfigureAwait(false)).ToArray()
                    : [];
                return new TextFileHandle(path, "a", PyString.Empty, context, encoding, errors, newline, appendBasePosition, appendPrefix);
            }

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

                FlushCore(null);
                IsClosed = true;
                _writeBuffer?.Release();
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                if (IsClosed)
                {
                    return false;
                }

                await FlushCoreAsync(null).ConfigureAwait(false);
                IsClosed = true;
                _writeBuffer?.Release();
                return false;
            }

            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                _ = await ExitAsync().ConfigureAwait(false);
                return false;
            }

            public PyString Read()
                => Read(-1);

            public PyString Read(int size)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                if (_readCursorByte >= _text.Utf8Bytes.Length)
                {
                    return PyString.Empty;
                }

                var end = size < 0
                    ? _text.Utf8Bytes.Length
                    : ByteOffsetAfterRunes(_readCursorByte, size);
                var result = _text.SliceByByteRange(_readCursorByte, end);
                _readCursorByte = end;
                return result;
            }

            public PyString ReadLine()
                => ReadLine(-1);

            public PyString ReadLine(int size)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var source = _text.Utf8Bytes.Span;
                if (_readCursorByte >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = FindLineEndByte(source, _readCursorByte);
                if (size >= 0)
                {
                    end = Math.Min(end, ByteOffsetAfterRunes(_readCursorByte, size));
                }

                var line = _text.SliceByByteRange(_readCursorByte, end);
                _readCursorByte = end;
                return line;
            }

            public PyList ReadLines()
                => ReadLines(-1);

            public PyList ReadLines(int hint)
            {
                EnsureOpen();
                if (Mode != "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for reading", null);
                }

                var items = new List<object>();
                var totalBytes = 0;
                while (true)
                {
                    var line = ReadLine();
                    if (line.Length == 0)
                    {
                        break;
                    }

                    items.Add(line);
                    totalBytes += line.Utf8Bytes.Length;
                    if (hint > 0 && totalBytes > hint)
                    {
                        break;
                    }
                }

                return new PyList(items, _context.MemoryGovernor, null);
            }

            public IEnumerable<object> Iterate()
            {
                while (TryMoveNext(out var value))
                {
                    yield return value;
                }
            }

            public bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var line = ReadLine();
                if (line.Length == 0)
                {
                    value = PyNone.Instance;
                    return false;
                }

                value = line;
                return true;
            }

            public BigInteger Write(PyString text)
            {
                EnsureOpen();
                if (Mode == "r")
                {
                    throw new LythonRuntimeException("ValueError", "file is not open for writing", null);
                }

                var inputLength = text.Length;
                if (_encoding == TextEncodingMode.Latin1)
                {
                    text = DecodeText(
                        EncodeText(
                            text,
                            _encoding,
                            _errors,
                            TextNewlineMode.PreserveUniversal,
                            _context,
                            null),
                        _encoding,
                        _context,
                        null,
                        TextErrorMode.Strict,
                        TextNewlineMode.PreserveUniversal);
                }

                var bufferedRuneLength = _bufferedRuneLength + text.Length;
                if (_context.Limits.MaxStringLength is { } maximumLength && bufferedRuneLength > maximumLength)
                {
                    throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximumLength})", null);
                }

                _writeBuffer.RequireNotNull().Append(text);
                _bufferedRuneLength = bufferedRuneLength;
                return new BigInteger(inputLength);
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

            private int ByteOffsetAfterRunes(int startByte, int runeCount)
                => _text.GetByteIndexAfterRunes(startByte, runeCount);

            private int FindLineEndByte(ReadOnlySpan<byte> source, int startByte)
            {
                for (var i = startByte; i < source.Length; i++)
                {
                    if (source[i] == (byte)'\n' &&
                        _newline is TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                    {
                        return i + 1;
                    }

                    if (source[i] != (byte)'\r')
                    {
                        continue;
                    }

                    if (_newline is TextNewlineMode.PreserveCarriageReturn)
                    {
                        return i + 1;
                    }

                    if (_newline is TextNewlineMode.PreserveCarriageReturnLineFeed)
                    {
                        if (i + 1 < source.Length && source[i + 1] == (byte)'\n')
                        {
                            return i + 2;
                        }

                        continue;
                    }

                    if (_newline == TextNewlineMode.PreserveUniversal)
                    {
                        return i + 1 < source.Length && source[i + 1] == (byte)'\n'
                            ? i + 2
                            : i + 1;
                    }
                }

                return source.Length;
            }

            private void FlushCore(LythonSourceSpan? span)
            {
                if (Mode == "r")
                {
                    return;
                }

                if (Mode == "w")
                {
                    var text = BufferedText();
                    _context.ObserveString(text, span);
                    var payload = EncodeBufferedText(text, span);
                    _context.RegisterHostCall(span);
                    WriteEncodedPayload(payload, span);
                    return;
                }

                FlushAppendCore(span);
            }

            private async ValueTask FlushCoreAsync(LythonSourceSpan? span)
            {
                if (Mode == "r")
                {
                    return;
                }

                if (Mode == "w")
                {
                    var text = BufferedText();
                    _context.ObserveString(text, span);
                    var payload = EncodeBufferedText(text, span);
                    _context.RegisterHostCall(span);
                    await WriteEncodedPayloadAsync(payload, span).ConfigureAwait(false);
                    return;
                }

                await FlushAppendCoreAsync(span).ConfigureAwait(false);
            }

            private void FlushAppendCore(LythonSourceSpan? span)
            {
                var buffer = _writeBuffer.RequireNotNull();
                if (_flushedAppendByteLength >= buffer.Length)
                {
                    return;
                }

                var suffix = PyString.FromUtf8(
                    buffer.WrittenMemory[_flushedAppendByteLength..],
                    _context.MemoryGovernor,
                    span);
                _context.ObserveString(suffix, span);
                if (_encoding == TextEncodingMode.Latin1)
                {
                    var payload = EncodeLatin1AppendPayload(BufferedText(), span);
                    _context.RegisterHostCall(span);
                    _context.WriteHostBytes(Path, payload, span);
                    _flushedAppendByteLength = buffer.Length;
                    return;
                }

                _context.RegisterHostCall(span);
                _context.AppendTextUtf8(Path, EncodeAppendText(suffix, span), span);
                _flushedAppendByteLength = buffer.Length;
            }

            private async ValueTask FlushAppendCoreAsync(LythonSourceSpan? span)
            {
                var buffer = _writeBuffer.RequireNotNull();
                if (_flushedAppendByteLength >= buffer.Length)
                {
                    return;
                }

                var suffix = PyString.FromUtf8(
                    buffer.WrittenMemory[_flushedAppendByteLength..],
                    _context.MemoryGovernor,
                    span);
                _context.ObserveString(suffix, span);
                if (_encoding == TextEncodingMode.Latin1)
                {
                    var payload = EncodeLatin1AppendPayload(BufferedText(), span);
                    _context.RegisterHostCall(span);
                    await _context.WriteHostBytesAsync(Path, payload, span).ConfigureAwait(false);
                    _flushedAppendByteLength = buffer.Length;
                    return;
                }

                _context.RegisterHostCall(span);
                await _context.AppendTextUtf8Async(Path, EncodeAppendText(suffix, span), span).ConfigureAwait(false);
                _flushedAppendByteLength = buffer.Length;
            }

            private PyString BufferedText()
                => _writeBuffer.RequireNotNull().ToPyString();

            private byte[] EncodeBufferedText(PyString text, LythonSourceSpan? span)
                => EncodeText(text, _encoding, _errors, _newline, _context, span);

            private byte[] EncodeAppendText(PyString suffix, LythonSourceSpan? span)
                => EncodeText(suffix, TextEncodingMode.Utf8, _errors, _newline, _context, span);

            private BigInteger EncodedTextLength()
                => _encoding switch
                {
                    TextEncodingMode.Latin1 => new BigInteger(_bufferedRuneLength),
                    TextEncodingMode.Utf8Bom => new BigInteger(_writeBuffer.RequireNotNull().Length + 3L),
                    _ => new BigInteger(_writeBuffer.RequireNotNull().Length)
                };

            private byte[] EncodeLatin1AppendPayload(PyString text, LythonSourceSpan? span)
            {
                var appended = EncodeBufferedText(text, span);
                var length = checked(_appendPrefix.Length + appended.Length);
                _context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
                var payload = new byte[length];
                _appendPrefix.CopyTo(payload, 0);
                appended.CopyTo(payload, _appendPrefix.Length);
                return payload;
            }

            private void WriteEncodedPayload(ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            {
                if (_encoding == TextEncodingMode.Latin1)
                {
                    _context.WriteHostBytes(Path, payload, span);
                }
                else
                {
                    _context.WriteTextUtf8(Path, payload, span);
                }
            }

            private ValueTask WriteEncodedPayloadAsync(ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
                => _encoding == TextEncodingMode.Latin1
                    ? _context.WriteHostBytesAsync(Path, payload, span)
                    : _context.WriteTextUtf8Async(Path, payload, span);

            private void EnsureOpen()
            {
                if (IsClosed)
                {
                    throw new LythonRuntimeException("ValueError", "I/O operation on closed file", null);
                }
            }
        }
    }
}
