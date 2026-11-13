using System.Xml;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class XmlModule : PyModule
    {
        internal static readonly XmlModule Instance = new("xml", "etree");
        internal static readonly XmlModule Etree = new("xml.etree", "ElementTree");
        private readonly string _child;
        private XmlModule(string name, string child) : base(name) => _child = child;
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name == _child
                ? Name == "xml" ? Etree : ElementTreeModule.Instance
                : MissingMemberValue.Instance;
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal sealed partial class ElementTreeModule : PyModule
    {
        internal static readonly ElementTreeModule Instance = new();
        internal static readonly PyBuiltinRuntimeType ElementType = new("xml.etree.ElementTree.Element",
            static (_, span, _) => throw new LythonRuntimeException("NotImplementedError",
                "Element constructors and tree mutation are not supported; use fromstring.", span));
        private static readonly ExceptionTypeValue ParseErrorType =
            new(ModuleException("xml.etree.ElementTree", "ParseError"));
        private static readonly LythonCallableSignature GetSignature =
            LythonCallableSignature.Create("Element.get", ["key", "default"], requiredCount: 1);
        private static readonly LythonCallableSignature FindSignature =
            LythonCallableSignature.Create("Element.find", ["path", "namespaces"], requiredCount: 1);
        private static readonly LythonCallableSignature FindAllSignature =
            LythonCallableSignature.Create("Element.findall", ["path", "namespaces"], requiredCount: 1);
        private static readonly LythonCallableSignature IterSignature =
            LythonCallableSignature.Create("Element.iter", ["tag"], requiredCount: 0);
        private const int MaximumDepth = 1024;

        private ElementTreeModule() : base("xml.etree.ElementTree") { }
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fromstring" => BuiltinCallable.Create(LythonKnownCallableSignatures.ElementTreeFromString, FromString),
                "ParseError" => ParseErrorType,
                "Element" => ElementType,
                "parse" or "iterparse" or "XMLParser" or "TreeBuilder" or "ElementTree" or
                "SubElement" or "tostring" or "tostringlist" or "fromstringlist" or
                "XML" or "XMLID" or "Comment" or "ProcessingInstruction" or
                "register_namespace" or "canonicalize" or "indent" =>
                    BuiltinCallable.CreateUnsupported("xml.etree.ElementTree." + name),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static LythonRuntimeException ParseError(string message, LythonSourceSpan span, Exception? inner = null)
            => new(ModuleException("xml.etree.ElementTree", "ParseError"), message, span, inner, null);

        // Every graph value is independently tracked. A child, attribute map or
        // text alias can outlive the root without retaining an XML DOM or parent
        // links. Failure rolls back only these unpublished values, never input.
        private sealed class GraphConstruction : IDisposable
        {
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly MemoryGovernor.TemporaryMemoryReservation _scratch;
            private readonly List<object> _values = new();
            private long _wrapperBytes;
            private bool _published;
            internal GraphConstruction(ExecutionContext context, LythonSourceSpan span)
            {
                _context = context;
                _span = span;
                _scratch = context.MemoryGovernor.ReserveTemporary(64, span);
            }
            private void FundSlot()
            {
                if (_values.Count == _values.Capacity)
                    _scratch.Grow(8L * (_values.Capacity == 0 ? 4 : _values.Capacity), _span);
            }
            internal T Create<T>(Func<T> create) where T : class
            {
                FundSlot();
                var shellBytes = typeof(T) == typeof(PyDict) || typeof(T) == typeof(PyList) ? 256L : 0;
                using var shell = _context.MemoryGovernor.ReserveTemporary(shellBytes, _span);
                var value = create();
                _values.Add(value);
                shell.Commit(shellBytes);
                _wrapperBytes += shellBytes;
                if (value is PyString text) _context.CheckExecution(_span);
                return value;
            }
            internal PyString Text(string text)
                => text.Length == 0 ? PyString.Empty : Create(() => CreateString(text, _context, _span));
            internal PyString Text(GovernedByteBuilder builder)
            {
                if (builder.Length == 0) return PyString.Empty;
                return Create(builder.ToPyStringAndRelease);
            }
            internal PyString Decode(ReadOnlyMemory<byte> bytes, TextEncodingMode mode)
                => Create(() => DecodeText(bytes, mode, _context, _span,
                    TextErrorMode.Strict, TextNewlineMode.PreserveLineFeed));
            internal void Publish() => _published = true;
            public void Dispose()
            {
                if (!_published)
                {
                    for (var index = _values.Count - 1; index >= 0; index--)
                        _context.State.MemoryGovernor.RefundUnpublishedValue(_values[index]);
                    _context.MemoryGovernor.Release(_wrapperBytes);
                }
                _values.Clear();
                _scratch.Dispose();
            }
        }

        private sealed class ParseFrame : IDisposable
        {
            internal readonly Element Element;
            internal Element? LastChild;
            internal readonly GovernedByteBuilder Content;
            private readonly MemoryGovernor.TemporaryMemoryReservation _scratch;
            internal ParseFrame(Element element, ExecutionContext context, LythonSourceSpan span)
            {
                _scratch = context.MemoryGovernor.ReserveTemporary(128, span);
                Element = element;
                Content = new(context.MemoryGovernor, span);
            }
            internal void FinishContent(GraphConstruction graph)
            {
                if (Content.Length == 0) return;
                var text = graph.Text(Content);
                if (LastChild is null) Element.Text = text;
                else LastChild.Tail = text;
            }
            public void Dispose() { Content.Release(); _scratch.Dispose(); }
        }

        private static object FromString(object[] args, LythonSourceSpan span, ExecutionContext context)
        {
            if (args.Length > 1 && args[1] is not PyNone)
                throw new LythonRuntimeException("NotImplementedError", "fromstring only supports parser=None.", span);
            if (args[0] is not PyString and not PyBytes)
                throw RuntimeErrors.Type("fromstring text must be str or bytes", span);
            context.CheckExecution(span);
            using var graph = new GraphConstruction(context, span);
            // No intermediate DOM. Reserve a conservative bound for the reader's
            // token storage, name table, namespace stack and parser buffers.
            // Guest graph storage and the conversion ledger are funded separately.
            var inputBytes = args[0] is PyString textInput ? textInput.ByteLength : ((PyBytes)args[0]).Length;
            using var parserScratch = context.MemoryGovernor.ReserveTemporary(16_384L + 24L * inputBytes, span);
            PyString source;
            try
            {
                source = args[0] is PyString text ? text : DecodeInput((PyBytes)args[0], graph, context, span);
            }
            catch (LythonRuntimeException error) when (error.ExceptionType == "UnicodeDecodeError")
            {
                throw ParseError("not well-formed (invalid encoded character)", span, error);
            }
            context.CheckExecution(span);
            using var characters = new Utf8XmlTextReader(source, context.State, span);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = false,
                CheckCharacters = true,
                CloseInput = false,
            };
            using var inner = XmlReader.Create(characters, settings);
            using var reader = new BoundedXmlReader(inner, context, span, MaximumDepth,
                maximum => ParseError($"XML element depth exceeds the contained limit ({maximum}).", span));
            using var stackScratch = context.MemoryGovernor.ReserveTemporary(64, span);
            var stack = new List<ParseFrame>();
            Element? root = null;
            try
            {
                while (reader.Read())
                {
                    context.CheckExecution(span);
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            context.CheckExecution(span);
                            var attributes = graph.Create(() => new PyDict(context.MemoryGovernor, span));
                            var tag = graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName));
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    context.CheckExecution(span);
                                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/") continue;
                                    context.CheckExecution(span);
                                    attributes.SetItem(graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName)),
                                        graph.Text(reader.Value));
                                }
                                while (reader.MoveToNextAttribute());
                                reader.MoveToElement();
                            }
                            var children = graph.Create(() => new PyList([], context.MemoryGovernor, span));
                            var element = graph.Create(() => new Element(tag, attributes, children, context.MemoryGovernor, span));
                            if (stack.Count == 0) root = element;
                            else
                            {
                                var parent = stack[^1];
                                parent.FinishContent(graph);
                                parent.Element.Children.Add(element);
                                parent.Element.SubtreeDepth = Math.Max(parent.Element.SubtreeDepth, 1);
                                parent.LastChild = element;
                            }
                            if (!reader.IsEmptyElement)
                            {
                                // Frame, empty builder and reference slot; capacity
                                // grows before the CLR list allocates its backing.
                                if (stack.Count == stack.Capacity)
                                    stackScratch.Grow(8L * (stack.Capacity == 0 ? 4 : stack.Capacity), span);
                                stack.Add(new ParseFrame(element, context, span));
                            }
                            break;
                        case XmlNodeType.Text:
                        case XmlNodeType.CDATA:
                        case XmlNodeType.Whitespace:
                        case XmlNodeType.SignificantWhitespace:
                            if (stack.Count > 0) stack[^1].Content.AppendString(reader.Value);
                            break;
                        case XmlNodeType.EndElement:
                            var completed = stack[^1];
                            completed.FinishContent(graph);
                            if (stack.Count > 1)
                                stack[^2].Element.SubtreeDepth = Math.Max(stack[^2].Element.SubtreeDepth,
                                    completed.Element.SubtreeDepth + 1);
                            completed.Dispose();
                            stack.RemoveAt(stack.Count - 1);
                            break;
                    }
                }
                if (root is null) throw ParseError("no element found", span);
                graph.Publish();
                return root;
            }
            catch (XmlException error)
            {
                throw ParseError(error.Message, span, error);
            }
            finally
            {
                foreach (var frame in stack) frame.Dispose();
            }
        }

        private static string ClarkName(string uri, string local) => uri.Length == 0 ? local : "{" + uri + "}" + local;

        internal static async ValueTask<object> ReadIndexAsync(Element target, object index,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (index is PySlice slice)
                return await ReadSliceAsync(target, slice.StartBound, slice.StopBound, slice.StepBound,
                    context, span, asynchronous).ConfigureAwait(false);
            index = (await CoerceTypeIndexAsync(index, context, span, asynchronous).ConfigureAwait(false)) ?? PyNone.Instance;
            return target.GetIndex(PyIndexing.NormalizeIndex(index, target.Length, span));
        }

        internal static async ValueTask<object> ReadSliceAsync(Element target, object? start, object? end,
            object? step, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            (start, end, step) = await CoerceSliceBoundsAsync(start, end, step, context, span, asynchronous).ConfigureAwait(false);
            var result = (PyList)target.GetSlice(PyIndexing.SliceIndices(target.Length, start, end, step, span));
            return result;
        }

        private static PyString DecodeInput(PyBytes input, GraphConstruction graph, ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = input.Memory;
            var data = bytes.Span;
            var utf8Offset = 0;
            TextEncodingMode mode;
            if (data.StartsWith(new byte[] { 0xff, 0xfe, 0, 0 }) ||
                data.StartsWith(new byte[] { 0, 0, 0xfe, 0xff }) ||
                data.StartsWith(new byte[] { 0, 0, 0, 0x3c }) ||
                data.StartsWith(new byte[] { 0x3c, 0, 0, 0 }))
                throw new LythonRuntimeException("NotImplementedError", "XML UTF-32 input is not supported.", span);
            if (data.StartsWith(new byte[] { 0xff, 0xfe }) || data.StartsWith(new byte[] { 0xfe, 0xff }))
                mode = TextEncodingMode.Utf16;
            else if (data.Length >= 4 && data[0] == '<' && data[1] == 0)
                mode = TextEncodingMode.Utf16LittleEndian;
            else if (data.Length >= 4 && data[0] == 0 && data[1] == '<')
                mode = TextEncodingMode.Utf16BigEndian;
            else
            {
                mode = TextEncodingMode.Utf8Bom;
                var offset = data.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
                utf8Offset = offset;
                bytes = bytes[offset..];
                if (data[offset..].StartsWith("<?xml"u8))
                {
                    var end = data[offset..].IndexOf("?>"u8);
                    if (end >= 0)
                    {
                        // Declaration storage stays inside the parser reservation.
                        var declaration = System.Text.Encoding.ASCII.GetString(data.Slice(offset, end + 2));
                        if (DeclaredEncoding(declaration) is { } name)
                            mode = DeclarationEncoding(name, span);
                    }
                }
                if (offset != 0 && mode == TextEncodingMode.Utf8Bom) mode = TextEncodingMode.Utf8;
            }
            context.CheckExecution(span);
            var decoded = mode is TextEncodingMode.Utf8 or TextEncodingMode.Utf8Bom
                && ReferenceEquals(input.OwnerMemoryGovernor, context.MemoryGovernor)
                ? graph.Create(() => DecodeUtf8Bytes(input, utf8Offset, context, span, TextNewlineMode.PreserveLineFeed))
                : graph.Decode(bytes, mode);
            if (IsUtf16Encoding(mode) && DeclaredEncoding(decoded.AsString().TrimStart('\ufeff')) is { } declared)
            {
                var declarationMode = DeclarationEncoding(declared, span);
                if (declarationMode != TextEncodingMode.Utf16)
                    throw ParseError("encoding specified in XML declaration is incorrect", span);
            }
            return decoded;
        }

        private static TextEncodingMode DeclarationEncoding(string name, LythonSourceSpan span)
        {
            // Expat's native Unicode encodings use these exact spellings
            // (case-insensitively). Codec aliases go through its single-byte
            // map: UTF-8 aliases consequently accept ASCII, rejecting high
            // bytes rather than interpreting them as UTF-8 sequences.
            if (name.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) return TextEncodingMode.Utf8;
            if (name.Equals("utf-16", StringComparison.OrdinalIgnoreCase)) return TextEncodingMode.Utf16;
            if (name.Equals("utf_8", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("cp65001", StringComparison.OrdinalIgnoreCase)) return TextEncodingMode.Ascii;
            if (name.Equals("iso8859-1", StringComparison.OrdinalIgnoreCase)) return TextEncodingMode.Latin1;
            var mode = ParseTextEncoding(PyString.FromString(name), "XML", span);
            if (IsUtf16Encoding(mode))
                throw new LythonRuntimeException("ValueError", "multi-byte encodings are not supported", span);
            return mode is TextEncodingMode.Utf8 or TextEncodingMode.Utf8Bom ? TextEncodingMode.Ascii : mode;
        }

        private static string? DeclaredEncoding(string source)
        {
            if (!source.StartsWith("<?xml", StringComparison.Ordinal)) return null;
            var end = source.IndexOf("?>", StringComparison.Ordinal);
            if (end < 0) return null;
            var declaration = source.AsSpan(0, end);
            var encoding = declaration.IndexOf("encoding", StringComparison.Ordinal);
            if (encoding < 0) return null;
            var cursor = encoding + 8;
            while (cursor < declaration.Length && char.IsWhiteSpace(declaration[cursor])) cursor++;
            if (cursor == declaration.Length || declaration[cursor++] != '=') return null;
            while (cursor < declaration.Length && char.IsWhiteSpace(declaration[cursor])) cursor++;
            if (cursor == declaration.Length || declaration[cursor] is not ('\'' or '"')) return null;
            var quote = declaration[cursor++];
            var stop = declaration[cursor..].IndexOf(quote);
            return stop < 0 ? null : declaration.Slice(cursor, stop).ToString();
        }

        internal sealed class Element : IPyMutableDynamicAttributes, IPyIterableValue, IPyIndexableValue,
            IPySizedValue, IPyTruthyValue, IPyOwnershipSnapshot
        {
            private const long ShellBytes = 128;
            internal readonly PyString Tag;
            internal PyDict Attributes;
            internal readonly PyList Children;
            internal object Text = PyNone.Instance;
            internal object Tail = PyNone.Instance;
            // Parsing establishes this upper bound. Supported mutation only
            // removes descendants, so ancestor bounds can remain conservative.
            internal int SubtreeDepth;
            internal Element(PyString tag, PyDict attributes, PyList children, MemoryGovernor governor, LythonSourceSpan span)
            {
                governor.Reserve(ShellBytes, span);
                Tag = tag;
                Attributes = attributes;
                Children = children;
                OwnerMemoryGovernor = governor;
                AllocationSpan = span;
                governor.Commit(ShellBytes);
            }
            public MemoryGovernor OwnerMemoryGovernor { get; }
            public LythonSourceSpan? AllocationSpan { get; }
            public int Length => Children.Count;
            public bool IsTruthy() => Length != 0;
            public bool TrySnapshotOwnership(out long bytes) { bytes = ShellBytes; return true; }
            public IEnumerable<object> Iterate() => Children;
            public object GetIndex(int index) => Children[index];
            public object GetSlice(IEnumerable<int> indices)
                => new PyList(indices.Select(index => Children[index]), OwnerMemoryGovernor, AllocationSpan);
            public bool TrySetMember(string name, object value)
            {
                if (name is "tag" or "text" or "tail" or "attrib")
                    throw new LythonRuntimeException("NotImplementedError", "Element field assignment is not supported.", AllocationSpan);
                return false;
            }
            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "tag" => Tag,
                    "text" => Text,
                    "tail" => Tail,
                    "attrib" => Attributes,
                    "__class__" => ElementType,
                    "get" => BoundCallable.Create((args, span, context) =>
                        DictMembers.Get(Attributes, args[0], args.Length > 1 ? args[1] : PyNone.Instance, span, context), GetSignature,
                        (args, span, context) => DictMembers.GetAsync(Attributes, args[0],
                            args.Length > 1 ? args[1] : PyNone.Instance, span, context)),
                    "find" => BoundCallable.Create((args, span, context) => Select(args, context, span, false), FindSignature),
                    "findall" => BoundCallable.Create((args, span, context) => Select(args, context, span, true), FindAllSignature),
                    "iter" => BoundCallable.Create((args, span, context) =>
                        new TreeIterator(this, args.Length == 0 ? PyNone.Instance : args[0], false, context, span), IterSignature),
                    "itertext" => BoundCallable.CreateNoArguments(this, "Element.itertext", static (receiver, span, context) =>
                        new TreeIterator(receiver, PyNone.Instance, true, context, span)),
                    "clear" => BoundCallable.CreateNoArguments(this, "Element.clear", static (receiver, span, context) => receiver.Clear(context, span)),
                    "append" or "extend" or "insert" or "remove" or "set" or
                    "keys" or "items" or "findtext" or "iterfind" or "makeelement" =>
                        BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError",
                            "Element." + name + " is not supported.", span), "Element." + name),
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            private object Clear(ExecutionContext context, LythonSourceSpan span)
            {
                context.CheckExecution(span);
                var governor = context.MemoryGovernor;
                using var shell = governor.ReserveTemporary(256, span);
                context.CheckExecution(span);
                var replacement = new PyDict(governor, span);
                try { context.CheckExecution(span); }
                catch { governor.RefundUnpublishedValue(replacement); throw; }
                shell.Commit(256);
                // Publish the entire cleared state before Children returns its
                // private backing. Old attribute/text/child aliases keep owners.
                Attributes = replacement;
                Text = Tail = PyNone.Instance;
                SubtreeDepth = 0;
                Children.Clear();
                return PyNone.Instance;
            }

            private object Select(object[] args, ExecutionContext context, LythonSourceSpan span, bool all)
            {
                context.CheckExecution(span);
                var path = ExpectString(args[0], "Element path", span);
                var namespaces = args.Length > 1 && args[1] is not PyNone ? args[1] as PyDict : null;
                if (args.Length > 1 && args[1] is not PyNone && namespaces is null)
                    throw RuntimeErrors.Type("Element namespaces must be a dict or None.", span);
                using var scratch = context.MemoryGovernor.ReserveTemporary(128L + 64L * path.Length, span);
                if (path.Length == 0) return all ? new PyList([], context.MemoryGovernor, span) : PyNone.Instance;
                var steps = ParsePath(path, namespaces, context, span, scratch);
                using var cursor = new SelectionCursor(this, steps, context, span);
                if (!all) return cursor.TryMoveNext(out var first) ? first : PyNone.Instance;
                var result = new PyList(0, context.MemoryGovernor, span);
                try
                {
                    while (cursor.TryMoveNext(out var element)) result.Add(element);
                    return result;
                }
                catch { result.DiscardUnpublishedStorage(); throw; }
            }
        }

        private readonly record struct SelectionStep(PyString? Tag, bool Descendants);

        // A frame resumes one axis after its selected child's remaining path.
        // This preserves ElementPath's duplicate/order semantics without CLR
        // recursion or a materialized frontier for each path component.
        private sealed class SelectionCursor : IDisposable
        {
            private readonly record struct Frame(Element Parent, int Step, int NextChild);
            private readonly List<SelectionStep> _steps;
            private readonly List<Frame> _stack = [];
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly MemoryGovernor.TemporaryMemoryReservation _storage;
            private Element? _self;

            internal SelectionCursor(Element root, List<SelectionStep> steps, ExecutionContext context, LythonSourceSpan span)
            {
                _steps = steps;
                _context = context;
                _span = span;
                _storage = context.MemoryGovernor.ReserveTemporary(64, span);
                try
                {
                    if (steps.Count == 0) _self = root;
                    else Push(root, 0);
                }
                catch { Dispose(); throw; }
            }

            internal bool TryMoveNext([MaybeNullWhen(false)] out Element element)
            {
                _context.CheckExecution(_span);
                if (_self is { } self) { _self = null; element = self; return true; }
                while (_stack.Count != 0)
                {
                    _context.CheckExecution(_span);
                    var frame = _stack[^1];
                    if (frame.NextChild == frame.Parent.Children.Count) { _stack.RemoveAt(_stack.Count - 1); continue; }
                    var child = (Element)frame.Parent.Children[frame.NextChild];
                    _stack[^1] = frame with { NextChild = frame.NextChild + 1 };
                    var step = _steps[frame.Step];
                    if (step.Descendants) Push(child, frame.Step);
                    if (step.Tag is not null && !child.Tag.Equals(step.Tag)) continue;
                    if (frame.Step + 1 == _steps.Count) { element = child; return true; }
                    Push(child, frame.Step + 1);
                }
                element = null;
                return false;
            }

            private void Push(Element parent, int step)
            {
                if (_stack.Count == _stack.Capacity)
                {
                    var previous = _stack.Capacity;
                    var capacity = previous == 0 ? 4 : checked(previous * 2);
                    _storage.Grow(32L + 16L * capacity, _span);
                    _stack.Capacity = capacity;
                    if (previous != 0) _storage.Shrink(32L + 16L * previous);
                }
                _stack.Add(new(parent, step, 0));
            }

            public void Dispose()
            {
                _self = null;
                _stack.Clear();
                _stack.Capacity = 0;
                _storage.Dispose();
            }
        }

        private static List<SelectionStep> ParsePath(string path, PyDict? namespaces, ExecutionContext context, LythonSourceSpan span,
            MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var steps = new List<SelectionStep>();
            var start = 0;
            var inClark = false;
            var descendants = false;
            for (var index = 0; index <= path.Length; index++)
            {
                if ((index & 1023) == 0) context.CheckExecution(span);
                if (index < path.Length)
                {
                    var character = path[index];
                    if (character == '{') { inClark = true; continue; }
                    if (character == '}') { inClark = false; continue; }
                    if (inClark || character != '/') continue;
                }
                var step = path[start..index];
                if (index == path.Length && start == index) step = "*";
                if (step.Length == 0)
                {
                    if (index == 0)
                        throw new LythonRuntimeException("NotImplementedError", "Element paths must be relative.", span);
                    if (descendants) throw new LythonRuntimeException("SyntaxError", "invalid descendant", span);
                    descendants = true;
                    start = index + 1;
                    continue;
                }
                if (step == "..")
                    throw new LythonRuntimeException("NotImplementedError", "Element parent selection is not supported.", span);
                if (step == ".")
                {
                    if (descendants) throw new LythonRuntimeException("SyntaxError", "invalid descendant", span);
                    start = index + 1;
                    continue;
                }
                var local = step.StartsWith('{') ? step[(step.IndexOf('}') + 1)..] : step;
                if (local.IndexOfAny(['[', ']', '@', '(', ')', '|']) >= 0 ||
                    (local.Contains('*', StringComparison.Ordinal) && local != "*") ||
                    (step.StartsWith('{') && (step.StartsWith("{*}", StringComparison.Ordinal) || local == "*")))
                    throw new LythonRuntimeException("NotImplementedError", "Element XPath operators and namespace wildcards are not supported.", span);
                if (step is not "." and not "*" && !step.StartsWith('{'))
                {
                    var colon = step.IndexOf(':');
                    if (colon >= 0)
                    {
                        // CPython's direct-tag fast path does not resolve prefixes
                        // when namespaces are absent and there are no path operators.
                        if (namespaces is not null || path.IndexOfAny(['/', '.', '*', '[', '@']) >= 0)
                        {
                            var prefix = PyString.FromString(step[..colon]);
                            if (namespaces is null || !namespaces.TryGetValue(prefix, out var uri))
                                throw new LythonRuntimeException("SyntaxError", "prefix not found in prefix map", span);
                            var name = ExpectString(uri, "namespace URI", span);
                            scratch.Grow(128 + 8L * name.Length, span);
                            step = ClarkName(name, step[(colon + 1)..]);
                        }
                    }
                    else if (namespaces is not null && namespaces.TryGetValue(PyString.Empty, out var defaultUri))
                    {
                        var name = ExpectString(defaultUri, "namespace URI", span);
                        scratch.Grow(128 + 8L * name.Length, span);
                        step = ClarkName(name, step);
                    }
                }
                steps.Add(new(step == "*" ? null : PyString.FromString(step), descendants));
                descendants = false;
                start = index + 1;
            }
            if (inClark) throw new LythonRuntimeException("SyntaxError", "invalid Clark name", span);
            return steps;
        }
    }
}
