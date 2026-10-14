namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Native working storage and rollback stay owned through result registration.
    internal sealed class NativeIterableOperation : IDisposable, IAsyncDisposable
    {
        private const long ShellBytes = 128;
        private readonly MemoryGovernor _governor;
        private object _source;
        internal object Result;
        internal object Current = PyNone.Instance;
        internal IEnumerator<object>? Iterator;
        internal IAsyncEnumerator<object>? AsyncIterator;
        private long _funding;
        private bool _unpublishedList;

        private NativeIterableOperation(object source, object result, ExecutionContext context)
        {
            _source = source;
            Result = result;
            _governor = context.MemoryGovernor;
            _funding = ShellBytes;
        }

        internal static NativeIterableOperation Create(object source, object result, ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            try
            {
                return new NativeIterableOperation(source, result, context);
            }
            catch { context.MemoryGovernor.ReleaseReserved(ShellBytes); throw; }
        }

        internal object Source => _source;

        internal void BeginList(int capacity, LythonSourceSpan span)
        {
            Result = new PyList(capacity, _governor, span);
            _unpublishedList = true;
        }

        internal void ResultRegistered() => _unpublishedList = false;

        internal void FinishIteration()
        {
            try { Iterator?.Dispose(); }
            finally { Iterator = null; }
            _source = PyNone.Instance;
            Current = PyNone.Instance;
        }

        internal async ValueTask FinishIterationAsync()
        {
            try
            {
                if (AsyncIterator is not null) await AsyncIterator.DisposeAsync().ConfigureAwait(false);
            }
            finally { AsyncIterator = null; }
            _source = PyNone.Instance;
            Current = PyNone.Instance;
        }

        public void Dispose()
        {
            if (_funding == 0) return;
            try { FinishIteration(); }
            finally { Withdraw(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (_funding == 0) return;
            try { await FinishIterationAsync().ConfigureAwait(false); }
            finally { Withdraw(); }
        }

        private void Withdraw()
        {
            _source = PyNone.Instance;
            var privateList = _unpublishedList ? (PyList)Result : null;
            Result = PyNone.Instance;
            Current = PyNone.Instance;
            _unpublishedList = false;
            try { privateList?.DiscardUnpublishedStorage(); }
            finally
            {
                _governor.ReleaseReserved(_funding);
                _funding = 0;
            }
        }
    }
}
