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
            public static readonly StringAffixFormatMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "removeprefix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var prefix))
                        {
                            throw new LythonRuntimeException("TypeError", "str.removeprefix(prefix) expects one string argument.", span);
                        }

                        return prefix.Length != 0 && text.StartsWith(prefix)
                            ? OwnMethodResult(SliceByByteCount(text, prefix.Utf8Bytes.Length, text.Utf8Bytes.Length - prefix.Utf8Bytes.Length), text, context.MemoryGovernor, span)
                            : text;
                    }, "str.removeprefix", ["prefix"]),
                    "removesuffix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                        {
                            throw new LythonRuntimeException("TypeError", "str.removesuffix(suffix) expects one string argument.", span);
                        }

                        return suffix.Length != 0 && text.EndsWith(suffix)
                            ? OwnMethodResult(SliceByByteCount(text, 0, text.Utf8Bytes.Length - suffix.Utf8Bytes.Length), text, context.MemoryGovernor, span)
                            : text;
                    }, "str.removesuffix", ["suffix"]),
                    "partition" => CreatePartitionMethod("partition", PyStringOps.Partition),
                    "rpartition" => CreatePartitionMethod("rpartition", PyStringOps.RPartition),
                    "format" => new CustomMethodCallable("str.format", (arguments, span, context) =>
                    {
                        try
                        {
                            var positionalCount = 0;
                            foreach (var argument in arguments)
                            {
                                if (argument.IsPositional)
                                {
                                    positionalCount++;
                                }
                            }

                            var positional = new object[positionalCount];
                            var positionalIndex = 0;
                            foreach (var argument in arguments)
                            {
                                if (argument.IsPositional)
                                {
                                    positional[positionalIndex++] = argument.Value;
                                }
                            }

                            var keywords = new Dictionary<string, object>(StringComparer.Ordinal);
                            foreach (var argument in arguments)
                            {
                                if (argument.IsPositional)
                                {
                                    continue;
                                }

                                if (!keywords.TryAdd(argument.KeywordName, argument.Value))
                                {
                                    throw CallErrors.MultipleValues(PythonCallableKind.Method, "str.format", argument.KeywordName, span);
                                }
                            }

                            return OwnMethodResult(PyStringOps.Format(text, positional, keywords, field => ResolveFormatField(field, positional, keywords, span, context)), text, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                            throw new LythonRuntimeException("IndexError", ex.Message, span);
                        }
                        catch (KeyNotFoundException ex)
                        {
                            throw new LythonRuntimeException("KeyError", ex.Message, span);
                        }
                    }),
                    "format_map" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || arguments[0] is not PyDict mapping)
                        {
                            throw new LythonRuntimeException("TypeError", "str.format_map(mapping) expects one dictionary argument.", span);
                        }

                        try
                        {
                            var positional = Array.Empty<object>();
                            var keywords = PyStringOps.ExtractStringKeyDictionary(mapping);
                            return OwnMethodResult(PyStringOps.Format(text, positional, keywords, field => ResolveFormatField(field, positional, keywords, span, context)), text, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                        catch (IndexOutOfRangeException ex)
                        {
                            throw new LythonRuntimeException("IndexError", ex.Message, span);
                        }
                        catch (KeyNotFoundException ex)
                        {
                            throw new LythonRuntimeException("KeyError", ex.Message, span);
                        }
                    }, "str.format_map", ["mapping"]),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                BoundCallable CreatePartitionMethod(
                    string methodName,
                    Func<PyString, PyString, MemoryGovernor, LythonSourceSpan?, PyTuple> operation)
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var separator))
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}(sep) expects one string argument.", span);
                        }

                        try
                        {
                            return operation(text, separator, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, $"str.{methodName}", ["sep"]);
            }
        }

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

            var positional = new List<object?>();
            foreach (var argument in arguments)
            {
                positional.Add(argument.Value);
            }

            if (positional.Count != 1)
            {
                throw new LythonRuntimeException("TypeError", "str.translate() takes exactly one argument (" + positional.Count + " given)", span);
            }

            var table = positional[0];
            var source = text.AsString();
            var builder = new StringBuilder(source.Length);
            foreach (var rune in source.EnumerateRunes())
            {
                if (!TryLookupTranslation(table, rune.Value, context, span, out var mapped))
                {
                    builder.Append(rune);
                    continue;
                }

                switch (mapped)
                {
                    case PyNone:
                        break;
                    case PyString replacement:
                        builder.Append(replacement.AsString());
                        break;
                    case BigInteger ordinal:
                        AppendTranslationOrdinal(builder, ordinal, span);
                        break;
                    case int small:
                        AppendTranslationOrdinal(builder, new BigInteger(small), span);
                        break;
                    case bool flag:
                        AppendTranslationOrdinal(builder, flag ? BigInteger.One : BigInteger.Zero, span);
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "character mapping must return integer, None or str", span);
                }
            }

            return OwnMethodResult(PyString.FromString(builder.ToString()), text, context.MemoryGovernor, span);
        }

        // Misses keep the character like CPython (only LookupError shapes
        // are swallowed); anything unindexable fails explicitly.
        private static bool TryLookupTranslation(object table, int ordinal, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
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

        private static bool TryLookupSequenceItem(object table, int ordinal, [MaybeNullWhen(false)] out object value)
        {
            switch (table)
            {
                case PyString text when ordinal >= 0 && ordinal < text.AsString().Length:
                    value = PyString.FromString(text.AsString()[ordinal].ToString());
                    return true;
                case PyList list when ordinal >= 0 && ordinal < list.Count:
                    value = list[ordinal];
                    return true;
                case PyTuple tuple when ordinal >= 0 && ordinal < tuple.Count:
                    value = tuple[ordinal];
                    return true;
                case PyBytes bytes when ordinal >= 0 && ordinal < bytes.Length:
                    value = new BigInteger(bytes.ToArray()[ordinal]);
                    return true;
                default:
                    value = PyNone.Instance;
                    return false;
            }
        }

        private static void AppendTranslationOrdinal(StringBuilder builder, BigInteger ordinal, LythonSourceSpan span)
        {
            if (ordinal < 0 || ordinal > 0x10FFFF)
            {
                throw new LythonRuntimeException("ValueError", "character mapping must be in range(0x110000)", span);
            }

            builder.Append(char.ConvertFromUtf32((int)ordinal));
        }
    }
}
