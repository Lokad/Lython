namespace Lokad.Lython.Runtime;

internal static partial class PyIteration
{
    // Actual wrapper state replaces compiler-generated forwarding iterators.
    // Upstream enumerators retain their own contract: unknown shapes still
    // prevent complete walking. No collection or guest code runs during visits.
    private sealed class CheckedEnumerator : IEnumerator<object>, IMemoryStorageEdges
    {
        private IEnumerable<object>? _source;
        private LythonRuntime.ExecutionContext? _context;
        private readonly LythonSourceSpan _span;
        private IEnumerator<object>? _upstream;
        private object _current = PyNone.Instance;
        private int _movesUntilCheckpoint;

        internal CheckedEnumerator(IEnumerable<object> source, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _source = source;
            _span = span;
            _context = context;
        }

        public object Current => _current;
        object System.Collections.IEnumerator.Current => Current;
        public void Reset() => throw new NotSupportedException();

        public bool MoveNext()
        {
            if (_source is null) return false;
            try
            {
                // Acquisition preceded the first checkpoint in the yield
                // implementation, and remains lazy until the first pull.
                _upstream ??= _source.GetEnumerator();
                if (_movesUntilCheckpoint-- == 0)
                {
                    _movesUntilCheckpoint = 63;
                    _context!.CheckExecution(_span);
                }
                if (_upstream.MoveNext())
                {
                    _current = _upstream.Current;
                    return true;
                }
                Dispose();
                return false;
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            try { _upstream?.Dispose(); }
            finally
            {
                _upstream = null;
                _source = null;
                _context = null;
                _current = PyNone.Instance;
            }
        }

        public void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            if (!visitor.Add(_source) || !visitor.Add(_current) || !visitor.Add(_upstream)) return;
            visitor.Add(_context);
        }
    }

    private sealed class AsyncSequence(object source, LythonSourceSpan span, LythonRuntime.ExecutionContext? context)
        : IAsyncEnumerable<object>, IMemoryStorageEdges
    {
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(source, span, context);

        public void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            if (visitor.Add(source)) visitor.Add(context);
        }
    }

    private sealed class AsyncEnumerator : IAsyncEnumerator<object>, IMemoryStorageEdges
    {
        private object? _source;
        private LythonRuntime.ExecutionContext? _context;
        private readonly LythonSourceSpan _span;
        private Cursor? _cursor;
        private object _current = PyNone.Instance;

        internal AsyncEnumerator(object source, LythonSourceSpan span, LythonRuntime.ExecutionContext? context)
        {
            _source = source;
            _span = span;
            _context = context;
            _cursor = source as Cursor;
        }

        public object Current => _current;

        public async ValueTask<bool> MoveNextAsync()
        {
            if (_source is null) return false;
            try
            {
                _cursor ??= _source is PyUserIterator acquired
                    ? Cursor.FromAcquired(acquired, _span, _context!)
                    : await Cursor.CreateAsync(_source, _span, _context!).ConfigureAwait(false);
                var result = await _cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (result.HasValue)
                {
                    _current = result.Value;
                    return true;
                }
                await DisposeAsync().ConfigureAwait(false);
                return false;
            }
            catch { await DisposeAsync().ConfigureAwait(false); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_cursor is not null) await _cursor.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _cursor = null;
                _source = null;
                _context = null;
                _current = PyNone.Instance;
            }
        }

        public void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            if (!visitor.Add(_source) || !visitor.Add(_current) || !visitor.Add(_cursor)) return;
            visitor.Add(_context);
        }
    }
}
