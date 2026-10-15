using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using ClrExecutionContext = System.Threading.ExecutionContext;

namespace Lokad.Lython.Runtime;

// Cache only idle threads. A busy worker never blocks admission of another run:
// concurrent callers and synchronous calls from host callbacks get their own
// large stack. Both the idle count and the idle lifetime are bounded.
internal sealed class ExecutionThreads : IDisposable
{
    private readonly object _gate = new();
    private readonly LinkedList<Worker> _idle = new();
    private readonly int _stackBytes;
    private readonly int _maxIdle;
    private readonly TimeSpan _idleTimeout;
    private bool _disposed;

    internal ExecutionThreads(int stackBytes, int maxIdle, TimeSpan idleTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stackBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIdle);
        if (idleTimeout <= TimeSpan.Zero || idleTimeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        _stackBytes = stackBytes;
        _maxIdle = maxIdle;
        _idleTimeout = idleTimeout;
    }

    internal LythonExecutionResult Run(Func<LythonExecutionResult> runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var work = new SynchronousWorkItem(runner, ClrExecutionContext.Capture());
        Schedule(work);
        return work.Wait();
    }

    internal Task<LythonExecutionResult> RunAsync(Func<Task<LythonExecutionResult>> runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var work = new AsynchronousWorkItem(runner, ClrExecutionContext.Capture());
        var task = work.Task;
        Schedule(work);
        return task;
    }

    private void Schedule(WorkItem work)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_idle.First is { } node)
            {
                _idle.Remove(node);
                node.Value.IdleNode = null;
                node.Value.Pending = work;
                Monitor.PulseAll(_gate);
            }
            else
            {
                var worker = new Worker(this, work);
                // Do not leave the first caller's context attached to the cached
                // thread. Each request enters and restores its own context.
                if (ClrExecutionContext.IsFlowSuppressed()) worker.Thread.Start();
                else
                {
                    using (ClrExecutionContext.SuppressFlow()) worker.Thread.Start();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var worker in _idle) worker.IdleNode = null;
            _idle.Clear();
            Monitor.PulseAll(_gate);
        }
    }

    private WorkItem? Take(Worker worker)
    {
        lock (_gate)
        {
            while (worker.Pending is null)
            {
                if (_disposed) return null;
                var remaining = _idleTimeout - Stopwatch.GetElapsedTime(worker.IdleSince);
                if (remaining <= TimeSpan.Zero)
                {
                    if (worker.IdleNode is { } node) _idle.Remove(node);
                    worker.IdleNode = null;
                    return null;
                }
                Monitor.Wait(_gate, remaining);
            }
            var work = worker.Pending;
            worker.Pending = null;
            return work;
        }
    }

    private bool Return(Worker worker)
    {
        lock (_gate)
        {
            if (_disposed || _idle.Count >= _maxIdle) return false;
            worker.IdleSince = Stopwatch.GetTimestamp();
            worker.IdleNode = _idle.AddFirst(worker);
            return true;
        }
    }

    private sealed class Worker
    {
        private readonly ExecutionThreads _owner;
        internal readonly Thread Thread;
        internal WorkItem? Pending;
        internal LinkedListNode<Worker>? IdleNode;
        internal long IdleSince;

        internal Worker(ExecutionThreads owner, WorkItem work)
        {
            _owner = owner;
            Pending = work;
            Thread = new Thread(Run, owner._stackBytes)
            {
                IsBackground = true,
                Name = "Lython execution",
            };
        }

        private void Run()
        {
            var cleanContext = ClrExecutionContext.Capture()!;
            while (_owner.Take(this) is { } work)
            {
                work.Execute(cleanContext);
                // Make the worker available before waking its caller, so a
                // sequential next call can reuse it immediately.
                var reuse = _owner.Return(this);
                work.Complete();
                if (!reuse) return;
            }
        }
    }

    private abstract class WorkItem(ClrExecutionContext? context)
    {
        private ClrExecutionContext? _context = context;
        protected ExceptionDispatchInfo? Failure;
        protected abstract void Run();
        protected abstract void ClearRunner();
        internal abstract void Complete();

        // Keep transient runner/context roots out of the idle worker's frame.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Execute(ClrExecutionContext cleanContext)
        {
            try
            {
                ClrExecutionContext.Run(_context ?? cleanContext,
                    static state =>
                    {
                        ((WorkItem)state!).Run();
                    }, this);
            }
            catch (Exception ex) { Failure = ExceptionDispatchInfo.Capture(ex); }
            finally
            {
                ClearRunner();
                _context = null;
            }
        }
    }

    private sealed class SynchronousWorkItem(Func<LythonExecutionResult> runner, ClrExecutionContext? context) : WorkItem(context)
    {
        private readonly object _completionGate = new();
        private Func<LythonExecutionResult>? _runner = runner;
        private LythonExecutionResult? _result;
        private bool _completed;

        protected override void Run() => _result = _runner!();
        protected override void ClearRunner() => _runner = null;

        internal override void Complete()
        {
            lock (_completionGate)
            {
                _completed = true;
                Monitor.PulseAll(_completionGate);
            }
        }

        internal LythonExecutionResult Wait()
        {
            LythonExecutionResult? result;
            ExceptionDispatchInfo? failure;
            lock (_completionGate)
            {
                while (!_completed) Monitor.Wait(_completionGate);
                result = _result;
                failure = Failure;
                // An idle worker may still have the completed work item in a
                // CLR local. Never let it retain a guest result or an exception.
                _result = null;
                Failure = null;
            }
            failure?.Throw();
            return result ?? throw new InvalidOperationException("Lython sync execution ended without a result.");
        }
    }

    private sealed class AsynchronousWorkItem(Func<Task<LythonExecutionResult>> runner, ClrExecutionContext? context) : WorkItem(context)
    {
        private Func<Task<LythonExecutionResult>>? _runner = runner;
        private Task<LythonExecutionResult>? _pending;
        private TaskCompletionSource<LythonExecutionResult>? _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<LythonExecutionResult> Task => _completion!.Task;
        protected override void Run() => _pending = _runner!();
        protected override void ClearRunner() => _runner = null;

        // The worker may keep this work item in a CLR local while idle. Move
        // all result/context owners into completion before publishing anything.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal override void Complete()
        {
            var completion = _completion!;
            var pending = _pending;
            var failure = Failure;
            _completion = null;
            _pending = null;
            Failure = null;
            if (failure is not null) completion.TrySetException(failure.SourceException);
            else if (pending is null) completion.TrySetException(new InvalidOperationException("Lython async execution ended without a task."));
            else if (pending.IsCompleted) Relay(pending, completion);
            else
            {
                _ = pending.ContinueWith(static (task, state) => Relay(task, (TaskCompletionSource<LythonExecutionResult>)state!),
                    completion, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        private static void Relay(Task<LythonExecutionResult> task, TaskCompletionSource<LythonExecutionResult> completion)
        {
            // Called only for a terminal task: no worker waits through I/O.
            // Keep the old relay's faulted-task treatment of cancellation.
            try { completion.TrySetResult(task.GetAwaiter().GetResult()); }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
}
