using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        // One invocation owns its arguments, numbering and expanded-spec scratch.
        // Cached layouts retain none of these values or the execution token.
        private sealed class MethodFormatOperation(CallArgumentValue[] arguments, int positionalCount,
            int[]? positionalIndices, Dictionary<string, int>? keywords,
            object? mapping, LythonSourceSpan span, ExecutionContext context, bool asynchronous, int templateBytes)
        {
            private int _autoIndex;
            private bool _sawAuto;
            private bool _sawManual;
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

                    var root = field.Root;
                    object selected;
                    if (root.Length == 0)
                    {
                        if (mapping is not null) throw new InvalidOperationException("Format string contains positional fields");
                        if (_sawManual) throw new InvalidOperationException("cannot switch from manual field specification to automatic field numbering");
                        _sawAuto = true;
                        if (_autoIndex >= positionalCount)
                            throw new IndexOutOfRangeException($"Replacement index {_autoIndex} out of range for positional args tuple");
                        selected = PositionalValue(_autoIndex++);
                    }
                    else if (PyStringOps.TryParseFormatIndex(root, out var slot, context.State.CancellationToken))
                    {
                        if (mapping is not null) throw new InvalidOperationException("Format string contains positional fields");
                        if (_sawAuto) throw new InvalidOperationException("cannot switch from automatic field numbering to manual field specification");
                        _sawManual = true;
                        if (slot >= positionalCount)
                            throw new IndexOutOfRangeException($"Replacement index {slot} out of range for positional args tuple");
                        selected = PositionalValue((int)slot);
                    }
                    else if (mapping is not null)
                    {
                        var key = PyString.FromString(root, context.MemoryGovernor, span);
                        selected = asynchronous
                            ? await ReadLoweredSubscriptAsync(mapping, key, span, context).ConfigureAwait(false)
                            : ReadSubscriptValue(mapping, key, span, context);
                    }
                    else if (keywords is not null && keywords.TryGetValue(root, out var keyword)) selected = arguments[keyword].Value;
                    else throw new KeyNotFoundException(root);
                    context.CheckExecution(span);

                    if (field.Suffix.Length != 0)
                        selected = await ResolveFormatFieldSuffixAsync(selected, field.Suffix, span, context, asynchronous).ConfigureAwait(false);
                    context.CheckExecution(span);

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
