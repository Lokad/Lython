using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ElementTreeModule
    {
        // The tree can only lose children after parsing. Fund one stack from
        // its conservative depth before publication; callbacks never resize it.
        private sealed class TreeIterator : PyIteratorBase
        {
            private readonly record struct Frame(Element Node, int NextChild);
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly bool _text;
            private Frame[] _frames;
            private long _backingBytes;
            private int _depth;
            private Element? _first;
            private object? _tag;

            internal TreeIterator(Element root, object tag, bool text, ExecutionContext context, LythonSourceSpan span)
            {
                context.CheckExecution(span);
                _context = GetGlobalContext(context);
                _span = span;
                _text = text;
                var capacity = checked(root.SubtreeDepth + 1);
                var bytes = checked(32L + 16L * capacity);
                using var storage = context.MemoryGovernor.ReserveTemporary(256L + bytes, span);
                context.CheckExecution(span);
                _frames = new Frame[capacity];
                _backingBytes = bytes;
                _first = root;
                _tag = tag is PyNone || tag is PyString wildcard && wildcard.Utf8Span.SequenceEqual("*"u8) ? null : tag;
                storage.Commit(256L + bytes);
            }

            public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var result = _tag is null or PyString ? MoveNextNative() : MoveNextProtocol(false).GetAwaiter().GetResult();
                value = result.HasValue ? result.Value : null;
                return result.HasValue;
            }

            public override ValueTask<PyIterationResult> TryMoveNextAsync()
                => _tag is null or PyString ? ValueTask.FromResult(MoveNextNative()) : MoveNextProtocol(true);

            // Both host APIs keep native traversal synchronous. Only user tag
            // equality reaches the protocol loop, which may genuinely suspend.
            private PyIterationResult MoveNextNative()
            {
                while (Read(out var element, out var tail))
                {
                    if (_text)
                    {
                        var value = tail ? element.Tail : element.Text;
                        if (value is PyString { ByteLength: > 0 }) return PyIterationResult.Yield(value);
                    }
                    else if (!tail)
                    {
                        if (_tag is not PyString tag || element.Tag.Equals(tag, _context, _span))
                            return PyIterationResult.Yield(element);
                    }
                }
                return PyIterationResult.End;
            }

            private async ValueTask<PyIterationResult> MoveNextProtocol(bool asynchronous)
            {
                while (Read(out var element, out _))
                {
                    var tag = _tag;
                    if (tag is null || (asynchronous
                        ? await ElementEqualsAsync(element.Tag, tag, _context, _span).ConfigureAwait(false)
                        : ElementEquals(element.Tag, tag, _context, _span)))
                        return PyIterationResult.Yield(element);
                }
                return PyIterationResult.End;
            }

            private bool Read([MaybeNullWhen(false)] out Element element, out bool tail)
            {
                _context.CheckExecution(_span);
                tail = false;
                if (_first is { } first)
                {
                    _first = null;
                    _frames[_depth++] = new(first, 0);
                    element = first;
                    return true;
                }
                while (_depth != 0)
                {
                    _context.CheckExecution(_span);
                    var frame = _frames[_depth - 1];
                    if (frame.NextChild < frame.Node.Children.Count)
                    {
                        element = (Element)frame.Node.Children[frame.NextChild];
                        _frames[_depth - 1] = frame with { NextChild = frame.NextChild + 1 };
                        _frames[_depth++] = new(element, 0);
                        return true;
                    }
                    _frames[--_depth] = default;
                    if (_text && _depth != 0)
                    {
                        element = frame.Node;
                        tail = true;
                        return true;
                    }
                }
                // Drop private backing and references before a refund can reenter.
                _frames = [];
                _tag = null;
                var bytes = _backingBytes;
                _backingBytes = 0;
                _context.MemoryGovernor.Release(bytes);
                element = null;
                return false;
            }

            public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<xml.etree.ElementTree.Element iterator>");
        }
    }
}
