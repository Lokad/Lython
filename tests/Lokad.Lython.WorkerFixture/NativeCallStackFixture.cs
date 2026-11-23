using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

// Force the actual continuation onto a small managed stack, not the large
// startup worker. A fatal regression remains isolated from the test host.
internal static class NativeCallStackFixture
{
    internal static async Task<int> RunAsync(string scenario, string libraryHash)
    {
        const string source = """
            import time
            time.sleep(1)
            time.monotonic_ns()
            def direct(n):
                return 7 if n<=0 else direct(n-1)
            def first(n):
                return 7 if n<=0 else second(n-1)
            def second(n):
                return first(n)
            failures=0
            for call in (direct,first):
                try:call(10000)
                except RuntimeError:failures+=1
                assert call(100)==7
            return failures==2
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        if (!script.IsValid) throw new InvalidOperationException("Continuation fixture did not compile.");
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        var prefixExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // The startup worker restores its ambient context after the whole await
        // chain registers. Observe that exit before releasing the host delay.
        var prefixContext = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.PreviousValue && !change.CurrentValue
                && timing.PrefixThread == Environment.CurrentManagedThreadId)
                prefixExited.TrySetResult();
        });
        prefixContext.Value = true;
        var pending = script.RunAsync(host, new LythonRunOptions { MaxRecursionDepth = 1024 });
        prefixContext.Value = false;
        await prefixExited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (pending.IsCompleted) throw new InvalidOperationException("Expected pending host work.");
        var continuation = new Thread(timing.Release, 256 * 1024);
        continuation.Start();
        if (!continuation.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Owned continuation did not join.");
        bool success;
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            success = result.Success && result.ReturnValue is true;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                scenario,
                result.Success,
                value = result.ReturnValue?.ToString(),
                error = result.Failure?.ExceptionType,
                message = result.Failure?.Message,
                completedAsynchronously = timing.CompletedAsynchronously,
                prefixThread = timing.PrefixThread,
                continuationThread = timing.ResumeThread,
                ownedThread = continuation.ManagedThreadId,
                libraryHash,
                processId = Environment.ProcessId,
            }));
        }
        script.Dispose();
        return success && timing.CompletedAsynchronously == 1
            && timing.ResumeThread == continuation.ManagedThreadId && budget.ReservedBytes == 0 ? 0 : 7;
    }

    private sealed class HeldTiming : ILythonTiming
    {
        private readonly TaskCompletionSource _release = new();
        internal int PrefixThread;
        internal int ResumeThread;
        internal int CompletedAsynchronously;
        public long MonotonicNanoseconds { get { ResumeThread = Environment.CurrentManagedThreadId; return 0; } }
        internal void Release()
        {
            CompletedAsynchronously++;
            _release.SetResult();
        }
        public ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            PrefixThread = Environment.CurrentManagedThreadId;
            // Return the actual held operation. An extra async wrapper can make
            // the CLR queue a continuation before it reaches the small stack.
            return new(_release.Task);
        }
    }
}
