namespace Lokad.Lython.Runtime;

internal static partial class PyIteration
{
    private sealed class AsyncSequence(object source, LythonSourceSpan span, LythonRuntime.ExecutionContext? context)
        : IAsyncEnumerable<object>
    {
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new AsyncEnumerator(source, span, context);

    }

    private sealed class AsyncEnumerator : IAsyncEnumerator<object>
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

    }
}
