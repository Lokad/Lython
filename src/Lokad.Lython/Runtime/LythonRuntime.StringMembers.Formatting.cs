using System.Numerics;
using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private static PyString? JoinPreparedPositionalText(PyStringOps.FormatPart[] parts,
            CallArgumentValue[] arguments, ExecutionContext context, LythonSourceSpan span)
        {
            long length = 0;
            foreach (var part in parts)
            {
                context.CheckExecution(span);
                if (part.Field is { } field)
                {
                    if ((ulong)field.Position >= (ulong)arguments.Length ||
                        arguments[(int)field.Position].Value is not PyString text) return null;
                    length += text.ByteLength;
                }
                else length += part.Literal.Length;
                if (length > Array.MaxLength) return null;
            }
            if (length == 0) return PyString.Empty;

            // Every referenced value is immutable exact text. No field work or
            // argument mutation can intervene between sizing and the copy.
            var count = (int)length;
            var bytes = PyString.EstimateApproximateBytes(count);
            var governor = context.MemoryGovernor;
            governor.Reserve(bytes, span);
            try
            {
                context.CheckExecution(span);
                var result = PyString.CreateReservedUtf8(count, governor);
                var destination = result.UnpublishedUtf8;
                foreach (var part in parts)
                {
                    context.CheckExecution(span);
                    var source = part.Field is { } field
                        ? ((PyString)arguments[(int)field.Position].Value).Utf8Span
                        : part.Literal.Span;
                    PyStringOps.CopyJoinBytes(source, destination, context, span);
                    destination = destination[source.Length..];
                }
                result.CompleteUnpublishedUtf8(count);
                governor.Commit(bytes);
                return result;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }

        // One invocation owns its arguments and expanded-spec scratch.
        // Cached layouts retain none of these values or the execution token.
        private sealed class MethodFormatOperation(CallArgumentValue[] arguments, int positionalCount,
            int[]? positionalIndices, Dictionary<string, int>? keywords,
            object? mapping, LythonSourceSpan span, ExecutionContext context, bool asynchronous, int templateBytes)
        {
            private MemoryGovernor.TemporaryMemoryReservation? _metadata;
            private long _metadataBytes;

            internal async ValueTask<object> RunAsync(PyString text, PyStringOps.FormatTemplate template,
                MemoryGovernor.TemporaryMemoryReservation? indexStorage = null)
            {
                using var indices = indexStorage;
                context.CheckExecution(span);
                var output = new GovernedByteBuilder(context.MemoryGovernor, span,
                    checked(text.ByteLength + positionalCount * 8));
                try
                {
                    await AppendTemplateAsync(template, output).ConfigureAwait(false);
                    context.CheckExecution(span);
                    return output.ToPyStringAndRelease();
                }
                catch (InvalidOperationException error) { throw new LythonRuntimeException("ValueError", error.Message, span); }
                catch (IndexOutOfRangeException error) { throw new LythonRuntimeException("IndexError", error.Message, span); }
                catch (KeyNotFoundException error)
                {
                    throw new LythonRuntimeException("KeyError", error.Message, span, null,
                        PyString.FromString(error.Message, context.MemoryGovernor, span));
                }
                finally
                {
                    try { output.Release(); }
                    finally { _metadata?.Dispose(); }
                }
            }

            private object PositionalValue(int index) => arguments[positionalIndices?[index] ?? index].Value;

            private async ValueTask AppendTemplateAsync(PyStringOps.FormatTemplate template, GovernedByteBuilder output)
            {
                foreach (var part in template.Parts)
                {
                    context.CheckExecution(span);
                    if (part.Error is { } error) throw new InvalidOperationException(error);
                    if (part.Field is not { } field)
                    {
                        output.Append(part.Literal.Span);
                        continue;
                    }

                    object selected;
                    if (field.Keyword is not { } keyword)
                    {
                        if (mapping is not null) throw new InvalidOperationException("Format string contains positional fields");
                        var position = field.Position;
                        if (position >= positionalCount)
                            throw new IndexOutOfRangeException($"Replacement index {position} out of range for positional args tuple");
                        selected = PositionalValue((int)position);
                    }
                    else if (mapping is not null)
                    {
                        var key = PyString.FromString(keyword, context.MemoryGovernor, span);
                        selected = asynchronous
                            ? await ReadLoweredSubscriptAsync(mapping, key, span, context).ConfigureAwait(false)
                            : ReadSubscriptValue(mapping, key, span, context);
                    }
                    else if (keywords is not null && keywords.TryGetValue(keyword, out var argument)) selected = arguments[argument].Value;
                    else throw new KeyNotFoundException(keyword);
                    context.CheckExecution(span);

                    if (field.Accesses.Length != 0)
                    {
                        foreach (var access in field.Accesses)
                        {
                            context.CheckExecution(span);
                            switch (access.Kind)
                            {
                                case PyStringOps.FormatAccessKind.Member:
                                {
                                    var member = (string)access.Value!;
                                    var resolved = asynchronous
                                        ? await TryResolveRuntimeMemberAsync(selected, member, context, span).ConfigureAwait(false)
                                        : PyMemberAccess.TryResolve(selected, member, context, span, out var found)
                                            ? (true, found) : (false, PyNone.Instance);
                                    if (!resolved.Item1)
                                        throw PyMemberAccess.CreateMissingMemberError(selected, member, span, context);
                                    selected = resolved.Item2;
                                    break;
                                }
                                case PyStringOps.FormatAccessKind.Error:
                                    throw new InvalidOperationException((string)access.Value!);
                                default:
                                {
                                    object key;
                                    if (access.Kind == PyStringOps.FormatAccessKind.StringIndex)
                                    {
                                        var preparedKey = (PyString)access.Value!;
                                        if (selected is PyDict dictionary &&
                                            dictionary.TryReadNativeStringValue(preparedKey, context, span, out var value))
                                        {
                                            selected = value;
                                            break;
                                        }
                                        // Generic subscript protocols and errors can expose the lookup key.
                                        key = PyString.CopyFromUtf8(preparedKey.Utf8Span, context.MemoryGovernor, span, context);
                                    }
                                    else key = new BigInteger(access.Index);
                                    selected = asynchronous
                                        ? await ReadLoweredSubscriptAsync(selected, key, span, context).ConfigureAwait(false)
                                        : ReadSubscriptValue(selected, key, span, context);
                                    break;
                                }
                            }
                        }
                        context.CheckExecution(span);
                    }

                    var conversion = field.Conversion;
                    if (conversion is { Value: not ('s' or 'r' or 'a') })
                    {
                        var scalar = conversion.Value.Value;
                        var display = scalar is > 32 and < 127 ? conversion.ToString()
                            : "\\x" + scalar.ToString("x", CultureInfo.InvariantCulture);
                        throw new InvalidOperationException($"Unknown conversion specifier {display}");
                    }

                    var spec = field.Spec;
                    if (field.RecursiveSpec) throw new InvalidOperationException("Max string recursion exceeded");
                    if (field.NestedSpec is { } nestedTemplate)
                    {
                        var nested = new GovernedByteBuilder(context.MemoryGovernor, span);
                        try
                        {
                            await AppendTemplateAsync(nestedTemplate, nested).ConfigureAwait(false);
                            AdmitExpandedSpec(nested.Length);
                            spec = Encoding.UTF8.GetString(nested.WrittenSpan);
                        }
                        finally { nested.Release(); }
                    }

                    var character = conversion is { } rune ? (char?)rune.Value : null;
                    if (selected is PyInstance instance)
                    {
                        var rendered = await FormatInstanceFieldAsync(instance, character, spec, context, span,
                            asynchronous).ConfigureAwait(false);
                        context.CheckExecution(span);
                        output.Append(rendered);
                    }
                    else AppendInterpolatedStringPart(output, selected, character, spec, context, span, field.PreparedSpec);
                    context.CheckExecution(span);
                }
            }

            private void AdmitExpandedSpec(int length)
            {
                var bytes = checked(512L + 8L * templateBytes + 32L + 2L * length);
                if (_metadata is null)
                {
                    _metadata = context.MemoryGovernor.ReserveTemporary(bytes, span);
                    _metadataBytes = bytes;
                }
                else if (bytes > _metadataBytes)
                {
                    _metadata.Grow(bytes - _metadataBytes, span);
                    _metadataBytes = bytes;
                }
            }
        }
    }
}
