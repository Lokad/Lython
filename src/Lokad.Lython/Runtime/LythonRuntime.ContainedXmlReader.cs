using System.Xml;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Contained XML reader: consumers pull through this
    /// wrapper, so depth validation and execution-budget checks share the
    /// one parse instead of scanning the bytes twice. Snapshot loads pass
    /// no context and keep depth-only enforcement.
    /// </summary>
    private sealed class BoundedXmlReader : XmlReader
    {
        private readonly XmlReader _inner;
        private readonly ExecutionContext? _context;
        private readonly LythonSourceSpan? _span;
        private readonly int _maximumElementDepth;
        private readonly Func<int, LythonRuntimeException> _depthFailure;
        private long _readsSinceCheck;
        private bool _disposed;
        public BoundedXmlReader(XmlReader inner, ExecutionContext? context, LythonSourceSpan? span,
            int maximumElementDepth, Func<int, LythonRuntimeException> depthFailure)
        {
            _inner = inner;
            _context = context;
            _span = span;
            _maximumElementDepth = maximumElementDepth;
            _depthFailure = depthFailure;
        }
        public override bool Read()
        {
            var moved = _inner.Read();
            if (moved)
            {
                if (_inner.NodeType == XmlNodeType.Element && _inner.Depth > _maximumElementDepth)
                {
                    throw _depthFailure(_maximumElementDepth);
                }
                if (_context is not null && (++_readsSinceCheck & 1023) == 0)
                {
                    _context.CheckExecution(_span);
                }
            }
            return moved;
        }
        public override void Close() => _inner.Close();
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
        public override int AttributeCount => _inner.AttributeCount;
        public override string BaseURI => _inner.BaseURI;
        public override int Depth => _inner.Depth;
        public override bool EOF => _inner.EOF;
        public override string GetAttribute(int i) => _inner.GetAttribute(i);
        public override string? GetAttribute(string name) => _inner.GetAttribute(name);
        public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);
        public override bool HasValue => _inner.HasValue;
        public override bool IsEmptyElement => _inner.IsEmptyElement;
        public override string LocalName => _inner.LocalName;
        public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);
        public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);
        public override void MoveToAttribute(int i) => _inner.MoveToAttribute(i);
        public override bool MoveToElement() => _inner.MoveToElement();
        public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();
        public override string Name => _inner.Name;
        public override string NamespaceURI => _inner.NamespaceURI;
        public override XmlNameTable NameTable => _inner.NameTable;
        public override XmlNodeType NodeType => _inner.NodeType;
        public override string Prefix => _inner.Prefix;
        public override char QuoteChar => _inner.QuoteChar;
        public override bool ReadAttributeValue() => _inner.ReadAttributeValue();
        public override ReadState ReadState => _inner.ReadState;
        public override void ResolveEntity() => _inner.ResolveEntity();
        public override string Value => _inner.Value;
    }
}
