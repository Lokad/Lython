using Lokad.Lython.Runtime.Text;

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

                        return text.StartsWith(prefix)
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
    }
}
