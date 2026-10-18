namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Private list storage and rollback stay owned through result registration.
    internal sealed class NativeListOperation : IDisposable, IAsyncDisposable
    {
        private const long ShellBytes = 128;
        private readonly MemoryGovernor _governor;
        internal object Result = PyNone.Instance;
        internal IEnumerator<object>? Iterator;
        internal IAsyncEnumerator<object>? AsyncIterator;
        private long _funding;
        private bool _unpublishedList;

        private NativeListOperation(ExecutionContext context)
        {
            _governor = context.MemoryGovernor;
            _funding = ShellBytes;
        }

        internal static NativeListOperation Create(ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            try
            {
                return new NativeListOperation(context);
            }
            catch { context.MemoryGovernor.ReleaseReserved(ShellBytes); throw; }
        }

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
        }

        internal async ValueTask FinishIterationAsync()
        {
            try
            {
                if (AsyncIterator is not null) await AsyncIterator.DisposeAsync().ConfigureAwait(false);
            }
            finally { AsyncIterator = null; }
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
            var privateList = _unpublishedList ? (PyList)Result : null;
            Result = PyNone.Instance;
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
