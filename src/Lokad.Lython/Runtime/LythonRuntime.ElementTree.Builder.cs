using System.Xml;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ElementTreeModule
    {
        private readonly record struct ElementEvents(Element? Started, Element? Ended);

        // One construction loop serves whole documents and incremental events.
        // The caller owns the construction ledger and its publication boundary.
        private sealed class TreeConstruction : IDisposable
        {
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly GraphConstruction _graph;
            private readonly MemoryGovernor.TemporaryMemoryReservation _working;
            private List<ParseFrame> _stack = new();
            internal Element? Root { get; private set; }

            internal TreeConstruction(ExecutionContext context, LythonSourceSpan span, GraphConstruction graph)
            {
                _context = context;
                _span = span;
                _graph = graph;
                _working = context.MemoryGovernor.ReserveTemporary(128, span);
            }

            internal ValueTask<ElementEvents> Process(XmlReader reader, bool asynchronous)
            {
                _context.CheckExecution(_span);
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        var attributes = _graph.Create(() => new PyDict(_context.MemoryGovernor, _span));
                        var tag = _graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName));
                        if (reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                _context.CheckExecution(_span);
                                if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/") continue;
                                if (asynchronous)
                                {
                                    var pending = reader.GetValueAsync();
                                    if (!pending.IsCompletedSuccessfully)
                                        return FinishAttributesAsync(pending, reader, attributes, tag);
                                    AddAttribute(reader, attributes, pending.Result);
                                }
                                else AddAttribute(reader, attributes, reader.Value);
                            }
                            while (reader.MoveToNextAttribute());
                            reader.MoveToElement();
                        }
                        return new(AttachElement(reader, attributes, tag));
                    case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                        if (_stack.Count != 0)
                        {
                            if (asynchronous)
                            {
                                var pending = reader.GetValueAsync();
                                if (!pending.IsCompletedSuccessfully) return AppendTextAsync(pending);
                                _stack[^1].Content.AppendString(pending.Result);
                            }
                            else _stack[^1].Content.AppendString(reader.Value);
                        }
                        return new(default(ElementEvents));
                    case XmlNodeType.EndElement:
                        var frame = _stack[^1];
                        frame.FinishContent(_graph);
                        frame.Element.SubtreeDepth = frame.ChildDepth;
                        if (_stack.Count > 1)
                            _stack[^2].ChildDepth = Math.Max(_stack[^2].ChildDepth, frame.ChildDepth + 1);
                        _stack.RemoveAt(_stack.Count - 1);
                        frame.Dispose();
                        return new(new ElementEvents(null, frame.Element));
                    default:
                        return new(default(ElementEvents));
                }
            }

            private void AddAttribute(XmlReader reader, PyDict attributes, string value)
                => attributes.SetItem(_graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName)), _graph.Text(value));

            private async ValueTask<ElementEvents> FinishAttributesAsync(Task<string> pending, XmlReader reader,
                PyDict attributes, PyString tag)
            {
                AddAttribute(reader, attributes, await pending.ConfigureAwait(false));
                while (reader.MoveToNextAttribute())
                {
                    _context.CheckExecution(_span);
                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/") continue;
                    AddAttribute(reader, attributes, await reader.GetValueAsync().ConfigureAwait(false));
                }
                reader.MoveToElement();
                return AttachElement(reader, attributes, tag);
            }

            private async ValueTask<ElementEvents> AppendTextAsync(Task<string> pending)
            {
                _stack[^1].Content.AppendString(await pending.ConfigureAwait(false));
                return default;
            }

            private ElementEvents AttachElement(XmlReader reader, PyDict attributes, PyString tag)
            {
                var children = _graph.Create(() => new PyList([], _context.MemoryGovernor, _span));
                var element = _graph.Create(() => new Element(tag, attributes, children, _context.MemoryGovernor, _span));
                if (_stack.Count == 0) Root = element;
                else
                {
                    var parent = _stack[^1];
                    parent.FinishContent(_graph);
                    parent.Element.Children.Add(element);
                    parent.LastChild = element;
                    if (reader.IsEmptyElement) parent.ChildDepth = Math.Max(parent.ChildDepth, 1);
                }
                if (reader.IsEmptyElement) return new(element, element);
                if (_stack.Count == _stack.Capacity)
                    _working.Grow(8L * (_stack.Capacity == 0 ? 4 : _stack.Capacity), _span);
                var frame = new ParseFrame(element, _context, _span);
                // The parser may add descendants after a start event, even if
                // the guest clears this node. Do not admit a too-small iterator.
                element.SubtreeDepth = reader.Depth - MaximumDepth;
                _stack.Add(frame);
                return new(element, null);
            }

            internal Element Complete()
            {
                if (Root is null) throw ParseError("no element found", _span);
                if (_stack.Count != 0) throw ParseError("unclosed element", _span);
                return Root;
            }

            public void Dispose()
            {
                var frames = _stack;
                _stack = null!;
                Root = null;
                try
                {
                    foreach (var frame in frames) frame.Dispose();
                }
                finally
                {
                    frames.Clear();
                    _working.Dispose();
                }
            }
        }
    }
}
