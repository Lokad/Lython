using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class BytesMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature BytesDecodeSignature = LythonCallableSignature.Create("bytes.decode", ["encoding", "errors"], 0);
        private static readonly LythonCallableSignature BytesExpandTabsSignature = LythonCallableSignature.Create("bytes.expandtabs", ["tabsize"], 0);
        private static readonly LythonCallableSignature BytesSplitSignature = LythonCallableSignature.Create("bytes.split", ["sep", "maxsplit"], 0);
        private static readonly LythonCallableSignature BytesRSplitSignature = LythonCallableSignature.Create("bytes.rsplit", ["sep", "maxsplit"], 0);
        private static readonly LythonCallableSignature BytesContainsSignature = LythonCallableSignature.Create("bytes.__contains__", ["item"]);
        private static readonly LythonCallableSignature BytesGetItemSignature = LythonCallableSignature.Create("bytes.__getitem__", ["index"]);
        private static readonly LythonCallableSignature BytesAddSignature = LythonCallableSignature.Create("bytes.__add__", ["value"]);
        private static readonly LythonCallableSignature BytesMulSignature = LythonCallableSignature.Create("bytes.__mul__", ["value"]);
        private static readonly LythonCallableSignature BytesRMulSignature = LythonCallableSignature.Create("bytes.__rmul__", ["value"]);
        private static readonly LythonCallableSignature BytesEqSignature = LythonCallableSignature.Create("bytes.__eq__", ["value"]);
        private static readonly LythonCallableSignature BytesNeSignature = LythonCallableSignature.Create("bytes.__ne__", ["value"]);
        private static readonly LythonCallableSignature BytesLtSignature = LythonCallableSignature.Create("bytes.__lt__", ["value"]);
        private static readonly LythonCallableSignature BytesLeSignature = LythonCallableSignature.Create("bytes.__le__", ["value"]);
        private static readonly LythonCallableSignature BytesGtSignature = LythonCallableSignature.Create("bytes.__gt__", ["value"]);
        private static readonly LythonCallableSignature BytesGeSignature = LythonCallableSignature.Create("bytes.__ge__", ["value"]);
        private static readonly LythonCallableSignature BytesHashSignature = LythonCallableSignature.Create("bytes.__hash__");
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

                    if (arguments.Length >= 1) RequireCodecNameType(arguments[0], "bytes.decode()", "encoding", span);
                    if (arguments.Length == 2) RequireCodecNameType(arguments[1], "bytes.decode()", "errors", span);

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    return DecodeText(bytes.Memory, encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
                }, BytesDecodeSignature),
                "hex" => new RawBoundCallable((arguments, span, context) => HexEncodeAsync(bytes, arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => HexEncodeAsync(bytes, arguments, span, context, true)) { BoundName = "bytes.hex", BoundReceiver = bytes },
                "count" => new RawBoundCallable((arguments, span, context) => SearchBytesAsync(bytes, "count", arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => SearchBytesAsync(bytes, "count", arguments, span, context, true)) { BoundName = "bytes.count", BoundReceiver = bytes },
                "find" => new RawBoundCallable((arguments, span, context) => SearchBytesAsync(bytes, "find", arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => SearchBytesAsync(bytes, "find", arguments, span, context, true)) { BoundName = "bytes.find", BoundReceiver = bytes },
                "index" => new RawBoundCallable((arguments, span, context) => SearchBytesAsync(bytes, "index", arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => SearchBytesAsync(bytes, "index", arguments, span, context, true)) { BoundName = "bytes.index", BoundReceiver = bytes },
                "rfind" => new RawBoundCallable((arguments, span, context) => SearchBytesAsync(bytes, "rfind", arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => SearchBytesAsync(bytes, "rfind", arguments, span, context, true)) { BoundName = "bytes.rfind", BoundReceiver = bytes },
                "rindex" => new RawBoundCallable((arguments, span, context) => SearchBytesAsync(bytes, "rindex", arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => SearchBytesAsync(bytes, "rindex", arguments, span, context, true)) { BoundName = "bytes.rindex", BoundReceiver = bytes },
                "startswith" => new RawBoundCallable((arguments, span, context) => StartsOrEndsWithBytesAsync(bytes, "startswith", arguments, span, context, true, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => StartsOrEndsWithBytesAsync(bytes, "startswith", arguments, span, context, true, true)) { BoundName = "bytes.startswith", BoundReceiver = bytes },
                "endswith" => new RawBoundCallable((arguments, span, context) => StartsOrEndsWithBytesAsync(bytes, "endswith", arguments, span, context, false, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => StartsOrEndsWithBytesAsync(bytes, "endswith", arguments, span, context, false, true)) { BoundName = "bytes.endswith", BoundReceiver = bytes },
                "replace" => new RawBoundCallable((arguments, span, context) => ReplaceBytesAsync(bytes, arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => ReplaceBytesAsync(bytes, arguments, span, context, true)) { BoundName = "bytes.replace", BoundReceiver = bytes },
                "isalnum" => BoundCallable.CreateNoArguments(bytes, "bytes.isalnum", static (receiver, span, context) => IsAsciiAlnum(receiver, context, span)),
                "isalpha" => BoundCallable.CreateNoArguments(bytes, "bytes.isalpha", static (receiver, span, context) => IsAsciiAlpha(receiver, context, span)),
                "isascii" => BoundCallable.CreateNoArguments(bytes, "bytes.isascii", static (receiver, span, context) => IsAscii(receiver, context, span)),
                "isdigit" => BoundCallable.CreateNoArguments(bytes, "bytes.isdigit", static (receiver, span, context) => IsAsciiDigit(receiver, context, span)),
                "islower" => BoundCallable.CreateNoArguments(bytes, "bytes.islower", static (receiver, span, context) => IsAsciiLower(receiver, context, span)),
                "isspace" => BoundCallable.CreateNoArguments(bytes, "bytes.isspace", static (receiver, span, context) => IsAsciiSpace(receiver, context, span)),
                "istitle" => BoundCallable.CreateNoArguments(bytes, "bytes.istitle", static (receiver, span, context) => IsAsciiTitle(receiver, context, span)),
                "isupper" => BoundCallable.CreateNoArguments(bytes, "bytes.isupper", static (receiver, span, context) => IsAsciiUpper(receiver, context, span)),
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
                "join" => new RawBoundCallable((arguments, span, context) => JoinBytes(bytes, arguments, span, context),
                    (arguments, span, context) => JoinBytesAsync(bytes, arguments, span, context)) { BoundName = "bytes.join", BoundReceiver = bytes },
                "center" => new RawBoundCallable((arguments, span, context) => PadBytesAsync(bytes, "center", arguments, span, context, BytesPadMode.Center, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => PadBytesAsync(bytes, "center", arguments, span, context, BytesPadMode.Center, true)) { BoundName = "bytes.center", BoundReceiver = bytes },
                "ljust" => new RawBoundCallable((arguments, span, context) => PadBytesAsync(bytes, "ljust", arguments, span, context, BytesPadMode.LJust, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => PadBytesAsync(bytes, "ljust", arguments, span, context, BytesPadMode.LJust, true)) { BoundName = "bytes.ljust", BoundReceiver = bytes },
                "rjust" => new RawBoundCallable((arguments, span, context) => PadBytesAsync(bytes, "rjust", arguments, span, context, BytesPadMode.RJust, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => PadBytesAsync(bytes, "rjust", arguments, span, context, BytesPadMode.RJust, true)) { BoundName = "bytes.rjust", BoundReceiver = bytes },
                "zfill" => new RawBoundCallable((arguments, span, context) => ZFillBytesAsync(bytes, arguments, span, context, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => ZFillBytesAsync(bytes, arguments, span, context, true)) { BoundName = "bytes.zfill", BoundReceiver = bytes },
                "expandtabs" => BoundCallable.Create(
                    (args, span, context) => ExpandBytesTabsAsync(bytes, args, span, context, false).GetAwaiter().GetResult(),
                    BytesExpandTabsSignature,
                    (args, span, context) => ExpandBytesTabsAsync(bytes, args, span, context, true)),
                "split" => BoundCallable.Create(
                    (args, span, context) => SplitBytesAsync(bytes, args, span, context, false, false).GetAwaiter().GetResult(),
                    BytesSplitSignature,
                    (args, span, context) => SplitBytesAsync(bytes, args, span, context, false, true)),
                "rsplit" => BoundCallable.Create(
                    (args, span, context) => SplitBytesAsync(bytes, args, span, context, true, false).GetAwaiter().GetResult(),
                    BytesRSplitSignature,
                    (args, span, context) => SplitBytesAsync(bytes, args, span, context, true, true)),
                "splitlines" => BoundCallable.Create(
                    (args, span, context) => SplitBytesLinesAsync(bytes, args, span, context, false).GetAwaiter().GetResult(),
                    LythonCallableSignature.Create("bytes.splitlines", ["keepends"], 0),
                    (args, span, context) => SplitBytesLinesAsync(bytes, args, span, context, true)),
                "__iter__" => BoundCallable.CreateNoArguments(bytes, "bytes.__iter__", static (receiver, span, context) =>
                {
                    PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
                    var bytesIterResult = new PyEnumerableIterator(receiver, span, context);
                    return bytesIterResult;
                }),
                "__len__" => BoundCallable.CreateNoArguments(bytes, "bytes.__len__", static (receiver, span, context) => Len([receiver], span, context)),
                "__contains__" => BoundCallable.Create(
                    (args, span, context) => ContainsBytesAsync(bytes, args[0], context, span, false).GetAwaiter().GetResult(),
                    BytesContainsSignature,
                    async (args, span, context) => await ContainsBytesAsync(bytes, args[0], context, span, true).ConfigureAwait(false)),
                "__getitem__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__getitem__(index) expects one argument.", span);
                    }

                    return ReadSubscriptValue(bytes, arguments[0], span, context);
                }, BytesGetItemSignature),
                "__add__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__add__(value) expects one argument.", span);
                    }

                    return EvaluateAdd(bytes, arguments[0], context, span);
                }, BytesAddSignature),
                "__mul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__mul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(bytes, arguments[0], context, span);
                }, BytesMulSignature),
                "__rmul__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__rmul__(value) expects one argument.", span);
                    }

                    RequireRepeatCount(arguments[0], context, span);

                    return EvaluateMultiply(arguments[0], bytes, context, span);
                }, BytesRMulSignature),
                "__eq__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__eq__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return bytes.Equals(other);
                }, BytesEqSignature),
                "__ne__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__ne__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return !bytes.Equals(other);
                }, BytesNeSignature),
                "__lt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__lt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return bytes.Memory.Span.SequenceCompareTo(other.Memory.Span) < 0;
                }, BytesLtSignature),
                "__le__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__le__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return bytes.Memory.Span.SequenceCompareTo(other.Memory.Span) <= 0;
                }, BytesLeSignature),
                "__gt__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__gt__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return bytes.Memory.Span.SequenceCompareTo(other.Memory.Span) > 0;
                }, BytesGtSignature),
                "__ge__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__ge__(value) expects one argument.", span);
                    }

                    if (arguments[0] is not PyBytes other)
                    {
                        return PyNotImplemented.Instance;
                    }

                    return bytes.Memory.Span.SequenceCompareTo(other.Memory.Span) >= 0;
                }, BytesGeSignature),
                "__hash__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.__hash__() expects no arguments.", span);
                    }

                    return ComputeBuiltinHash(bytes, span);
                }, BytesHashSignature),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static int NormalizeSequenceCount(long value) => (int)Math.Clamp(value, -1, int.MaxValue);

    private static int NormalizeTextWidth(long value, LythonSourceSpan span)
    {
        if (value <= 0) return 0;
        if (value > Array.MaxLength) throw RuntimeErrors.Memory("requested width exceeds supported storage size", span);
        return (int)value;
    }

    private static async ValueTask<object> ExpandBytesTabsAsync(PyBytes value, object[] args, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        var size = args.Length == 0 ? 8 : await RuntimeArgumentValidation.CoerceIndexInt32Async(args[0], context, span, asynchronous).ConfigureAwait(false);
        return ExpandBytesTabs(value, size, context, span);
    }

    private static async ValueTask<object> SplitBytesAsync(PyBytes value, object[] args, LythonSourceSpan span,
        ExecutionContext context, bool reverse, bool asynchronous)
    {
        if (args.Length > 0 && args[0] is not (PyNone or PyBytes))
            throw RuntimeErrors.Type("a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(args[0], context) + "'", span);
        var count = args.Length < 2 ? -1 : NormalizeSequenceCount(await CoerceIoIntegerAsync(args[1], span, context, asynchronous).ConfigureAwait(false));
        if (args.Length == 0 || args[0] is PyNone)
            return reverse ? RSplitBytesWhitespace(value, count, context, span) : SplitBytesWhitespace(value, count, context, span);
        var separator = (PyBytes)args[0];
        if (separator.Length == 0) throw RuntimeErrors.Value("empty separator", span);
        return reverse ? RSplitBytes(value, separator, count, context, span) : SplitBytes(value, separator, count, context, span);
    }

    private static async ValueTask<object> SplitBytesLinesAsync(PyBytes value, object[] args, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        var keep = args.Length != 0 && (asynchronous ? await IsTruthyAsync(args[0], context, span).ConfigureAwait(false) : IsTruthy(args[0], context, span));
        return SplitBytesLines(value, keep, context, span);
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

        if (table is not PyBytes and not PyNone)
        {
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(table, context) + "'", span);
        }
        if (table is PyBytes tableBytes && tableBytes.Length != 256)
        {
            throw new LythonRuntimeException("ValueError", "translation table must be 256 characters long", span);
        }

        var mapping = table is PyBytes mapped ? mapped.Bytes : ReadOnlySpan<byte>.Empty;
        Span<bool> discarded = stackalloc bool[256];
        discarded.Clear();
        if (delete is not null)
        {
            if (delete is not PyBytes deleteBytes)
            {
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(delete, context) + "'", span);
            }

            var deletion = deleteBytes.Bytes;
            for (var i = 0; i < deletion.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                discarded[deletion[i]] = true;
            }
        }

        var source = value.Bytes;
        var count = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            if (!discarded[source[i]]) count++;
        }
        if (mapping.IsEmpty && count == source.Length) return value;
        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(count), span);
        context.CheckExecution(span);
        var result = new byte[count];
        var offset = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            var octet = source[i];
            if (!discarded[octet]) result[offset++] = mapping.IsEmpty ? octet : mapping[octet];
        }
        return new PyBytes(result, context.MemoryGovernor, span, storage);
    }

    private static async ValueTask<object> HexEncodeAsync(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
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
            perSep = await RuntimeArgumentValidation.CoerceIndexInt32Async(group, context, span, asynchronous).ConfigureAwait(false);
        }

        var source = value.Bytes;
        var groupSize = Math.Abs((long)perSep);
        var separators = separator is not null && groupSize > 0 && source.Length > 0 ? (source.Length - 1L) / groupSize : 0;
        var length = 2L * source.Length + separators;
        if (length > int.MaxValue) throw new LythonRuntimeException("OverflowError", "Hexadecimal bytes are too large.", span);
        context.CheckExecution(span);
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span, (int)length);
        try
        {
            const string digits = "0123456789abcdef";
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (i > 0 && separator is not null && groupSize > 0 &&
                    (perSep < 0 ? i % groupSize == 0 : (source.Length - i) % groupSize == 0))
                    builder.Append(separator.Value);
                builder.Append((byte)digits[source[i] >> 4]);
                builder.Append((byte)digits[source[i] & 15]);
            }
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    private static byte ParseHexSeparator(object sep, LythonSourceSpan span)
    {
        if (sep is PyString sepText)
        {
            Rune.DecodeFromUtf8(sepText.Utf8Bytes.Span, out var rune, out var consumed);
            if (consumed == 0 || consumed != sepText.Utf8Bytes.Length)
            {
                throw new LythonRuntimeException("ValueError", "sep must be length 1.", span);
            }

            if (rune.Value > 127)
            {
                throw new LythonRuntimeException("ValueError", "sep must be ASCII.", span);
            }

            return (byte)rune.Value;
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


    private static void ValidateBytesPositionals(CallArgumentValue[] arguments, string method, int minimum, int maximum, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
            if (argument.IsKeyword) throw RuntimeErrors.Type("bytes." + method + "() takes no keyword arguments", span);
        if (arguments.Length < minimum)
            throw RuntimeErrors.Type(method + " expected at least " + minimum + " argument, got " + arguments.Length, span);
        if (arguments.Length > maximum)
            throw RuntimeErrors.Type(method + " expected at most " + maximum + " arguments, got " + arguments.Length, span);
    }

    private static async ValueTask<(int Start, int End)> ByteSearchBoundsAsync(PyBytes value, CallArgumentValue[] args,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var start = args.Length < 2 ? null : await ByteSearchBoundAsync(args[1].Value, context, span, asynchronous).ConfigureAwait(false);
        var end = args.Length < 3 ? null : await ByteSearchBoundAsync(args[2].Value, context, span, asynchronous).ConfigureAwait(false);
        return (RuntimeArgumentValidation.NormalizeSearchBound(start, value.Length, 0),
            Math.Min(value.Length, RuntimeArgumentValidation.NormalizeSearchBound(end, value.Length, value.Length)));
    }

    private static async ValueTask<BigInteger?> ByteSearchBoundAsync(object argument, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        if (argument is PyNone) return null;
        var indexed = await CoerceTypeIndexAsync(argument, context, span, asynchronous).ConfigureAwait(false);
        return RuntimeArgumentValidation.ExpectInteger(indexed ?? PyNone.Instance,
            "slice indices must be integers or None or have an __index__ method", span);
    }

    private static async ValueTask<object> ByteNeedleAsync(object value, ExecutionContext? context, LythonSourceSpan span, bool asynchronous, string typeError)
    {
        if (value is PyBytes) return value;
        var indexed = context is null ? value : await CoerceTypeIndexAsync(value, context, span, asynchronous).ConfigureAwait(false);
        if (!PyNumberOps.TryAsInteger(indexed ?? PyNone.Instance, out var integer))
            throw RuntimeErrors.Type(typeError + ", not '" + RuntimeErrors.OperandTypeName(value) + "'", span);
        if (integer < 0 || integer > 255) throw RuntimeErrors.Value("byte must be in range(0, 256)", span);
        return (byte)integer;
    }

    private static async ValueTask<object> SearchBytesAsync(PyBytes value, string method, CallArgumentValue[] args,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        ValidateBytesPositionals(args, method, 1, 3, span);
        var (start, end) = await ByteSearchBoundsAsync(value, args, context, span, asynchronous).ConfigureAwait(false);
        var needle = await ByteNeedleAsync(args[0].Value, context, span, asynchronous, "argument should be integer or bytes-like object").ConfigureAwait(false);
        var found = SearchPreparedBytes(value, needle, start, end, method, context, span);
        if (found < 0 && method is "index" or "rindex") throw RuntimeErrors.Value("subsection not found", span);
        return new BigInteger(found);
    }

    internal static async ValueTask<bool> ContainsBytesAsync(PyBytes value, object candidate, ExecutionContext? context,
        LythonSourceSpan span, bool asynchronous)
    {
        var needle = await ByteNeedleAsync(candidate, context, span, asynchronous, "a bytes-like object is required").ConfigureAwait(false);
        return SearchPreparedBytes(value, needle, 0, value.Length, "find", context, span) >= 0;
    }

    private static int SearchPreparedBytes(PyBytes value, object prepared, int start, int end, string method,
        ExecutionContext? context, LythonSourceSpan span)
    {
        if (end < start) return method == "count" ? 0 : -1;
        Span<byte> single = stackalloc byte[1];
        if (prepared is byte octet) single[0] = octet;
        ReadOnlySpan<byte> needle = prepared is PyBytes data ? data.Bytes : single;
        var source = value.Bytes[start..end];
        if (method != "count")
        {
            var found = FindBytes(source, needle, context, span, method is "rfind" or "rindex");
            return found < 0 ? -1 : start + found;
        }
        if (needle.IsEmpty) return source.Length + 1;
        var count = 0;
        for (var offset = 0; offset <= source.Length - needle.Length;)
        {
            var found = FindBytes(source[offset..], needle, context, span);
            if (found < 0) break;
            count++;
            offset += found + needle.Length;
        }
        return count;
    }

    // Ranges partition candidate starts, not input bytes: a needle crossing a
    // range boundary is searched once. The managed BCL performs each bulk search.
    private static int FindBytes(ReadOnlySpan<byte> source, ReadOnlySpan<byte> needle,
        ExecutionContext? context, LythonSourceSpan span, bool reverse = false)
    {
        context?.CheckExecution(span);
        if (needle.IsEmpty) return reverse ? source.Length : 0;
        var candidates = source.Length - needle.Length + 1;
        const int chunk = 16384;
        for (var position = reverse ? candidates : 0; reverse ? position > 0 : position < candidates;)
        {
            context?.CheckExecution(span);
            var start = reverse ? Math.Max(0, position - chunk) : position;
            var count = reverse ? position - start : Math.Min(chunk, candidates - start);
            var range = source.Slice(start, count + needle.Length - 1);
            var found = reverse ? range.LastIndexOf(needle) : range.IndexOf(needle);
            if (found >= 0) return start + found;
            position = reverse ? start : start + count;
        }
        return -1;
    }

    private static async ValueTask<object> StartsOrEndsWithBytesAsync(PyBytes value, string method, CallArgumentValue[] args,
        LythonSourceSpan span, ExecutionContext context, bool isStart, bool asynchronous)
    {
        ValidateBytesPositionals(args, method, 1, 3, span);
        var (start, end) = await ByteSearchBoundsAsync(value, args, context, span, asynchronous).ConfigureAwait(false);
        var prefix = args[0].Value;
        if (prefix is PyBytes bytes) return MatchesByteAffix(value, bytes, start, end, isStart);
        if (prefix is not PyTuple tuple) throw RuntimeErrors.Type(method + " first arg must be bytes or a tuple of bytes, not " + UnboundTypeMethod.PythonTypeName(prefix, context), span);
        foreach (var item in tuple)
        {
            context.CheckExecution(span);
            if (item is not PyBytes part) throw RuntimeErrors.Type("a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(item, context) + "'", span);
            if (MatchesByteAffix(value, part, start, end, isStart)) return true;
        }
        return false;

    }

    private static bool MatchesByteAffix(PyBytes value, PyBytes part, int start, int end, bool isStart)
        => end - start >= part.Length && value.Bytes.Slice(isStart ? start : end - part.Length, part.Length).SequenceEqual(part.Bytes);

    private static async ValueTask<object> ReplaceBytesAsync(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
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
            count = NormalizeSequenceCount(await CoerceIoIntegerAsync(countArgument, span, context, asynchronous).ConfigureAwait(false));
        }

        if (count == 0)
        {
            return value;
        }

        var source = value.Bytes;
        var oldSpan = oldBytes.Bytes;
        var newSpan = newBytes.Bytes;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span, source.Length);
        try
        {

            if (oldSpan.IsEmpty)
            {
                var insertions = 0;
                if (count < 0 || insertions < count)
                {
                    builder.Append(newSpan);
                    insertions++;
                }

                for (var i = 0; i < source.Length; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    builder.Append(source[i]);
                    if (count < 0 || insertions < count)
                    {
                        builder.Append(newSpan);
                        insertions++;
                    }
                }

                context.CheckExecution(span);
                return builder.ToPyBytesAndRelease();
            }

            var offset = 0;
            var replaced = 0;
            while (offset < source.Length && (count < 0 || replaced < count))
            {
                context.CheckExecution(span);
                var found = FindBytes(source[offset..], oldSpan, context, span);
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
            context.CheckExecution(span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
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

    private static bool IsAsciiAlnum(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        var position = 0;
        foreach (var octet in source)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            if (!IsAsciiLetter(octet) && !IsAsciiDigit(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlpha(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }
        var position = 0;
        foreach (var octet in source)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            if (!IsAsciiLetter(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAscii(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var position = 0;
        foreach (var octet in value.Bytes)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            if (octet > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiDigit(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        var position = 0;
        foreach (var octet in source)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            if (!IsAsciiDigit(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLower(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var foundLower = false;
        var position = 0;
        foreach (var octet in value.Bytes)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
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

    private static bool IsAsciiUpper(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var foundUpper = false;
        var position = 0;
        foreach (var octet in value.Bytes)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
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

    private static bool IsAsciiSpace(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return false;
        }

        var position = 0;
        foreach (var octet in source)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            if (!IsAsciiSpace(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiTitle(PyBytes value, ExecutionContext context, LythonSourceSpan span)
    {
        var foundCased = false;
        var previousIsCased = false;
        var position = 0;
        foreach (var octet in value.Bytes)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
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

        return isPrefix ? SliceBytesRange(source, affixSpan.Length, source.Length, context, span)
                        : SliceBytesRange(source, 0, source.Length - affixSpan.Length, context, span);
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
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span, source.Length);
        try
        {

            var previousIsCased = false;
            var isFirst = true;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var octet = source[i];
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

            context.CheckExecution(span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
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

        return SliceBytesRange(source, start, end, context, span);
    }

    private static bool IsStrippedByte(byte octet, ReadOnlySpan<byte> stripSet, bool stripWhitespace)
    {
        return stripWhitespace ? IsAsciiSpace(octet) : stripSet.IndexOf(octet) >= 0;
    }

    private static PyList NewBytesPartList(MemoryGovernor governor, LythonSourceSpan? span)
        => new PyList([], governor, span);

    private static PyBytes SliceBytesRange(ReadOnlySpan<byte> source, int start, int end, ExecutionContext context, LythonSourceSpan span)
    {
        if (start >= end)
        {
            return CreateBytes([], context, span);
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(end - start), span);
        context.CheckExecution(span);
        return new PyBytes(source[start..end].ToArray(), context.MemoryGovernor, span, storage);
    }

    private static PyList SplitBytesWhitespace(PyBytes value, int maxSplit, ExecutionContext context, LythonSourceSpan span)
    {
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var source = value.Bytes;
        var start = 0;
        while (start < source.Length && IsAsciiSpace(source[start]))
        {
            if ((start & 1023) == 0) context.CheckExecution(span);
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
                if ((end & 1023) == 0) context.CheckExecution(span);
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
                if ((start & 1023) == 0) context.CheckExecution(span);
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
            if ((endByte & 1023) == 0) context.CheckExecution(span);
            endByte--;
        }

        var parts = NewBytesPartList(governor, span);
        if (endByte == 0)
        {
            return parts;
        }

        if (maxSplit == 0)
        {
            parts.Add(SliceBytesRange(source, 0, endByte, context, span));
            return parts;
        }

        var splits = 0;
        while (endByte > 0)
        {
            context.CheckExecution(span);
            var partEnd = endByte;
            var partStart = endByte;
            while (partStart > 0 && !IsAsciiSpace(source[partStart - 1]))
            {
                if ((partStart & 1023) == 0) context.CheckExecution(span);
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
                if ((endByte & 1023) == 0) context.CheckExecution(span);
                endByte--;
            }

            if (endByte == 0)
            {
                break;
            }
        }

        parts.Reverse();
        return parts;
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
            var found = FindBytes(source[offset..], needle, context, span);
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
        var parts = NewBytesPartList(context.MemoryGovernor, span);
        var searchEnd = source.Length;
        var splits = 0;
        while (maxSplit < 0 || splits < maxSplit)
        {
            context.CheckExecution(span);
            var found = FindBytes(source[..searchEnd], needle, context, span, true);
            if (found < 0) break;
            parts.Add(SliceBytesRange(source, found + needle.Length, searchEnd, context, span));
            searchEnd = found;
            splits++;
        }
        parts.Add(SliceBytesRange(source, 0, searchEnd, context, span));
        parts.Reverse();
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
            if ((index & 1023) == 0) context.CheckExecution(span);
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

        var match = isFirst
            ? FindBytes(source, needle, context, span)
            : FindBytes(source, needle, context, span, true);

        var governor = context.MemoryGovernor;
        if (match < 0)
        {
            var missed = isFirst
                ? new object[] { value, CreateBytes([], context, span), CreateBytes([], context, span) }
                : new object[] { CreateBytes([], context, span), CreateBytes([], context, span), value };
            // The tuple owns reference storage independently of fresh byte
            // pieces and of the receiver/separator aliases.
            var missedTuple = new PyTuple(missed, governor, span);
            return missedTuple;
        }

        var parts = new object[] { SliceBytesRange(source, 0, match, context, span), separatorBytes, SliceBytesRange(source, match + needle.Length, source.Length, context, span) };
        var tuple = new PyTuple(parts, governor, span);
        return tuple;
    }
    private static object JoinBytes(PyBytes separator, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var parts = PyIteration.MaterializeLeased(BytesJoinIterable(arguments, span), span, context);
        return BuildJoinedBytes(separator, parts.Items, span, context);
    }

    private static async ValueTask<object> JoinBytesAsync(PyBytes separator, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var parts = await PyIteration.MaterializeLeasedAsync(BytesJoinIterable(arguments, span), span, context).ConfigureAwait(false);
        return BuildJoinedBytes(separator, parts.Items, span, context);
    }

    private static object BytesJoinIterable(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        object iterable = PyNone.Instance;
        var positionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "bytes.join() takes no keyword arguments", span);
            }

            positionals++;
            if (positionals == 1)
            {
                iterable = argument.Value;
            }
        }

        if (positionals != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes.join() takes exactly one argument (" + positionals + " given)", span);
        }

        return iterable;
    }

    private static PyBytes BuildJoinedBytes(PyBytes separator, IReadOnlyList<object> parts, LythonSourceSpan span, ExecutionContext context)
    {
        long length = parts.Count > 1 ? (long)separator.Length * (parts.Count - 1) : 0;
        for (var index = 0; index < parts.Count; index++)
        {
            context.CheckExecution(span);
            if (parts[index] is not PyBytes part)
            {
                throw new LythonRuntimeException("TypeError", "sequence item " + index + ": expected a bytes-like object, " + UnboundTypeMethod.PythonTypeName(parts[index], context) + " found", span);
            }
            length = checked(length + part.Length);
        }
        if (parts.Count == 1) return (PyBytes)parts[0];
        if (length > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Joined bytes are too large.", span);
        }
        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes((int)length), span);
        context.CheckExecution(span);
        var result = new byte[(int)length];
        var offset = 0;
        for (var index = 0; index < parts.Count; index++)
        {
            context.CheckExecution(span);
            if (index != 0)
            {
                separator.Bytes.CopyTo(result.AsSpan(offset));
                offset += separator.Length;
            }
            var part = (PyBytes)parts[index];
            part.Bytes.CopyTo(result.AsSpan(offset));
            offset += part.Length;
        }
        return new PyBytes(result, context.MemoryGovernor, span, storage);
    }
    private enum BytesPadMode
    {
        LJust,
        RJust,
        Center,
    }

    private static async ValueTask<object> PadBytesAsync(PyBytes value, string methodName, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, BytesPadMode mode, bool asynchronous)
    {
        var signature = "bytes." + methodName + "(width[, fillchar])";
        object widthArgument = PyNone.Instance;
        object? fillArgument = null;
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
                widthArgument = argument.Value;
            }
            else if (positionals == 2)
            {
                fillArgument = argument.Value;
            }
        }

        if (positionals is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", signature + " expects one or two arguments.", span);
        }

        var width = NormalizeTextWidth(await CoerceIoIntegerAsync(widthArgument, span, context, asynchronous).ConfigureAwait(false), span);
        var fill = (byte)' ';
        if (fillArgument is not null)
        {
            if (fillArgument is not PyBytes fillBytes || fillBytes.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", fillArgument is not PyBytes ? signature + " expects fillchar to be a bytes-like object." : "The fill character must be exactly one byte long", span);
            }

            fill = fillBytes.Bytes[0];
        }

        var source = value.Bytes;
        var padding = width - source.Length;
        if (padding <= 0)
        {
            return value;
        }

        var left = mode switch
        {
            BytesPadMode.LJust => 0,
            BytesPadMode.RJust => padding,
            _ => padding / 2 + ((padding & width & 1) == 1 ? 1 : 0),
        };
        var right = padding - left;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            AppendRepeatedBytes(builder, fill, left, context, span);
            builder.Append(source);
            AppendRepeatedBytes(builder, fill, right, context, span);
            context.CheckExecution(span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }

    private static async ValueTask<object> ZFillBytesAsync(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        object widthArgument = PyNone.Instance;
        var positionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "bytes.zfill() takes no keyword arguments", span);
            }

            positionals++;
            if (positionals == 1)
            {
                widthArgument = argument.Value;
            }
        }

        if (positionals != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes.zfill() takes exactly one argument (" + positionals + " given)", span);
        }

        var width = NormalizeTextWidth(await CoerceIoIntegerAsync(widthArgument, span, context, asynchronous).ConfigureAwait(false), span);
        var source = value.Bytes;
        if (width <= source.Length)
        {
            return value;
        }

        var prefixLength = source.Length > 0 && (source[0] == (byte)'+' || source[0] == (byte)'-') ? 1 : 0;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            builder.Append(source[..prefixLength]);
            AppendRepeatedBytes(builder, (byte)'0', width - source.Length, context, span);
            builder.Append(source[prefixLength..]);
            context.CheckExecution(span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }
    private static object ExpandBytesTabs(PyBytes value, int tabSize, ExecutionContext context, LythonSourceSpan span)
    {
        var source = value.Bytes;
        if (source.IsEmpty)
        {
            return value;
        }

        if (tabSize < 0)
        {
            tabSize = 0;
        }

        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            long column = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var octet = source[i];
                if (octet == (byte)'\t')
                {
                    var spaces = tabSize == 0 ? 0 : (int)(tabSize - (column % tabSize));
                    AppendRepeatedBytes(builder, (byte)' ', spaces, context, span);
                    column += spaces;
                }
                else
                {
                    builder.Append(octet);
                    column = octet is 10 or 13 ? 0 : column + 1;
                }
            }

            context.CheckExecution(span);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }
    private static void AppendRepeatedBytes(GovernedByteBuilder builder, byte value, int count, ExecutionContext context, LythonSourceSpan span)
    {
        while (count > 0)
        {
            context.CheckExecution(span);
            var chunk = Math.Min(count, 4096);
            builder.AppendRepeated(value, chunk);
            count -= chunk;
        }
    }

    private sealed class RawBoundCallable(
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation,
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation = null) : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue, IPyRawBoundCallable
    {
        public string? BoundName { get; init; }

        public object? BoundReceiver { get; init; }

        // Named bound shapes render like CPython built-in methods minus the
        // address suffix; anonymous shapes keep the legacy rendering.
        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString(
                BoundName is not null && UnboundTypeMethod.IsPlainBoundMethodName(BoundName)
                    ? UnboundTypeMethod.BoundEngineMethodDisplay(ShortMethodName(BoundName), BoundReceiver, context.Context)
                    : "<object>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            return implementation(arguments, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (asyncImplementation is null)
            {
                return implementation(arguments, span, context);
            }

            return await asyncImplementation(arguments, span, context).ConfigureAwait(false);
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
