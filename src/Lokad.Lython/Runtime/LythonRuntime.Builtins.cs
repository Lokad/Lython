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
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyInstance)
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
            if (arguments.Length == 0 || arguments[0].IsKeyword || arguments[0].Value is not PyType)
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
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
                arguments[2].IsKeyword ||
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
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
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
                arguments[0].IsKeyword ||
                arguments[1].IsKeyword ||
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

}
