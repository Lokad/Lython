using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One explicit traversal for glob and pathlib. Frames own private native
    // storage; published UTF-8 results have independent execution ownership.
    private sealed class PyGlobIterator : PyIteratorBase
    {
        private sealed class Frame(string path, int segment, Frame? parent, long bytes, bool recursiveChild)
        {
            internal readonly string Path = path;
            internal readonly int Segment = segment;
            internal readonly Frame? Parent = parent;
            internal readonly bool RecursiveChild = recursiveChild;
            internal long Bytes = bytes;
            internal bool Started;
            internal string[]? Names;
            internal int Next;
        }

        private readonly GlobRequest _request;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly string[] _segments;
        private readonly PyString[] _patterns;
        private readonly PyPath? _pathBase;
        private readonly bool _directoriesOnly;
        private Frame? _frame;

        internal PyGlobIterator(GlobRequest request, ExecutionContext context, LythonSourceSpan span, PyPath? pathBase = null, IReadOnlyList<string>? snapshot = null)
        {
            ChargeIteratorValue(context.MemoryGovernor, span);
            _request = request;
            _context = context;
            _span = span;
            _pathBase = pathBase;
            _directoriesOnly = request.Pattern.EndsWith("/", StringComparison.Ordinal);
            // ParseGlobArguments/PathGlob admit the retained native preparation
            // before decoding. Keep it conservatively until execution cleanup.
            var absolute = pathBase is null ? PathOps.Normalize(request.Pattern, request.RootDirectory) : request.Pattern;
            _segments = absolute.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var backing = checked(32L + 8L * _segments.Length);
            context.MemoryGovernor.Reserve(backing, span);
            context.MemoryGovernor.Commit(backing);
            _patterns = new PyString[_segments.Length];
            for (var i = 0; i < _segments.Length; i++)
                _patterns[i] = PyString.FromString(_segments[i], context.MemoryGovernor, span);
            if (request.Pattern.Length != 0) Push(pathBase is null ? "/" : request.RootDirectory, 0, false);
            if (snapshot is not null) OwnNames(_frame!, snapshot);
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = MoveNext(false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }

        public override ValueTask<PyIterationResult> TryMoveNextAsync() => MoveNext(true);

        private async ValueTask<PyIterationResult> MoveNext(bool asynchronous)
        {
            try
            {
                while (_frame is { } frame)
                {
                    _context.CheckExecution(_span);
                    using var scratch = _context.MemoryGovernor.ReserveTemporary(checked(256L + 128L * frame.Path.Length), _span);
                    if (frame.Segment == _segments.Length)
                    {
                        if (frame.Started) { Pop(); continue; }
                        frame.Started = true;
                        return Yield(frame.Path, _directoriesOnly);
                    }

                    var segment = _segments[frame.Segment];
                    var recursive = _request.Recursive && segment == "**";
                    var last = frame.Segment == _segments.Length - 1;
                    if (!frame.Started)
                    {
                        frame.Started = true;
                        if (recursive)
                        {
                            if (!last) { Push(frame.Path, frame.Segment + 1, false); continue; }
                            var skipRoot = _pathBase is null && !_request.PatternIsAbsolute &&
                                frame.Path == _request.RootDirectory && _request.Pattern.StartsWith("**", StringComparison.Ordinal);
                            if (!skipRoot) return Yield(frame.Path, _pathBase is null && !frame.RecursiveChild || _directoriesOnly);
                        }
                        else if (!HasGlobMeta(segment))
                        {
                            using var candidate = _context.MemoryGovernor.ReserveTemporary(checked(96L + 4L * (frame.Path.Length + segment.Length + 1L)), _span);
                            var next = JoinChild(frame.Path, segment);
                            var stat = asynchronous ? await _context.HostStatAsync(next, _span).ConfigureAwait(false) : _context.HostStat(next, _span);
                            if (stat.Exists && (!last || _directoriesOnly ? stat.IsDir : true))
                            {
                                if (last) return Yield(next, _directoriesOnly);
                                Push(next, frame.Segment + 1, false);
                            }
                            continue;
                        }
                    }
                    if (!recursive && !HasGlobMeta(segment)) { Pop(); continue; }

                    if (frame.Names is null)
                    {
                        var names = asynchronous ? await ListDirectoryNamesAsync(frame.Path, _context, _span).ConfigureAwait(false)
                            : ListDirectoryNames(frame.Path, _context, _span);
                        OwnNames(frame, names);
                    }
                    if (frame.Next == frame.Names!.Length) { Pop(); continue; }
                    var name = frame.Names[frame.Next++];
                    if (!ShouldConsiderGlobName(name, segment, _request.IncludeHidden)) continue;
                    // The private names capacity includes this temporary UTF-8
                    // match input; it never escapes or accumulates per pull.
                    if (!recursive && !FnMatchModule.MatchSimple(PyString.FromString(name), _patterns[frame.Segment], _context.Limits.CancellationToken)) continue;
                    using var child = _context.MemoryGovernor.ReserveTemporary(checked(96L + 4L * (frame.Path.Length + name.Length + 1L)), _span);
                    var path = JoinChild(frame.Path, name);
                    if (!recursive && last && !_directoriesOnly) return Yield(path, false);
                    var childStat = asynchronous ? await _context.HostStatAsync(path, _span).ConfigureAwait(false) : _context.HostStat(path, _span);
                    if (childStat.IsDir)
                    {
                        if (!recursive && last) return Yield(path, _directoriesOnly);
                        Push(path, recursive ? frame.Segment : frame.Segment + 1, recursive);
                    }
                    else if (recursive && last && !_directoriesOnly && childStat.Exists) return Yield(path, false);
                }
                return PyIterationResult.End;
            }
            catch
            {
                while (_frame is not null) Pop();
                throw;
            }
        }

        private void Push(string path, int segment, bool recursiveChild)
        {
            var bytes = checked(96L + 4L * path.Length);
            using var reservation = _context.MemoryGovernor.ReserveTemporary(bytes, _span);
            var frame = new Frame(path, segment, _frame, bytes, recursiveChild);
            reservation.Commit(bytes);
            _frame = frame;
        }

        private void OwnNames(Frame frame, IReadOnlyList<string> names)
        {
            long bytes = checked(32L + 8L * names.Count);
            foreach (var name in names)
            { _context.CheckExecution(_span); bytes = checked(bytes + 64L + 8L * name.Length); }
            using var reservation = _context.MemoryGovernor.ReserveTemporary(bytes, _span);
            frame.Names = names.ToArray();
            reservation.Commit(bytes);
            frame.Bytes = checked(frame.Bytes + bytes);
        }

        private void Pop()
        {
            var frame = _frame!;
            _frame = frame.Parent;
            _context.MemoryGovernor.Release(frame.Bytes);
        }

        private PyIterationResult Yield(string absolute, bool trailingSlash)
        {
            using var scratch = _context.MemoryGovernor.ReserveTemporary(checked(256L + 128L * (absolute.Length + _request.RootDirectory.Length)), _span);
            var text = _pathBase is null ? FormatGlobResult(absolute, _request)
                : RelPath(absolute, _request.RootDirectory, _request.RootDirectory);
            if (_pathBase is null && trailingSlash && !text.EndsWith("/", StringComparison.Ordinal)) text += "/";
            var value = PyString.FromString(text, _context.MemoryGovernor, _span);
            if (_pathBase is null) return PyIterationResult.Yield(value);
            var joined = PathOps.Join(_pathBase.Value, value, _context, _span);
            return PyIterationResult.Yield(OwnPathResult(joined, joined, _context.MemoryGovernor, _span, _pathBase.IsPure));
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<glob iterator>");
    }
}

