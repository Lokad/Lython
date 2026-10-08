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

    internal sealed class ElementTreeModule : PyModule
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
        private const int MaximumDepth = 1024;
        private static readonly ConditionalWeakTable<object, ContainerShell> ContainerShells = new();

        // Core containers own their backing independently. Each XML-created
        // wrapper also needs a fixed coupon, including an empty attrib alias.
        // Ephemeron ownership ties that coupon to the container, without parent
        // links or a global strong reference keeping abandoned graphs alive.
        private sealed class ContainerShell : IPyOwnershipSnapshot
        {
            private const long Bytes = 256; // Wrapper, empty storage and ephemeron entry.
            internal ContainerShell(MemoryGovernor governor, LythonSourceSpan span)
            {
                governor.Reserve(Bytes, span);
                governor.Commit(Bytes);
            }
            public bool TrySnapshotOwnership(out long bytes) { bytes = Bytes; return true; }
        }

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
                var shell = typeof(T) == typeof(PyDict) || typeof(T) == typeof(PyList)
                    ? Create(() => new ContainerShell(_context.MemoryGovernor, _span)) : null;
                FundSlot();
                var value = create();
                _values.Add(value);
                _context.Services.State.CallTemporaries.TrackCallResult(value, _span);
                if (value is PyString text) _context.ObserveString(text, _span);
                if (shell is not null) ContainerShells.Add(value, shell);
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
            internal void Refresh(object value)
                => _context.Services.State.CallTemporaries.TrackCallResult(value, _span);
            internal void Publish() => _published = true;
            public void Dispose()
            {
                if (!_published)
                {
                    for (var index = _values.Count - 1; index >= 0; index--)
                        _context.Services.State.CallTemporaries.RefundUnpublishedValue(_values[index]);
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
            context.CheckExecutionBudget(span);
            using var graph = new GraphConstruction(context, span);
            // No intermediate DOM. Reserve a conservative bound for the reader's
            // UTF-16 input/token storage, name table, namespace stack and buffers.
            // Guest graph storage and the conversion ledger are funded separately.
            var inputBytes = args[0] is PyString textInput ? textInput.Utf8Bytes.Length : ((PyBytes)args[0]).Length;
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
            context.ObserveString(source, span);
            using var characters = new StringReader(source.AsString());
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
            var elements = 0;
            try
            {
                while (reader.Read())
                {
                    context.CheckExecutionBudget(span);
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            context.ObserveCollectionCount(++elements, span);
                            var attributes = graph.Create(() => new PyDict(context.MemoryGovernor, span));
                            var tag = graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName));
                            if (reader.MoveToFirstAttribute())
                            {
                                var attributeCount = 0;
                                do
                                {
                                    context.CheckExecutionBudget(span);
                                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/") continue;
                                    context.ObserveCollectionCount(++attributeCount, span);
                                    attributes.SetItem(graph.Text(ClarkName(reader.NamespaceURI, reader.LocalName)),
                                        graph.Text(reader.Value));
                                    graph.Refresh(attributes);
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
                                graph.Refresh(parent.Element.Children);
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
            index = (await IndexBound(index, context, span, asynchronous).ConfigureAwait(false)) ?? PyNone.Instance;
            return target.GetIndex(PyIndexing.NormalizeIndex(index, target.Length, span));
        }

        internal static async ValueTask<object> ReadSliceAsync(Element target, object? start, object? end,
            object? step, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            start = await IndexBound(start, context, span, asynchronous).ConfigureAwait(false);
            end = await IndexBound(end, context, span, asynchronous).ConfigureAwait(false);
            step = await IndexBound(step, context, span, asynchronous).ConfigureAwait(false);
            var result = (PyList)target.GetSlice(PyIndexing.SliceIndices(target.Length, start, end, step, span));
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            return result;
        }

        private static async ValueTask<object?> IndexBound(object? index, ExecutionContext context,
            LythonSourceSpan span, bool asynchronous)
        {
            if (index is not PyInstance instance) return index;
            if (!instance.Type.TryLookupInMro("__index__", 0, out var raw, out _)) return index;
            var callable = asynchronous
                ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(instance, raw, context, span);
            var result = asynchronous
                ? await InvokeCallableTargetAsync(callable, span, span, context,
                    () => ValueTask.FromResult(Array.Empty<CallArgumentValue>())).ConfigureAwait(false)
                : InvokeCallableTarget(callable, span, span, context, Array.Empty<CallArgumentValue>());
            if (!Lokad.Lython.Runtime.Numbers.PyNumberOps.TryAsInteger(result, out var integer))
                throw RuntimeErrors.Type("__index__ returned non-int", span);
            return integer;
        }

        private static PyString DecodeInput(PyBytes input, GraphConstruction graph, ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = input.Memory;
            var data = bytes.Span;
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
            context.CheckExecutionBudget(span);
            var decoded = graph.Decode(bytes, mode);
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
            IPySizedValue, IPyTruthyValue, IPyGovernedValue, IPyOwnershipSnapshot
        {
            private const long ShellBytes = 128;
            internal readonly PyString Tag;
            internal readonly PyDict Attributes;
            internal readonly PyList Children;
            internal object Text = PyNone.Instance;
            internal object Tail = PyNone.Instance;
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
                    "get" => BoundCallable.Create((args, _, _) =>
                        Attributes.TryGetValue(args[0], out var item) ? item : args.Length > 1 ? args[1] : PyNone.Instance, GetSignature),
                    "find" => BoundCallable.Create((args, span, context) => Select(args, context, span, false), FindSignature),
                    "findall" => BoundCallable.Create((args, span, context) => Select(args, context, span, true), FindAllSignature),
                    "append" or "extend" or "insert" or "remove" or "clear" or "set" or
                    "keys" or "items" or "iter" or "itertext" or "findtext" or "iterfind" or "makeelement" =>
                        BoundCallable.Create((_, span, _) => throw new LythonRuntimeException("NotImplementedError",
                            "Element." + name + " is not supported.", span), "Element." + name),
                    _ => MissingMemberValue.Instance,
                };
                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            private object Select(object[] args, ExecutionContext context, LythonSourceSpan span, bool all)
            {
                var path = ExpectString(args[0], "Element path", span);
                var namespaces = args.Length > 1 && args[1] is not PyNone ? args[1] as PyDict : null;
                if (args.Length > 1 && args[1] is not PyNone && namespaces is null)
                    throw RuntimeErrors.Type("Element namespaces must be a dict or None.", span);
                using var scratch = context.MemoryGovernor.ReserveTemporary(128L + 64L * path.Length, span);
                if (path.Length == 0) return all ? new PyList([], context.MemoryGovernor, span) : PyNone.Instance;
                var steps = ParsePath(path, namespaces, context, span, scratch);
                var current = new List<Element> { this };
                scratch.Grow(32, span);
                foreach (var step in steps)
                {
                    if (step == ".") continue;
                    var next = new List<Element>();
                    foreach (var parent in current)
                    {
                        foreach (Element child in parent.Children)
                        {
                            context.CheckExecutionBudget(span);
                            if (step != "*" && !child.Tag.AsString().Equals(step, StringComparison.Ordinal)) continue;
                            context.ObserveCollectionCount(next.Count + 1, span);
                            if (next.Count == next.Capacity)
                                scratch.Grow(8L * (next.Capacity == 0 ? 4 : next.Capacity), span);
                            next.Add(child);
                        }
                    }
                    current = next;
                }
                if (!all) return current.Count == 0 ? PyNone.Instance : current[0];
                var result = new PyList(current, context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                return result;
            }
        }

        private static List<string> ParsePath(string path, PyDict? namespaces, ExecutionContext context, LythonSourceSpan span,
            MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var steps = new List<string>();
            var start = 0;
            var inClark = false;
            for (var index = 0; index <= path.Length; index++)
            {
                if ((index & 1023) == 0) context.CheckExecutionBudget(span);
                if (index < path.Length)
                {
                    var character = path[index];
                    if (character == '{') { inClark = true; continue; }
                    if (character == '}') { inClark = false; continue; }
                    if (inClark || character != '/') continue;
                }
                var step = path[start..index];
                if (index == path.Length && start == index) step = "*";
                if (step.Length == 0 || step == "..")
                    throw new LythonRuntimeException("NotImplementedError", "Element paths support only relative child steps.", span);
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
                            scratch.Grow(64 + 2L * name.Length, span);
                            step = ClarkName(name, step[(colon + 1)..]);
                        }
                    }
                    else if (namespaces is not null && namespaces.TryGetValue(PyString.Empty, out var defaultUri))
                    {
                        var name = ExpectString(defaultUri, "namespace URI", span);
                        scratch.Grow(64 + 2L * name.Length, span);
                        step = ClarkName(name, step);
                    }
                }
                steps.Add(step);
                start = index + 1;
            }
            if (inClark) throw new LythonRuntimeException("SyntaxError", "invalid Clark name", span);
            return steps;
        }
    }
}
