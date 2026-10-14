namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // The aggregate's actual evolving state, using the same active head/caller
    // link as function and executable frames. No separate activation registry.
    private sealed class SumOperation : ExecutionFrame, IDisposable, IAsyncDisposable
    {
        private const long ShellBytes = 128;
        private readonly MemoryGovernor _governor;
        private ExecutionContext? _context;
        private object _source;
        internal object Total;
        internal object Current = PyNone.Instance;
        internal IEnumerator<object>? Iterator;
        internal IAsyncEnumerator<object>? AsyncIterator;
        private long _funding;

        private SumOperation(object source, object total, ExecutionContext context)
        {
            _source = source;
            Total = total;
            _context = context;
            _governor = context.MemoryGovernor;
            _funding = ShellBytes;
        }

        internal static SumOperation Create(object source, object total, ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            try
            {
                var operation = new SumOperation(source, total, context) { ActiveCaller = context.State.ActiveExecutionRoot };
                context.State.ActiveExecutionRoot = operation;
                return operation;
            }
            catch { context.MemoryGovernor.ReleaseReserved(ShellBytes); throw; }
        }

        internal object Source => _source;

        public override void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            if (!visitor.Add(Total) || !visitor.Add(Current) || !visitor.Add(_source) ||
                !visitor.Add(Iterator) || !visitor.Add(AsyncIterator) || !visitor.Add(_context)) return;
            // Compiler enumerators may contain additional unmodeled working
            // references. Their unknown shape prevents complete traversal.
            base.VisitStorageChildren(visitor);
        }

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
            _context!.State.ActiveExecutionRoot = ActiveCaller;
            ActiveCaller = null;
            _context = null;
            _source = PyNone.Instance;
            Total = PyNone.Instance;
            Current = PyNone.Instance;
            _governor.ReleaseReserved(_funding);
            _funding = 0;
        }
    }
}
