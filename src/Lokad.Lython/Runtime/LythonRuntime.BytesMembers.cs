using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class BytesMembers
    {
        public static bool TryGetMember(PyBytes bytes, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fromhex" => new BuiltinTypeMethod("bytes", "fromhex", bindsOwner: true, BytesFromHex),
                "maketrans" => BuiltinTypeMethod.BytesMaketrans,
                "translate" => new RawBoundCallable((arguments, span, context) => TranslateBytes(bytes, arguments, span, context)) { BoundName = "bytes.translate", BoundReceiver = bytes },
                "decode" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.decode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    return DecodeText(bytes.ToArray(), encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
                }, "bytes.decode", ["encoding", "errors"], 0),
                "hex" => new RawBoundCallable((arguments, span, context) => HexEncode(bytes, arguments, span, context)) { BoundName = "bytes.hex", BoundReceiver = bytes },
                "count" => new RawBoundCallable((arguments, span, context) => SearchBytes(bytes, "count", arguments, span, context)) { BoundName = "bytes.count", BoundReceiver = bytes },
                "find" => new RawBoundCallable((arguments, span, context) => SearchBytes(bytes, "find", arguments, span, context)) { BoundName = "bytes.find", BoundReceiver = bytes },
                "index" => new RawBoundCallable((arguments, span, context) => SearchBytes(bytes, "index", arguments, span, context)) { BoundName = "bytes.index", BoundReceiver = bytes },
                "rfind" => new RawBoundCallable((arguments, span, context) => SearchBytes(bytes, "rfind", arguments, span, context)) { BoundName = "bytes.rfind", BoundReceiver = bytes },
                "rindex" => new RawBoundCallable((arguments, span, context) => SearchBytes(bytes, "rindex", arguments, span, context)) { BoundName = "bytes.rindex", BoundReceiver = bytes },
                "startswith" => new RawBoundCallable((arguments, span, context) => StartsOrEndsWithBytes(bytes, "startswith", arguments, span, context, isStart: true)) { BoundName = "bytes.startswith", BoundReceiver = bytes },
                "endswith" => new RawBoundCallable((arguments, span, context) => StartsOrEndsWithBytes(bytes, "endswith", arguments, span, context, isStart: false)) { BoundName = "bytes.endswith", BoundReceiver = bytes },
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static object TranslateBytes(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? table = null;
        object? delete = null;
        var positionals = 0;
        var keywords = 0;
        foreach (var argument in arguments)
        {
            if (!argument.IsKeyword)
            {
                positionals++;
                if (positionals == 1)
                {
                    table = argument.Value;
                }
                else if (positionals == 2)
                {
                    delete = argument.Value;
                }

                continue;
            }

            keywords++;
            if (argument.KeywordName == "delete")
            {
                delete = argument.Value;
            }
        }

        if (positionals == 0)
        {
            throw new LythonRuntimeException("TypeError", "translate() takes at least 1 positional argument (0 given)", span);
        }

        if (positionals + keywords > 2)
        {
            throw new LythonRuntimeException("TypeError", "translate() takes at most 2 arguments (" + (positionals + keywords) + " given)", span);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword && argument.KeywordName != "delete")
            {
                throw new LythonRuntimeException("TypeError", "translate() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
            }
        }

        if (table is not PyBytes tableBytes || tableBytes.Length != 256)
        {
            throw new LythonRuntimeException("ValueError", "translation table must be 256 characters long", span);
        }

        var mapping = tableBytes.ToArray();
        var discarded = new bool[256];
        if (delete is not null)
        {
            if (delete is not PyBytes deleteBytes)
            {
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(delete, context) + "'", span);
            }

            foreach (var octet in deleteBytes.ToArray())
            {
                discarded[octet] = true;
            }
        }

        var source = value.ToArray();
        var result = new List<byte>(source.Length);
        foreach (var octet in source)
        {
            if (!discarded[octet])
            {
                result.Add(mapping[octet]);
            }
        }

        return CreateBytes([.. result], context, span);
    }

    private static object HexEncode(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? sep = null;
        object? group = null;
        var hasSepPositional = false;
        var hasGroupPositional = false;
        var positionals = 0;
        var keywords = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                keywords++;
                continue;
            }
            positionals++;
            if (positionals == 1)
            {
                sep = argument.Value;
                hasSepPositional = true;
            }
            else if (positionals == 2)
            {
                group = argument.Value;
                hasGroupPositional = true;
            }
        }
        if (positionals + keywords > 2)
        {
            throw new LythonRuntimeException("TypeError", "hex() takes at most 2 arguments (" + (positionals + keywords) + " given)", span);
        }

        foreach (var argument in arguments)
        {
            if (!argument.IsKeyword)
            {
                continue;
            }
            if (argument.KeywordName == "sep")
            {
                if (hasSepPositional)
                {
                    throw new LythonRuntimeException("TypeError", "argument for hex() given by name ('sep') and position (1)", span);
                }

                sep = argument.Value;
            }
            else if (argument.KeywordName == "bytes_per_sep")
            {
                if (hasGroupPositional)
                {
                    throw new LythonRuntimeException("TypeError", "argument for hex() given by name ('bytes_per_sep') and position (2)", span);
                }

                group = argument.Value;
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "hex() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
            }
        }

        byte? separator = null;
        if (sep is not null)
        {
            separator = ParseHexSeparator(sep, span);
        }

        var perSep = 1;
        if (group is not null)
        {
            perSep = RuntimeArgumentValidation.ParseInt32(group, "bytes_per_sep", "bytes.hex([sep[, bytes_per_sep]])", span);
        }

        if (perSep < 0)
        {
            perSep = perSep == int.MinValue ? int.MaxValue : -perSep;
        }

        return CreateString(RenderHex(value.ToArray(), separator, perSep), context, span);
    }

    private static byte ParseHexSeparator(object sep, LythonSourceSpan span)
    {
        if (sep is PyString sepText)
        {
            var count = 0;
            var code = 0;
            foreach (var rune in sepText.AsString().EnumerateRunes())
            {
                count++;
                code = rune.Value;
            }

            if (count != 1)
            {
                throw new LythonRuntimeException("ValueError", "sep must be length 1.", span);
            }

            if (code > 127)
            {
                throw new LythonRuntimeException("ValueError", "sep must be ASCII.", span);
            }

            return (byte)code;
        }

        if (sep is PyBytes sepBytes)
        {
            if (sepBytes.Length != 1)
            {
                throw new LythonRuntimeException("ValueError", "sep must be length 1.", span);
            }

            var octet = sepBytes.Bytes[0];
            if (octet > 127)
            {
                throw new LythonRuntimeException("ValueError", "sep must be ASCII.", span);
            }

            return octet;
        }

        throw new LythonRuntimeException("TypeError", "hex() expects sep to be str or bytes.", span);
    }

    private static string RenderHex(byte[] source, byte? separator, int perSep)
    {
        const string Digits = "0123456789abcdef";
        var first = source.Length;
        if (separator is not null && perSep > 0 && source.Length > 0)
        {
            var head = source.Length % perSep;
            first = head == 0 ? Math.Min(perSep, source.Length) : head;
        }

        var groups = 1;
        if (first < source.Length)
        {
            groups += (source.Length - first + perSep - 1) / perSep;
        }

        var sepChar = (char)separator.GetValueOrDefault();
        var text = new char[2 * source.Length + groups - 1];
        var at = 0;
        var index = 0;
        for (var g = 0; g < groups; g++)
        {
            if (g > 0)
            {
                text[at++] = sepChar;
            }

            var end = g == 0 ? first : Math.Min(index + perSep, source.Length);
            while (index < end)
            {
                var octet = source[index++];
                text[at++] = Digits[octet >> 4];
                text[at++] = Digits[octet & 15];
            }
        }

        return new string(text);
    }

    private static object SearchBytes(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? needle = null;
        object? startArgument = null;
        object? endArgument = null;
        var positionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "bytes." + methodName + "() takes no keyword arguments", span);
            }

            positionals++;
            if (positionals == 1)
            {
                needle = argument.Value;
            }
            else if (positionals == 2)
            {
                startArgument = argument.Value;
            }
            else if (positionals == 3)
            {
                endArgument = argument.Value;
            }
        }

        if (positionals < 1)
        {
            throw new LythonRuntimeException("TypeError", methodName + " expected at least 1 argument, got 0", span);
        }

        if (positionals > 3)
        {
            throw new LythonRuntimeException("TypeError", methodName + " expected at most 3 arguments, got " + positionals, span);
        }

        var source = value.Bytes;
        int start;
        int end;
        try
        {
            start = NormalizeBytesBound(startArgument, source.Length, 0);
            end = NormalizeBytesBound(endArgument, source.Length, source.Length);
        }
        catch (InvalidOperationException)
        {
            throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
        }

        if (end < start)
        {
            if (methodName == "count")
            {
                return BigInteger.Zero;
            }

            if (methodName is "index" or "rindex")
            {
                throw new LythonRuntimeException("ValueError", "subsection not found", span);
            }

            return new BigInteger(-1);
        }

        var needleBytes = ParseSearchNeedle(needle, span, context);

        if (methodName == "count")
        {
            return new BigInteger(CountBytes(source, needleBytes, start, end));
        }

        var found = methodName is "rfind" or "rindex"
            ? FindLastByte(source, needleBytes, start, end)
            : FindFirstByte(source, needleBytes, start, end);

        if (found < 0 && methodName is "index" or "rindex")
        {
            throw new LythonRuntimeException("ValueError", "subsection not found", span);
        }

        return new BigInteger(found);
    }

    private static byte[] ParseSearchNeedle(object? needle, LythonSourceSpan span, ExecutionContext context)
    {
        if (needle is PyBytes needleBytes)
        {
            return needleBytes.ToArray();
        }

        if (needle is bool flag)
        {
            return [(byte)(flag ? 1 : 0)];
        }

        if (needle is int small)
        {
            if (small < 0 || small > 255)
            {
                throw new LythonRuntimeException("ValueError", "byte must be in range(0, 256)", span);
            }

            return [(byte)small];
        }

        if (needle is BigInteger big)
        {
            if (big < 0 || big > 255)
            {
                throw new LythonRuntimeException("ValueError", "byte must be in range(0, 256)", span);
            }

            return [(byte)big];
        }

        throw new LythonRuntimeException("TypeError", "argument should be integer or bytes-like object, not '" + UnboundTypeMethod.PythonTypeName(needle, context) + "'", span);
    }

    private static int NormalizeBytesBound(object? bound, int length, int defaultValue)
    {
        if (bound is null || ReferenceEquals(bound, PyNone.Instance))
        {
            return defaultValue;
        }

        BigInteger integer;
        if (bound is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (bound is int small)
        {
            integer = new BigInteger(small);
        }
        else if (bound is not BigInteger big)
        {
            throw new InvalidOperationException("slice bounds must be integers or None");
        }
        else
        {
            integer = big;
        }

        if (integer < int.MinValue)
        {
            return 0;
        }

        if (integer > int.MaxValue)
        {
            return length;
        }

        var value = (int)integer;
        if (value < 0)
        {
            value += length;
        }

        if (value < 0)
        {
            return 0;
        }

        return value > length ? length : value;
    }

    private static int FindFirstByte(ReadOnlySpan<byte> source, ReadOnlySpan<byte> needle, int start, int end)
    {
        if (needle.IsEmpty)
        {
            return start;
        }

        for (var i = start; i <= end - needle.Length; i++)
        {
            if (source.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastByte(ReadOnlySpan<byte> source, ReadOnlySpan<byte> needle, int start, int end)
    {
        if (needle.IsEmpty)
        {
            return end;
        }

        for (var i = end - needle.Length; i >= start; i--)
        {
            if (source.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static int CountBytes(ReadOnlySpan<byte> source, ReadOnlySpan<byte> needle, int start, int end)
    {
        if (needle.IsEmpty)
        {
            return end - start + 1;
        }

        var count = 0;
        var index = start;
        while (index <= end - needle.Length)
        {
            if (source.Slice(index, needle.Length).SequenceEqual(needle))
            {
                count++;
                index += needle.Length;
            }
            else
            {
                index++;
            }
        }

        return count;
    }

    private static object StartsOrEndsWithBytes(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool isStart)
    {
        object? prefix = null;
        object? startArgument = null;
        object? endArgument = null;
        var positionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "bytes." + methodName + "() takes no keyword arguments", span);
            }

            positionals++;
            if (positionals == 1)
            {
                prefix = argument.Value;
            }
            else if (positionals == 2)
            {
                startArgument = argument.Value;
            }
            else if (positionals == 3)
            {
                endArgument = argument.Value;
            }
        }

        if (positionals < 1)
        {
            throw new LythonRuntimeException("TypeError", methodName + " expected at least 1 argument, got 0", span);
        }

        if (positionals > 3)
        {
            throw new LythonRuntimeException("TypeError", methodName + " expected at most 3 arguments, got " + positionals, span);
        }

        var source = value.Bytes;
        int start;
        int end;
        try
        {
            start = NormalizeBytesBound(startArgument, source.Length, 0);
            end = NormalizeBytesBound(endArgument, source.Length, source.Length);
        }
        catch (InvalidOperationException)
        {
            throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
        }

        var startBeyondLength = startArgument switch
        {
            BigInteger integer => integer > source.Length,
            int integer => integer > source.Length,
            _ => false,
        };

        if (prefix is PyBytes prefixBytes)
        {
            return !startBeyondLength && (isStart
                ? StartsWithBytes(source, prefixBytes.Bytes, start, end)
                : EndsWithBytes(source, prefixBytes.Bytes, start, end));
        }

        if (prefix is not PyTuple tuple)
        {
            throw new LythonRuntimeException("TypeError", methodName + " first arg must be bytes or a tuple of bytes, not " + UnboundTypeMethod.PythonTypeName(prefix, context), span);
        }

        foreach (var item in tuple)
        {
            if (item is not PyBytes itemBytes)
            {
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(item, context) + "'", span);
            }

            if (!startBeyondLength && (isStart
                ? StartsWithBytes(source, itemBytes.Bytes, start, end)
                : EndsWithBytes(source, itemBytes.Bytes, start, end)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithBytes(ReadOnlySpan<byte> source, ReadOnlySpan<byte> prefix, int start, int end)
    {
        return end - start >= prefix.Length && source.Slice(start, prefix.Length).SequenceEqual(prefix);
    }

    private static bool EndsWithBytes(ReadOnlySpan<byte> source, ReadOnlySpan<byte> suffix, int start, int end)
    {
        return end - start >= suffix.Length && source.Slice(end - suffix.Length, suffix.Length).SequenceEqual(suffix);
    }

    private sealed class RawBoundCallable(
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation) : ICallable, IPyDynamicAttributes, IPyHashableValue, IPyRawBoundCallable
    {
        public string? BoundName { get; init; }

        public object? BoundReceiver { get; init; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return implementation(arguments, span, context);
        }

        // Named shapes expose CPython-style identity like bound builtins:
        // the short __name__, the qualified __qualname__, a None __module__
        // and the bound receiver (anonymous callables stay missing).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (BoundName is not null)
            {
                if (name == "__name__")
                {
                    value = PyString.FromString(ShortMethodName(BoundName));
                    return true;
                }

                if (name == "__qualname__")
                {
                    value = PyString.FromString(BoundName);
                    return true;
                }

                if (name == "__module__")
                {
                    value = PyNone.Instance;
                    return true;
                }

                if (name == "__self__" && BoundReceiver is not null)
                {
                    value = BoundReceiver;
                    return true;
                }
            }

            value = PyNone.Instance;
            return false;
        }

        private static string ShortMethodName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        public int GetPyHashCode() => BoundName is null || BoundReceiver is null
            ? RuntimeHelpers.GetHashCode(this)
            : HashCode.Combine(string.GetHashCode(BoundName, StringComparison.Ordinal), RuntimeHelpers.GetHashCode(BoundReceiver));
    }
}
