using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

// Force the actual continuation onto a small managed stack, not the large
// startup worker. A fatal regression remains isolated from the test host.
internal static class NativeCallStackFixture
{
    internal static async Task<int> RunAsync(string scenario, string libraryHash)
    {
        if (Environment.ProcessorCount != 1) throw new InvalidOperationException("The fixture needs one execution worker for its prefix-release barrier.");
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
        var pending = script.RunAsync(host, new LythonRunOptions { MaxRecursionDepth = 1024 });
        await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (pending.IsCompleted) throw new InvalidOperationException("Expected pending host work.");
        // A completed second command on the single worker proves that the first
        // prefix returned and registered its whole await chain. No timer race.
        using (var barrier = new LythonEngine().Run("return 1", new MockLythonHost()))
            if (!barrier.Success) throw new InvalidOperationException("Prefix-release barrier failed.");
        var continuation = new Thread(() => timing.Release.SetResult(), 256 * 1024);
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
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new();
        internal int ResumeThread;
        internal int CompletedAsynchronously;
        public long MonotonicNanoseconds { get { ResumeThread = Environment.CurrentManagedThreadId; return 0; } }
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Entered.SetResult();
            await Release.Task.ConfigureAwait(false);
            CompletedAsynchronously++;
        }
    }
}
