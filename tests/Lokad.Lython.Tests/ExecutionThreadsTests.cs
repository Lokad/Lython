using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ExecutionThreadsTests
{
    private static ExecutionThreads Create(int maxIdle = 2, TimeSpan? timeout = null)
        => new(16 * 1024 * 1024, maxIdle, timeout ?? TimeSpan.FromSeconds(30));

    private static LythonExecutionResult Success()
        => LythonExecutionResult.Succeeded(null, string.Empty, string.Empty, []);

    [Fact]
    public void SequentialCallsReuseThreadAndDisposeRetiresIt()
    {
        var threads = Create();
        Thread? first = null, second = null;
        threads.Run(() => { first = Thread.CurrentThread; return Success(); });
        threads.Run(() => { second = Thread.CurrentThread; return Success(); });
        Assert.Same(first, second);
        threads.Dispose();
        Assert.True(first!.Join(TimeSpan.FromSeconds(10)));
        Assert.Throws<ObjectDisposedException>(() => threads.Run(Success));
    }

    [Fact]
    public void IdleThreadExpiresWithoutAnotherCall()
    {
        using var threads = Create(timeout: TimeSpan.FromMilliseconds(50));
        Thread? worker = null;
        threads.Run(() => { worker = Thread.CurrentThread; return Success(); });
        Assert.True(worker!.Join(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ConcurrentCallsDoNotQueueAndIdleRetentionIsBounded()
    {
        using var threads = Create(maxIdle: 2);
        using var entered = new CountdownEvent(3);
        using var release = new ManualResetEventSlim();
        var workers = new Thread[3];
        // These callers block in Run. Give each its own thread so the test
        // measures Lython worker admission, independent of ThreadPool ramp-up
        // and other test collections occupying pool threads.
        var calls = Enumerable.Range(0, 3).Select(i => Task.Factory.StartNew(() => threads.Run(() =>
        {
            workers[i] = Thread.CurrentThread;
            entered.Signal();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return Success();
        }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); }
        finally
        {
            release.Set();
            // Do not dispose the callback's events before every caller exits,
            // including when the entry assertion fails.
            await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(15));
        }
        Assert.Equal(3, workers.Distinct().Count());
        Assert.True(SpinWait.SpinUntil(() => workers.Count(t => t.IsAlive) <= 2, TimeSpan.FromSeconds(10)));
        Assert.Equal(2, workers.Count(t => t.IsAlive));
        threads.Dispose();
        Assert.All(workers, t => Assert.True(t.Join(TimeSpan.FromSeconds(10))));
    }

    [Fact]
    public async Task DisposingDuringRunAllowsItToFinishAndRetiresWorker()
    {
        using var threads = Create();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Thread? worker = null;
        var run = Task.Factory.StartNew(() => threads.Run(() =>
        {
            worker = Thread.CurrentThread;
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return Success();
        }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            threads.Dispose();
        }
        finally
        {
            release.Set();
            Assert.True((await run.WaitAsync(TimeSpan.FromSeconds(15))).Success);
        }
        Assert.True(worker!.Join(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void FailurePreservesExceptionAndStackAndDoesNotPoisonWorker()
    {
        using var threads = Create();
        var exception = new InvalidOperationException("original");
        var actual = Assert.Throws<InvalidOperationException>(() => threads.Run(() => ThrowFromRunner(exception)));
        Assert.Same(exception, actual);
        Assert.Contains(nameof(ThrowFromRunner), actual.StackTrace);
        Assert.True(threads.Run(Success).Success);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LythonExecutionResult ThrowFromRunner(Exception exception) => throw exception;

    [Fact]
    public void IdleWorkerDoesNotRetainFailureOrItsPayload()
    {
        using var threads = Create();
        var references = FailAndDrop(threads);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] FailAndDrop(ExecutionThreads threads)
    {
        var payload = new object();
        var exception = new InvalidOperationException();
        exception.Data["payload"] = payload;
        Assert.Throws<InvalidOperationException>(() => threads.Run(() => ThrowFromRunner(exception)));
        return [new(exception), new(payload)];
    }

    [Fact]
    public void IdleWorkerDoesNotRetainAsyncResultFailureOrContext()
    {
        using var threads = Create();
        var references = FinishAsyncAndDrop(threads);
        threads.Run(Success);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] FinishAsyncAndDrop(ExecutionThreads threads)
    {
        var ambient = new AsyncLocal<object?>();
        var payload = new object();
        ambient.Value = payload;
        try
        {
            var result = Success();
            Assert.Same(result, threads.RunAsync(() => Task.FromResult(result)).GetAwaiter().GetResult());
            var error = new InvalidOperationException();
            error.Data["payload"] = payload;
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => threads.RunAsync(
                () => Task.FromException<LythonExecutionResult>(error)).GetAwaiter().GetResult()));
            return [new(result), new(error), new(payload)];
        }
        finally { ambient.Value = null; }
    }

    [Fact]
    public async Task SuspendedContinuationKeepsItsContextWhileWorkerRunsAnotherCaller()
    {
        using var threads = Create(maxIdle: 1);
        var ambient = new AsyncLocal<string?> { Value = "first" };
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = threads.RunAsync(async () =>
        {
            Assert.Equal("first", ambient.Value);
            ambient.Value = "suspended";
            entered.SetResult(Thread.CurrentThread);
            await release.Task.ConfigureAwait(false);
            Assert.Equal("suspended", ambient.Value);
            return Success();
        });
        try
        {
            var worker = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(3)));
            Assert.Equal("first", ambient.Value);
            ambient.Value = "second";
            await threads.RunAsync(() =>
            {
                Assert.Same(worker, Thread.CurrentThread);
                Assert.Equal("second", ambient.Value);
                ambient.Value = "host mutation";
                return Task.FromResult(Success());
            }).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("second", ambient.Value);
        }
        finally
        {
            release.TrySetResult();
            Assert.True((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Success);
            ambient.Value = null;
        }
    }
}
