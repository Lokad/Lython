using Lokad.Lython.Runtime.Text;

using System.Numerics;
using System.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class StringAffixFormatMemberProvider : IStringMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
        private static readonly LythonCallableSignature StrPartitionSignature = LythonCallableSignature.Create("str.partition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private static readonly LythonCallableSignature StrRpartitionSignature = LythonCallableSignature.Create("str.rpartition", ["sep"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            private static readonly LythonCallableSignature StrRemovePrefixSignature = LythonCallableSignature.Create("str.removeprefix", ["prefix"]);
            private static readonly LythonCallableSignature StrRemoveSuffixSignature = LythonCallableSignature.Create("str.removesuffix", ["suffix"]);
            private static readonly LythonCallableSignature StrFormatMapSignature = LythonCallableSignature.Create("str.format_map", ["mapping"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            public static readonly StringAffixFormatMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "removeprefix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.removeprefix(prefix) expects one string argument.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var prefix))
                        {
                            // CPython numbers no argument here and spells a None value bare.
                            var prefixType = arguments[0] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments[0]);
                            throw new LythonRuntimeException("TypeError", $"removeprefix() argument must be str, not {prefixType}", span);
                        }

                        return prefix.Length != 0 && text.StartsWith(prefix)
                            ? text.SliceByByteRange(prefix.ByteLength, text.ByteLength,
                                text.OwnerMemoryGovernor ?? context.MemoryGovernor, span, context)
                            : text;
                    }, StrRemovePrefixSignature),
                    "removesuffix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.removesuffix(suffix) expects one string argument.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var suffix))
                        {
                            var suffixType = arguments[0] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments[0]);
                            throw new LythonRuntimeException("TypeError", $"removesuffix() argument must be str, not {suffixType}", span);
                        }

                        return suffix.Length != 0 && text.EndsWith(suffix)
                            ? text.SliceByByteRange(0, text.ByteLength - suffix.ByteLength,
                                text.OwnerMemoryGovernor ?? context.MemoryGovernor, span, context)
                            : text;
                    }, StrRemoveSuffixSignature),
                    "partition" => CreatePartitionMethod("partition", PyStringOps.Partition, StrPartitionSignature),
                    "rpartition" => CreatePartitionMethod("rpartition", PyStringOps.RPartition, StrRpartitionSignature),
                    "format" => new MethodFormatCallable(text),
                    "format_map" => CreateFormatMappingMethod(),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                ICallable CreateFormatMappingMethod()
                {
                    PyStringOps.FormatTemplate? prepared = null;
                    return BoundCallable.Create(
                        (arguments, span, context) => FormatMappingAsync(text, arguments[0], span, context, false,
                            PrepareFormat(text, ref prepared, span, context)).GetAwaiter().GetResult(),
                        StrFormatMapSignature,
                        (arguments, span, context) => FormatMappingAsync(text, arguments[0], span, context, true,
                            PrepareFormat(text, ref prepared, span, context)));
                }

                BoundCallable CreatePartitionMethod(
                    string methodName,
                    Func<PyString, PyString, MemoryGovernor, LythonSourceSpan?, PyTuple> operation,
                    LythonCallableSignature signature)
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}(sep) expects one string argument.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var separator))
                        {
                            throw new LythonRuntimeException("TypeError", "must be str, not " + RuntimeErrors.OperandTypeName(arguments[0]), span);
                        }

                        try
                        {
                            return operation(text, separator, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, signature);
            }
        }

        private static ValueTask<object> FormatStringAsync(PyString text, MethodFormatArguments arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous, PyStringOps.FormatTemplate prepared)
        {
            var positionalCount = arguments.Length;
            var firstKeyword = arguments.Length;
            if (!arguments.KnownPositional)
            {
                for (var i = 0; i < arguments.Length; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    if (arguments.KeywordAt(i) is null) continue;
                    positionalCount--;
                    if (firstKeyword == arguments.Length) firstKeyword = i;
                }
            }

            var keywordCount = arguments.Length - positionalCount;
            if (keywordCount == 0 && prepared.IsPositionalText &&
                JoinPreparedPositionalText(prepared.Parts, arguments, context, span) is { } joined)
                return new(joined);

            var interleaved = firstKeyword < positionalCount;
            var indexBytes = (interleaved ? 32L + 4L * positionalCount : 0) +
                (keywordCount != 0 ? 64L + 64L * keywordCount : 0);
            var indexStorage = indexBytes == 0 ? null : context.MemoryGovernor.ReserveTemporary(indexBytes, span);
            try
            {
                context.CheckExecution(span);
                var positionalIndices = interleaved ? new int[positionalCount] : null;
                Dictionary<string, int>? keywords = null;
                if (keywordCount != 0)
                {
                    keywords = new Dictionary<string, int>(keywordCount, StringComparer.Ordinal);
                    var positionalIndex = 0;
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        if ((i & 1023) == 0) context.CheckExecution(span);
                        var keyword = arguments.KeywordAt(i);
                        if (keyword is null)
                        {
                            if (positionalIndices is not null) positionalIndices[positionalIndex++] = i;
                        }
                        else if (!keywords.TryAdd(keyword, i))
                            throw CallErrors.MultipleValues(PythonCallableKind.Method, "str.format", keyword, span);
                    }
                }

                // The caller owns the original arguments until formatting and
                // all its callbacks complete. Only the indices need a new lease.
                return new MethodFormatOperation(arguments, positionalCount, positionalIndices, keywords, null,
                    span, context, asynchronous, text.ByteLength).RunAsync(text, prepared, indexStorage);
            }
            catch { indexStorage?.Dispose(); throw; }
        }

        private static ValueTask<object> FormatMappingAsync(PyString text, object mapping,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous, PyStringOps.FormatTemplate prepared)
            => new MethodFormatOperation(default, 0, null, null, mapping, span, context, asynchronous, text.ByteLength)
                .RunAsync(text, prepared);

        private sealed class StringTranslateMemberProvider : IStringMemberProvider
        {
            public static readonly StringTranslateMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                // translate() takes its table positionally (keywords fail
                // explicitly), so it rides a raw callable like dict.update.
                if (name == "translate")
                {
                    value = new RawBoundCallable((arguments, span, context) => TranslateString(text, arguments, span, context)) { BoundName = "str.translate", BoundReceiver = text };
                    return true;
                }

                value = PyNone.Instance;
                return false;
            }
        }

        private static object TranslateString(PyString text, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "str.translate() takes no keyword arguments", span);
                }
            }

            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "str.translate() takes exactly one argument (" + arguments.Length + " given)", span);
            }

            var table = arguments[0].Value;
            var source = text.Utf8Span;
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                for (var offset = 0; offset < source.Length;)
                {
                    context.CheckExecution(span);
                    Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                    if (!TryLookupTranslation(table, rune.Value, context, span, out var mapped)) builder.Append(rune);
                    else
                    {
                        context.CheckExecution(span);
                        switch (mapped)
                        {
                            case PyNone: break;
                            case PyString replacement: builder.Append(replacement); break;
                            case object value when Numbers.PyNumberOps.TryAsInteger(value, out var ordinal):
                                AppendTranslationOrdinal(builder, ordinal, span);
                                break;
                            default:
                                throw new LythonRuntimeException("TypeError", "character mapping must return integer, None or str", span);
                        }
                    }
                    offset += consumed;
                }
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        // Misses keep the character like CPython (only LookupError shapes
        // are swallowed); anything unindexable fails explicitly.
        private static bool TryLookupTranslation(object? table, int ordinal, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (table is PyDict dict)
            {
                return dict.TryGetValue(new BigInteger(ordinal), out value);
            }

            if (table is PyDefaultDict defaultdict)
            {
                try
                {
                    value = defaultdict.GetOrCreate(new BigInteger(ordinal), context, span);
                    return true;
                }
                catch (LythonRuntimeException ex) when (ex.ExceptionType is "KeyError" or "IndexError" or "LookupError")
                {
                    value = PyNone.Instance;
                    return false;
                }
            }

            // User mappings run the __getitem__ protocol like CPython:
            // LookupError misses keep the character, other failures (and
            // non-callable slots) propagate with their texts.
            if (table is PyInstance userMapping)
            {
                if (!userMapping.TryGetAttribute("__getitem__", context, span, out var member) || member is null)
                {
                    throw new LythonRuntimeException("TypeError", "'" + userMapping.Type.Name + "' object is not subscriptable", span);
                }

                if (member is not ICallable getter)
                {
                    throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(member, context) + "' object is not callable", span);
                }

                try
                {
                    value = getter.Invoke([CallArgumentValue.Positional(new BigInteger(ordinal))], span, context);
                    return true;
                }
                catch (LythonRuntimeException ex) when (ex.ExceptionType is "KeyError" or "IndexError" or "LookupError")
                {
                    value = PyNone.Instance;
                    return false;
                }
            }

            if (TryLookupSequenceItem(table, ordinal, out value))
            {
                return true;
            }

            if (table is PyString or PyList or PyTuple or PyBytes)
            {
                value = PyNone.Instance;
                return false;
            }

            throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(table, context) + "' object is not subscriptable", span);
        }

        private static bool TryLookupSequenceItem(object? table, int ordinal, [MaybeNullWhen(false)] out object value)
        {
            switch (table)
            {
                case PyString text when ordinal >= 0 && ordinal < text.Length:
                    value = text.Index(ordinal);
                    return true;
                case PyList list when ordinal >= 0 && ordinal < list.Count:
                    value = list[ordinal];
                    return true;
                case PyTuple tuple when ordinal >= 0 && ordinal < tuple.Count:
                    value = tuple[ordinal];
                    return true;
                case PyBytes bytes when ordinal >= 0 && ordinal < bytes.Length:
                    value = new BigInteger(bytes.Bytes[ordinal]);
                    return true;
                default:
                    value = PyNone.Instance;
                    return false;
            }
        }

        private static void AppendTranslationOrdinal(GovernedByteBuilder builder, BigInteger ordinal, LythonSourceSpan span)
        {
            if (ordinal < 0 || ordinal > 0x10FFFF)
            {
                throw new LythonRuntimeException("ValueError", "character mapping must be in range(0x110000)", span);
            }

            if (!Rune.IsValid((int)ordinal))
                throw new LythonRuntimeException("ValueError", "character mapping must return a valid Unicode scalar value", span);
            builder.Append(new Rune((int)ordinal));
        }
    }
}
