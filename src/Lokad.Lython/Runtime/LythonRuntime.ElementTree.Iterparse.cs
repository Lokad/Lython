using System.Text;
using System.Xml;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ElementTreeModule
    {
        private static readonly LythonCallableSignature IterparseSignature =
            LythonCallableSignature.Create("xml.etree.ElementTree.iterparse", ["source", "events", "parser"], requiredCount: 1);
        private static readonly LythonCallableSignature IterparseCloseSignature =
            LythonCallableSignature.Create("iterparse.close", []);
        private static readonly PyString StartEvent = PyString.FromString("start");
        private static readonly PyString EndEvent = PyString.FromString("end");

        private static async ValueTask<object> IterparseAsync(object[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length > 2 && arguments[2] is not PyNone)
                throw new LythonRuntimeException("NotImplementedError", "iterparse only supports parser=None.", span);
            var start = false;
            var end = true;
            if (arguments.Length > 1 && arguments[1] is not PyNone)
            {
                end = false;
                await using var cursor = asynchronous
                    ? await PyIteration.Cursor.CreateAsync(arguments[1], span, context).ConfigureAwait(false)
                    : PyIteration.Cursor.Create(arguments[1], span, context);
                while (true)
                {
                    context.CheckExecution(span);
                    var next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false)
                        : cursor.TryMoveNext(out var item) ? PyIterationResult.Yield(item) : PyIterationResult.End;
                    if (!next.HasValue) break;
                    var name = next.Value as PyString;
                    if (name is not null && name.Equals(StartEvent, context, span)) start = true;
                    else if (name is not null && name.Equals(EndEvent, context, span)) end = true;
                    else if (name is not null && name.AsString() is "start-ns" or "end-ns" or "comment" or "pi")
                        throw new LythonRuntimeException("NotImplementedError", "iterparse supports start and end events.", span);
                    else throw new LythonRuntimeException("ValueError", "unknown XML event", span);
                }
            }
            context = GetGlobalContext(context);
            using var storage = context.MemoryGovernor.ReserveTemporary(768, span);
            context.CheckExecution(span);
            var iterator = new XmlEventIterator(context, span, start, end);
            try
            {
                context.State.TrackOpenFile(iterator);
                var member = asynchronous
                    ? await TryResolveRuntimeMemberAsync(arguments[0], "read", context, span).ConfigureAwait(false)
                    : TryResolveRuntimeMember(arguments[0], "read", context, span, out var read) ? (true, read) : (false, PyNone.Instance);
                if (member.Item1) iterator._source = arguments[0];
                else
                {
                    var path = asynchronous ? await CoercePathLikeAsync(arguments[0], context, span, "iterparse()").ConfigureAwait(false)
                        : CoercePathLike(arguments[0], context, span, "iterparse()");
                    iterator._file = new OpenedByteFile(context.State);
                    await iterator._file.OpenAsync(path.AsString(), new(LythonFileAccess.Read), span, asynchronous).ConfigureAwait(false);
                }
                context.CheckExecution(span);
                storage.Commit(768);
                return iterator;
            }
            catch
            {
                try { await iterator.CloseCoreAsync(asynchronous).ConfigureAwait(false); }
                catch (Exception error) when (error is not OutOfMemoryException) { }
                throw;
            }
        }

        private sealed class XmlEventIterator : PyIteratorBase, IExecutionFile, IPyMutableDynamicAttributes
        {
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly bool _start, _end;
            internal OpenedByteFile? _file;
            internal object? _source;
            private XmlInputReader? _input;
            private BoundedXmlReader? _reader;
            private GraphConstruction? _graph;
            private TreeConstruction? _construction;
            private Element? _pendingEnd;
            private object _root = PyNone.Instance;
            private bool _closed, _stepping;

            internal XmlEventIterator(ExecutionContext context, LythonSourceSpan span, bool start, bool end)
            { _context = context; _span = span; _start = start; _end = end; }

            public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var next = StepAsync(false).GetAwaiter().GetResult();
                value = next.HasValue ? next.Value : null;
                return next.HasValue;
            }

            public override ValueTask<PyIterationResult> TryMoveNextAsync() => StepAsync(true);

            private async ValueTask<PyIterationResult> StepAsync(bool asynchronous)
            {
                if (_stepping) throw new LythonRuntimeException("ValueError", "generator already executing", _span);
                if (_closed) return PyIterationResult.End;
                _stepping = true;
                try
                {
                    _context.CheckExecution(_span);
                    if (_pendingEnd is { } pending)
                    {
                        _pendingEnd = null;
                        return Event(pending, false);
                    }
                    if (_input is null)
                    {
                        _input = new XmlInputReader(_context, _span, _file, _source);
                        _graph = new GraphConstruction(_context, _span);
                        _construction = new TreeConstruction(_context, _span, _graph);
                        _reader = CreateXmlReader(_input, _context, _span, true);
                    }
                    _input.SetExecutionMode(asynchronous);
                    while (await _reader!.ReadAsync().ConfigureAwait(false))
                    {
                        _context.CheckExecution(_span);
                        var events = await _construction!.Process(_reader, true).ConfigureAwait(false);
                        if (_start && events.Started is { } started)
                        {
                            if (_end) _pendingEnd = events.Ended;
                            return Event(started, true);
                        }
                        if (_end && events.Ended is { } ended) return Event(ended, false);
                    }
                    var root = _construction!.Complete();
                    _graph!.Publish();
                    _root = root;
                    await CloseCoreAsync(asynchronous).ConfigureAwait(false);
                    return PyIterationResult.End;
                }
                catch (Exception error)
                {
                    try { await CloseCoreAsync(asynchronous).ConfigureAwait(false); }
                    catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
                    if (error is XmlException xml) throw ParseError(xml.Message, _span, xml);
                    if (error is DecoderFallbackException || error is LythonRuntimeException { ExceptionType: "UnicodeDecodeError" })
                        throw ParseError("not well-formed (invalid encoded character)", _span, error);
                    throw;
                }
                finally { _stepping = false; }
            }

            private PyIterationResult Event(Element element, bool start)
            {
                var pair = new PyTuple([start ? StartEvent : EndEvent, element], _context.MemoryGovernor, _span);
                _graph!.Publish();
                return PyIterationResult.Yield(pair);
            }

            private ValueTask<object> CloseAsync(bool asynchronous)
            {
                if (_stepping) throw new LythonRuntimeException("ValueError", "generator already executing", _span);
                return CloseCoreAsync(asynchronous);
            }

            internal async ValueTask<object> CloseCoreAsync(bool asynchronous)
            {
                if (_closed) return PyNone.Instance;
                _closed = true;
                _context.State.UntrackOpenFile(this);
                var file = _file; _file = null;
                var input = _input; _input = null;
                var reader = _reader; _reader = null;
                var construction = _construction; _construction = null;
                var graph = _graph; _graph = null;
                _source = null; _pendingEnd = null;
                try { reader?.Dispose(); }
                finally
                {
                    try { if (file is not null) await file.CloseAsync(_span, asynchronous).ConfigureAwait(false); }
                    finally
                    {
                        try { construction?.Dispose(); }
                        finally
                        {
                            try { graph?.Dispose(); }
                            finally { input?.Dispose(); }
                        }
                    }
                }
                return PyNone.Instance;
            }

            object IExecutionFile.Exit(ExecutionContext context) => CloseCoreAsync(false).GetAwaiter().GetResult();
            ValueTask<object> IExecutionFile.ExitAsync(ExecutionContext context) => CloseCoreAsync(true);

            public bool TrySetMember(string name, object value)
            {
                if (name != "root") return false;
                _root = value;
                return true;
            }

            public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                if (name == "root") { value = _root; return true; }
                if (name == "close")
                {
                    value = BoundCallable.Create((_, _, _) => CloseAsync(false).GetAwaiter().GetResult(), IterparseCloseSignature,
                        (_, _, _) => CloseAsync(true));
                    return true;
                }
                return base.TryGetMember(name, out value);
            }

            public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<xml.etree.ElementTree.iterparse iterator>");
        }
    }
}
