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
                "replace" => new RawBoundCallable((arguments, span, context) => ReplaceBytes(bytes, arguments, span, context)) { BoundName = "bytes.replace", BoundReceiver = bytes },
                "isalnum" => BoundCallable.CreateNoArguments(bytes, "bytes.isalnum", static (receiver, _, _) => IsAsciiAlnum(receiver)),
                "isalpha" => BoundCallable.CreateNoArguments(bytes, "bytes.isalpha", static (receiver, _, _) => IsAsciiAlpha(receiver)),
                "isascii" => BoundCallable.CreateNoArguments(bytes, "bytes.isascii", static (receiver, _, _) => IsAscii(receiver)),
                "isdigit" => BoundCallable.CreateNoArguments(bytes, "bytes.isdigit", static (receiver, _, _) => IsAsciiDigit(receiver)),
                "islower" => BoundCallable.CreateNoArguments(bytes, "bytes.islower", static (receiver, _, _) => IsAsciiLower(receiver)),
                "isspace" => BoundCallable.CreateNoArguments(bytes, "bytes.isspace", static (receiver, _, _) => IsAsciiSpace(receiver)),
                "istitle" => BoundCallable.CreateNoArguments(bytes, "bytes.istitle", static (receiver, _, _) => IsAsciiTitle(receiver)),
                "isupper" => BoundCallable.CreateNoArguments(bytes, "bytes.isupper", static (receiver, _, _) => IsAsciiUpper(receiver)),
                "removeprefix" => new RawBoundCallable((arguments, span, context) => RemoveBytesAffix(bytes, "removeprefix", arguments, span, context, isPrefix: true)) { BoundName = "bytes.removeprefix", BoundReceiver = bytes },
                "removesuffix" => new RawBoundCallable((arguments, span, context) => RemoveBytesAffix(bytes, "removesuffix", arguments, span, context, isPrefix: false)) { BoundName = "bytes.removesuffix", BoundReceiver = bytes },
                "capitalize" => BoundCallable.CreateNoArguments(bytes, "bytes.capitalize", static (receiver, span, context) => MapBytesCase(receiver, BytesCaseMode.Capitalize, span, context)),
                "lower" => BoundCallable.CreateNoArguments(bytes, "bytes.lower", static (receiver, span, context) => MapBytesCase(receiver, BytesCaseMode.Lower, span, context)),
                "swapcase" => BoundCallable.CreateNoArguments(bytes, "bytes.swapcase", static (receiver, span, context) => MapBytesCase(receiver, BytesCaseMode.SwapCase, span, context)),
                "title" => BoundCallable.CreateNoArguments(bytes, "bytes.title", static (receiver, span, context) => MapBytesCase(receiver, BytesCaseMode.Title, span, context)),
                "upper" => BoundCallable.CreateNoArguments(bytes, "bytes.upper", static (receiver, span, context) => MapBytesCase(receiver, BytesCaseMode.Upper, span, context)),
                "strip" => new RawBoundCallable((arguments, span, context) => StripBytes(bytes, "strip", arguments, span, context, BytesStripMode.Both)) { BoundName = "bytes.strip", BoundReceiver = bytes },
                "lstrip" => new RawBoundCallable((arguments, span, context) => StripBytes(bytes, "lstrip", arguments, span, context, BytesStripMode.Left)) { BoundName = "bytes.lstrip", BoundReceiver = bytes },
                "rstrip" => new RawBoundCallable((arguments, span, context) => StripBytes(bytes, "rstrip", arguments, span, context, BytesStripMode.Right)) { BoundName = "bytes.rstrip", BoundReceiver = bytes },
                "partition" => new RawBoundCallable((arguments, span, context) => PartitionBytes(bytes, "partition", arguments, span, context, isFirst: true)) { BoundName = "bytes.partition", BoundReceiver = bytes },
                "rpartition" => new RawBoundCallable((arguments, span, context) => PartitionBytes(bytes, "rpartition", arguments, span, context, isFirst: false)) { BoundName = "bytes.rpartition", BoundReceiver = bytes },
                "split" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return SplitBytesWhitespace(bytes, -1, context, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? RuntimeArgumentValidation.ParseInt32(arguments[1], "maxsplit", "bytes.split([sep[, maxsplit]])", span) : -1;
                        return SplitBytesWhitespace(bytes, maxSplit, context, span);
                    }

                    if (arguments.Length is < 1 or > 2 || arguments[0] is not PyBytes separator)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.split([sep[, maxsplit]]) expects zero, one, or two arguments with bytes separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? RuntimeArgumentValidation.ParseInt32(arguments[1], "maxsplit", "bytes.split([sep[, maxsplit]])", span) : -1;
                    try
                    {
                        return SplitBytes(bytes, separator, maxSplit, context, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "bytes.split", ["sep", "maxsplit"], 0),
                "rsplit" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return RSplitBytesWhitespace(bytes, -1, context, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? RuntimeArgumentValidation.ParseInt32(arguments[1], "maxsplit", "bytes.rsplit([sep[, maxsplit]])", span) : -1;
                        return RSplitBytesWhitespace(bytes, maxSplit, context, span);
                    }

                    if (arguments.Length is < 1 or > 2 || arguments[0] is not PyBytes separator)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.rsplit([sep[, maxsplit]]) expects zero, one, or two arguments with bytes separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? RuntimeArgumentValidation.ParseInt32(arguments[1], "maxsplit", "bytes.rsplit([sep[, maxsplit]])", span) : -1;
                    try
                    {
                        return RSplitBytes(bytes, separator, maxSplit, context, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "bytes.rsplit", ["sep", "maxsplit"], 0),
                "splitlines" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.splitlines([keepends]) expects zero or one bool argument.", span);
                    }

                    var keepEnds = arguments.Length == 1 && IsTruthy(arguments[0]);
                    return SplitBytesLines(bytes, keepEnds, context, span);
                }, LythonCallableSignature.Create("bytes.splitlines", ["keepends"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 0)),
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

    private static object ReplaceBytes(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? oldValue = null;
        object? newValue = null;
        object? countArgument = null;
        var positionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "bytes.replace() takes no keyword arguments", span);
            }

            positionals++;
            if (positionals == 1)
            {
                oldValue = argument.Value;
            }
            else if (positionals == 2)
            {
                newValue = argument.Value;
            }
            else if (positionals == 3)
            {
                countArgument = argument.Value;
            }
        }

        if (positionals < 2)
        {
            throw new LythonRuntimeException("TypeError", "replace expected at least 2 arguments, got " + positionals, span);
        }

        if (positionals > 3)
        {
            throw new LythonRuntimeException("TypeError", "replace expected at most 3 arguments, got " + positionals, span);
        }

        if (oldValue is not PyBytes oldBytes)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(oldValue, context) + "'", span);
        }

        if (newValue is not PyBytes newBytes)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(newValue, context) + "'", span);
        }

        var count = -1;
        if (countArgument is not null)
        {
            count = RuntimeArgumentValidation.ParseInt32(countArgument, "count", "bytes.replace(old, new[, count])", span);
        }

        if (count == 0)
        {
            return value;
        }

        var source = value.Bytes;
        var oldSpan = oldBytes.Bytes;
        var newSpan = newBytes.Bytes;
        GovernedByteBuilder builder = value.OwnerMemoryGovernor is null
            ? new GovernedByteBuilder(source.Length)
            : new GovernedByteBuilder(value.OwnerMemoryGovernor, value.AllocationSpan, source.Length);

        if (oldSpan.IsEmpty)
        {
            var insertions = 0;
            if (count < 0 || insertions < count)
            {
                builder.Append(newSpan);
                insertions++;
            }

            foreach (var octet in source)
            {
                builder.Append(octet);
                if (count < 0 || insertions < count)
                {
                    builder.Append(newSpan);
                    insertions++;
                }
            }

            return CreateBytes(builder.ToArrayAndRelease(), context, span);
        }

        var offset = 0;
        var replaced = 0;
        while (offset < source.Length && (count < 0 || replaced < count))
        {
            var found = PyString.IndexOfBytes(source[offset..], oldSpan);
            if (found < 0)
            {
                break;
            }

            builder.Append(source.Slice(offset, found));
            builder.Append(newSpan);
            offset += found + oldSpan.Length;
            replaced++;
        }

        builder.Append(source[offset..]);
        return CreateBytes(builder.ToArrayAndRelease(), context, span);
    }

    private static bool IsAsciiLetter(byte octet)
    {
        return (octet >= 65 && octet <= 90) || (octet >= 97 && octet <= 122);
    }

    private static bool IsAsciiDigit(byte octet)
    {
        return octet >= 48 && octet <= 57;
    }

    private static bool IsAsciiSpace(byte octet)
    {
        return octet == 32 || (octet >= 9 && octet <= 13);
    }

    private static bool IsAsciiAlnum(PyBytes value)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        foreach (var octet in source)
        {
            if (!IsAsciiLetter(octet) && !IsAsciiDigit(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlpha(PyBytes value)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }
        foreach (var octet in source)
        {
            if (!IsAsciiLetter(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAscii(PyBytes value)
    {
        foreach (var octet in value.Bytes)
        {
            if (octet > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiDigit(PyBytes value)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        foreach (var octet in source)
        {
            if (!IsAsciiDigit(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLower(PyBytes value)
    {
        var foundLower = false;
        foreach (var octet in value.Bytes)
        {
            if (octet >= 97 && octet <= 122)
            {
                foundLower = true;
            }
            else if (octet >= 65 && octet <= 90)
            {
                return false;
            }
        }

        return foundLower;
    }

    private static bool IsAsciiUpper(PyBytes value)
    {
        var foundUpper = false;
        foreach (var octet in value.Bytes)
        {
            if (octet >= 65 && octet <= 90)
            {
                foundUpper = true;
            }
            else if (octet >= 97 && octet <= 122)
            {
                return false;
            }
        }

        return foundUpper;
    }

    private static bool IsAsciiSpace(PyBytes value)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        foreach (var octet in source)
        {
            if (!IsAsciiSpace(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiTitle(PyBytes value)
    {
        var foundCased = false;
        var previousIsCased = false;
        foreach (var octet in value.Bytes)
        {
            var isUpper = octet >= 65 && octet <= 90;
            var isLower = octet >= 97 && octet <= 122;
            if (isUpper)
            {
                if (previousIsCased)
                {
                    return false;
                }

                previousIsCased = true;
                foundCased = true;
            }
            else if (isLower)
            {
                if (!previousIsCased)
                {
                    return false;
                }

                previousIsCased = true;
                foundCased = true;
            }
            else
            {
                previousIsCased = false;
            }
        }

        return foundCased;
    }

    private static object RemoveBytesAffix(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool isPrefix)
    {
        object? affix = null;
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
                affix = argument.Value;
            }
        }

        if (positionals != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes." + methodName + "() takes exactly one argument (" + positionals + " given)", span);
        }

        if (affix is not PyBytes affixBytes)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(affix, context) + "'", span);
        }

        var source = value.Bytes;
        var affixSpan = affixBytes.Bytes;
        if (affixSpan.IsEmpty)
        {
            return value;
        }

        bool matches = isPrefix
            ? affixSpan.Length <= source.Length && source[..affixSpan.Length].SequenceEqual(affixSpan)
            : affixSpan.Length <= source.Length && source[^affixSpan.Length..].SequenceEqual(affixSpan);

        if (!matches)
        {
            return value;
        }

        var stripped = isPrefix ? source[affixSpan.Length..].ToArray() : source[..^affixSpan.Length].ToArray();
        return CreateBytes(stripped, context, span);
    }

    private enum BytesCaseMode
    {
        Lower,
        Upper,
        SwapCase,
        Capitalize,
        Title,
    }

    private static object MapBytesCase(PyBytes value, BytesCaseMode mode, LythonSourceSpan span, ExecutionContext context)
    {
        var source = value.Bytes;
        GovernedByteBuilder builder = value.OwnerMemoryGovernor is null
            ? new GovernedByteBuilder(source.Length)
            : new GovernedByteBuilder(value.OwnerMemoryGovernor, value.AllocationSpan, source.Length);

        var previousIsCased = false;
        var isFirst = true;
        foreach (var octet in source)
        {
            var isUpper = octet >= 65 && octet <= 90;
            var isLower = octet >= 97 && octet <= 122;
            byte mapped;
            switch (mode)
            {
                case BytesCaseMode.Lower:
                    mapped = isUpper ? (byte)(octet + 32) : octet;
                    break;
                case BytesCaseMode.Upper:
                    mapped = isLower ? (byte)(octet - 32) : octet;
                    break;
                case BytesCaseMode.SwapCase:
                    mapped = isUpper ? (byte)(octet + 32) : isLower ? (byte)(octet - 32) : octet;
                    break;
                case BytesCaseMode.Capitalize:
                    mapped = isFirst ? (isLower ? (byte)(octet - 32) : octet) : (isUpper ? (byte)(octet + 32) : octet);
                    break;
                default:
                    if (isUpper || isLower)
                    {
                        mapped = previousIsCased ? (isUpper ? (byte)(octet + 32) : octet) : (isLower ? (byte)(octet - 32) : octet);
                        previousIsCased = true;
                    }
                    else
                    {
                        mapped = octet;
                        previousIsCased = false;
                    }
                    break;
            }

            builder.Append(mapped);
            isFirst = false;
        }

        return CreateBytes(builder.ToArrayAndRelease(), context, span);
    }

    private enum BytesStripMode
    {
        Both,
        Left,
        Right,
    }

    private static object StripBytes(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, BytesStripMode mode)
    {
        object? chars = null;
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
                chars = argument.Value;
            }
        }

        if (positionals > 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes." + methodName + "([chars]) expects zero or one argument.", span);
        }

        var stripWhitespace = chars is null || chars is PyNone;
        ReadOnlySpan<byte> stripSet = ReadOnlySpan<byte>.Empty;
        if (!stripWhitespace && chars is not PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(chars, context) + "'", span);
        }

        if (chars is PyBytes resolved)
        {
            stripSet = resolved.Bytes;
        }

        var source = value.Bytes;
        var start = 0;
        var end = source.Length;
        if (mode != BytesStripMode.Right)
        {
            while (start < end && IsStrippedByte(source[start], stripSet, stripWhitespace))
            {
                start++;
            }
        }

        if (mode != BytesStripMode.Left)
        {
            while (end > start && IsStrippedByte(source[end - 1], stripSet, stripWhitespace))
            {
                end--;
            }
        }

        if (start == 0 && end == source.Length)
        {
            return value;
        }

        return CreateBytes(source[start..end].ToArray(), context, span);
    }

    private static bool IsStrippedByte(byte octet, ReadOnlySpan<byte> stripSet, bool stripWhitespace)
    {
        return stripWhitespace ? IsAsciiSpace(octet) : stripSet.IndexOf(octet) >= 0;
    }

    private static PyList NewBytesPartList(MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? new PyList() : new PyList([], governor, span);

    private static PyBytes SliceBytesRange(ReadOnlySpan<byte> source, int start, int end, ExecutionContext context, LythonSourceSpan span)
    {
        if (start >= end)
        {
            return CreateBytes([], context, span);
        }

        return CreateBytes(source[start..end].ToArray(), context, span);
    }

    private static PyList SplitBytesWhitespace(PyBytes value, int maxSplit, ExecutionContext context, LythonSourceSpan span)
    {
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var source = value.Bytes;
        var start = 0;
        while (start < source.Length && IsAsciiSpace(source[start]))
        {
            start++;
        }

        if (start >= source.Length)
        {
            return parts;
        }

        if (maxSplit == 0)
        {
            parts.Add(SliceBytesRange(source, start, source.Length, context, span));
            return parts;
        }

        var splits = 0;
        while (true)
        {
            var end = start;
            while (end < source.Length && !IsAsciiSpace(source[end]))
            {
                end++;
            }

            if (maxSplit >= 0 && splits == maxSplit)
            {
                parts.Add(SliceBytesRange(source, start, source.Length, context, span));
                break;
            }

            parts.Add(SliceBytesRange(source, start, end, context, span));
            splits++;
            start = end;
            while (start < source.Length && IsAsciiSpace(source[start]))
            {
                start++;
            }

            if (start >= source.Length)
            {
                break;
            }
        }

        return parts;
    }

    private static PyList RSplitBytesWhitespace(PyBytes value, int maxSplit, ExecutionContext context, LythonSourceSpan span)
    {
        if (maxSplit < 0)
        {
            return SplitBytesWhitespace(value, -1, context, span);
        }

        var governor = context.MemoryGovernor;
        var source = value.Bytes;
        var endByte = source.Length;
        while (endByte > 0 && IsAsciiSpace(source[endByte - 1]))
        {
            endByte--;
        }

        var parts = new List<PyBytes>();
        if (endByte == 0)
        {
            return NewBytesPartList(governor, span);
        }

        if (maxSplit == 0)
        {
            var only = SliceBytesRange(source, 0, endByte, context, span);
            return governor is null ? new PyList([only]) : new PyList([only], governor, span);
        }

        var splits = 0;
        while (endByte > 0)
        {
            var partEnd = endByte;
            var partStart = endByte;
            while (partStart > 0 && !IsAsciiSpace(source[partStart - 1]))
            {
                partStart--;
            }

            if (splits == maxSplit)
            {
                parts.Add(SliceBytesRange(source, 0, partEnd, context, span));
                break;
            }

            parts.Add(SliceBytesRange(source, partStart, partEnd, context, span));
            splits++;
            endByte = partStart;
            while (endByte > 0 && IsAsciiSpace(source[endByte - 1]))
            {
                endByte--;
            }

            if (endByte == 0)
            {
                break;
            }
        }

        parts.Reverse();
        var items = new object[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            items[i] = parts[i];
        }

        return governor is null ? new PyList(items) : new PyList(items, governor, span);
    }

    private static PyList SplitBytes(PyBytes value, PyBytes separator, int maxSplit, ExecutionContext context, LythonSourceSpan span)
    {
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var source = value.Bytes;
        var needle = separator.Bytes;
        if (needle.IsEmpty)
        {
            throw new InvalidOperationException("empty separator");
        }

        var offset = 0;
        var splits = 0;
        while (maxSplit < 0 || splits < maxSplit)
        {
            var found = PyString.IndexOfBytes(source[offset..], needle);
            if (found < 0)
            {
                break;
            }

            parts.Add(SliceBytesRange(source, offset, offset + found, context, span));
            offset += found + needle.Length;
            splits++;
        }

        parts.Add(SliceBytesRange(source, offset, source.Length, context, span));
        return parts;
    }

    private static PyList RSplitBytes(PyBytes value, PyBytes separator, int maxSplit, ExecutionContext context, LythonSourceSpan span)
    {
        var needle = separator.Bytes;
        if (needle.IsEmpty)
        {
            throw new InvalidOperationException("empty separator");
        }

        var source = value.Bytes;
        var matches = new List<int>();
        var searchFrom = 0;
        while (searchFrom <= source.Length)
        {
            var found = PyString.IndexOfBytes(source[searchFrom..], needle);
            if (found < 0)
            {
                break;
            }

            matches.Add(searchFrom + found);
            searchFrom += found + needle.Length;
        }

        var firstKept = maxSplit < 0 ? 0 : int.Max(0, matches.Count - maxSplit);
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var offset = 0;
        for (var i = firstKept; i < matches.Count; i++)
        {
            parts.Add(SliceBytesRange(source, offset, matches[i], context, span));
            offset = matches[i] + needle.Length;
        }

        parts.Add(SliceBytesRange(source, offset, source.Length, context, span));
        return parts;
    }

    private static PyList SplitBytesLines(PyBytes value, bool keepEnds, ExecutionContext context, LythonSourceSpan span)
    {
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var source = value.Bytes;
        var start = 0;
        var index = 0;
        while (index < source.Length)
        {
            int breakLength;
            if (source[index] == (byte)'\r')
            {
                breakLength = index + 1 < source.Length && source[index + 1] == (byte)'\n' ? 2 : 1;
            }
            else if (source[index] == (byte)'\n')
            {
                breakLength = 1;
            }
            else
            {
                index++;
                continue;
            }

            var end = keepEnds ? index + breakLength : index;
            parts.Add(SliceBytesRange(source, start, end, context, span));
            index += breakLength;
            start = index;
        }

        if (start < source.Length)
        {
            parts.Add(SliceBytesRange(source, start, source.Length, context, span));
        }

        return parts;
    }
    private static object PartitionBytes(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool isFirst)
    {
        object? separator = null;
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
                separator = argument.Value;
            }
        }

        if (positionals != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes." + methodName + "() takes exactly one argument (" + positionals + " given)", span);
        }

        if (separator is not PyBytes separatorBytes)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(separator, context) + "'", span);
        }

        var source = value.Bytes;
        var needle = separatorBytes.Bytes;
        if (needle.IsEmpty)
        {
            throw new LythonRuntimeException("ValueError", "empty separator", span);
        }

        var match = -1;
        var searchFrom = 0;
        while (searchFrom <= source.Length)
        {
            var found = PyString.IndexOfBytes(source[searchFrom..], needle);
            if (found < 0)
            {
                break;
            }

            match = searchFrom + found;
            if (isFirst)
            {
                break;
            }

            searchFrom = match + needle.Length;
        }

        var governor = context.MemoryGovernor;
        if (match < 0)
        {
            var missed = isFirst
                ? new object[] { value, CreateBytes([], context, span), CreateBytes([], context, span) }
                : new object[] { CreateBytes([], context, span), CreateBytes([], context, span), value };
            return governor is null ? new PyTuple(missed) : new PyTuple(missed, governor, span);
        }

        var parts = new object[] { SliceBytesRange(source, 0, match, context, span), separatorBytes, SliceBytesRange(source, match + needle.Length, source.Length, context, span) };
        return governor is null ? new PyTuple(parts) : new PyTuple(parts, governor, span);
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
