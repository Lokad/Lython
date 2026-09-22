using System.Collections.Concurrent;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N01: async structural traversal must be execution-local, never retained on
// pool/worker threads across awaits. Pair tracking lives per-run in
// ExecutionState.StructuralTraversal, carried explicitly via ExecutionContext.
// Thread-static storage is only for context-free sync paths that never span
// an await. These tests pin deterministic hops, interleaving, cancellation,
// errors and absence of roots, plus configured-limit enforcement.
public sealed class StructuralGuardAsyncIsolationTests
{
    // Deterministic hopping host: two persistent workers alternate completions.
    // TaskCompletionSource without RunContinuationsAsynchronously runs the
    // awaiting continuation synchronously on the completing worker, forcing a
    // thread hop for every host read (same mechanism as the review probe).
    private sealed class HoppingHost : ILythonHost
    {
        private readonly BlockingCollection<Action>[] _queues = [new(), new()];
        private int _next;
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public HoppingHost()
        {
            _files["/delay"] = "1\n2\n3\n";
            foreach (var q in _queues)
            {
                var t = new Thread(() =>
                {
                    foreach (var a in q.GetConsumingEnumerable())
                        a();
                })
                {
                    IsBackground = true,
                };
                t.Start();
            }
        }

        public readonly ConcurrentBag<(int Thread, int Depth, int Pairs)> WorkerObservations = [];
        public readonly ConcurrentBag<(int Thread, int Depth, int Pairs)> BeforeAfterObservations = [];

        public string Cwd => "/";
        public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));
        public DateTimeOffset UtcNow => new(2024, 1, 2, 2, 4, 5, TimeSpan.Zero);
        public ILythonTextInput? StandardInput => null;
        public ILythonTextOutput? StandardOutput => null;
        public ILythonTextOutput? StandardError => null;
        public ILythonSubprocessRunner? SubprocessRunner => null;
        public ILythonTiming? Timing => null;

        private void Observe(string kind)
        {
            var bag = kind == "worker" ? WorkerObservations : BeforeAfterObservations;
            bag.Add((Environment.CurrentManagedThreadId, PyStructuralGuard.ThreadDepthForTests, PyStructuralGuard.ThreadPairCountForTests));
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken ct)
        {
            Observe("before");
            if (!_files.TryGetValue(path, out var text))
                throw new InvalidOperationException("missing " + path);
            var done = new TaskCompletionSource();
            _queues[Interlocked.Increment(ref _next) % 2].Add(() =>
            {
                Thread.Sleep(1);
                done.SetResult();
                Observe("worker");
            });
            await done.Task.ConfigureAwait(false);
            Observe("after");
            return System.Text.Encoding.UTF8.GetBytes(text);
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken ct) => throw new InvalidOperationException("write");
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken ct) => throw new InvalidOperationException("append");
        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken ct) => throw new InvalidOperationException("bytes");
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, CancellationToken ct) => throw new InvalidOperationException("range");
        public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path, long offset, int count, CancellationToken ct) => throw new InvalidOperationException("range");
        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> b, CancellationToken ct) => throw new InvalidOperationException("write");
        public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> b, CancellationToken ct) => throw new InvalidOperationException("append");
        public ValueTask<bool> ExistsAsync(string path, CancellationToken ct) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken ct) => throw new InvalidOperationException("listdir");
        public ValueTask MkDirAsync(string path, CancellationToken ct) => throw new InvalidOperationException("mkdir");
        public ValueTask RemoveAsync(string path, CancellationToken ct) => throw new InvalidOperationException("remove");
        public ValueTask CopyAsync(string s, string d, CancellationToken ct) => throw new InvalidOperationException("copy");
        public ValueTask MoveAsync(string s, string d, CancellationToken ct) => throw new InvalidOperationException("move");
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken ct) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
    }

    private const string DelayedListScript =
        "from pathlib import Path\n" +
        "class E:\n" +
        "    def __eq__(self, other):\n" +
        "        return Path(\"/delay\").read_text() == \"1\\n2\\n3\\n\"\n" +
        "for i in range(10):\n" +
        "    a = [E(), [0] * 50]\n" +
        "    b = [E(), [0] * 50]\n" +
        "    assert a == b\n" +
        "print(\"done\")\n";

    [Fact]
    public void ExplicitState_DisposesSameStateAcrossThreads()
    {
        var stateA = new StructuralGuardState();
        var stateB = new StructuralGuardState();
        var left = new object();
        var right = new object();

        var scopeA = PyStructuralGuard.EnterPair(stateA, left, right, span: null, context: null);
        Assert.Equal(1, stateA.Pairs?.Count ?? 0);
        Assert.Equal(0, stateB.Pairs?.Count ?? 0);
        Assert.Equal(0, PyStructuralGuard.ThreadPairCountForTests);

        // Simulate continuation hopping to another thread: dispose there.
        var t = new Thread(() => scopeA.Dispose());
        t.Start();
        t.Join();

        Assert.Equal(0, stateA.Pairs?.Count ?? 0);
        Assert.Equal(0, stateA.Depth);
        Assert.Equal(0, PyStructuralGuard.ThreadPairCountForTests);
    }

    [Fact]
    public void ExplicitStates_AreIsolated()
    {
        var stateA = new StructuralGuardState();
        var stateB = new StructuralGuardState();
        var a1 = new object();
        var a2 = new object();
        var b1 = new object();
        var b2 = new object();

        using (PyStructuralGuard.EnterPair(stateA, a1, a2, null, null))
        using (PyStructuralGuard.EnterPair(stateB, b1, b2, null, null))
        {
            Assert.Equal(1, stateA.Pairs?.Count ?? 0);
            Assert.Equal(1, stateB.Pairs?.Count ?? 0);
        }

        Assert.Equal(0, stateA.Pairs?.Count ?? 0);
        Assert.Equal(0, stateB.Pairs?.Count ?? 0);
    }

    [Fact]
    public async Task DelayedListComparisons_SucceedWithHopsAndLeaveNoWorkerRoots()
    {
        var host = new HoppingHost();
        var script = new LythonEngine().Compile(DelayedListScript);
        Assert.True(script.IsValid);

        for (var i = 0; i < 2; i++)
        {
            var result = await script.RunAsync(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("done\n", result.StandardOutput);
        }

        // Workers never retain pair entries: async tracking is per-run, not thread-static.
        foreach (var obs in host.WorkerObservations)
        {
            Assert.Equal(0, obs.Pairs);
            Assert.Equal(0, obs.Depth);
        }

        Assert.Equal(0, PyStructuralGuard.ThreadDepthForTests);
        Assert.Equal(0, PyStructuralGuard.ThreadPairCountForTests);
    }

    [Fact]
    public async Task InterleavedRuns_OnSharedWorkers_DoNotContaminate()
    {
        var host = new HoppingHost();
        var script = new LythonEngine().Compile(DelayedListScript);
        Assert.True(script.IsValid);

        var t1 = script.RunAsync(host);
        var t2 = script.RunAsync(host);
        var results = await Task.WhenAll(t1, t2);
        foreach (var r in results)
        {
            Assert.True(r.Success, r.Failure?.Message);
            Assert.Equal("done\n", r.StandardOutput);
        }

        foreach (var obs in host.WorkerObservations)
        {
            Assert.Equal(0, obs.Pairs);
        }
    }

    [Fact]
    public async Task CancellationDuringDelayedComparison_CleansUpAndAllowsRetry()
    {
        var host = new HoppingHost();
        var script = new LythonEngine().Compile(DelayedListScript);
        Assert.True(script.IsValid);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);
        var cancelled = await script.RunAsync(host, new LythonRunOptions { CancellationToken = cts.Token });
        // Either cancellation wins or the tiny workload finishes first; both are acceptable,
        // but a cancelled run must not poison the next run or workers.
        if (!cancelled.Success)
        {
            Assert.NotNull(cancelled.Failure);
        }

        var retryHost = new HoppingHost();
        var retry = await script.RunAsync(retryHost);
        Assert.True(retry.Success, retry.Failure?.Message);
        foreach (var obs in retryHost.WorkerObservations)
        {
            Assert.Equal(0, obs.Pairs);
        }
    }

    [Fact]
    public async Task ErrorInDelayedComparison_CleansUpAndAllowsRetry()
    {
        var host = new HoppingHost();
        const string failing =
            "from pathlib import Path\n" +
            "class E:\n" +
            "    def __eq__(self, other):\n" +
            "        Path(\"/delay\").read_text()\n" +
            "        raise ValueError(\"boom\")\n" +
            "print([E()] == [E()])\n";
        var bad = new LythonEngine().Compile(failing);
        var failed = await bad.RunAsync(host);
        Assert.False(failed.Success);
        Assert.Equal("ValueError", failed.Failure?.ExceptionType);

        var good = new LythonEngine().Compile(DelayedListScript);
        var ok = await good.RunAsync(host);
        Assert.True(ok.Success, ok.Failure?.Message);
        foreach (var obs in host.WorkerObservations)
        {
            Assert.Equal(0, obs.Pairs);
        }
    }

    [Fact]
    public async Task CyclicLists_WithHops_StillRaiseRecursionError()
    {
        var host = new HoppingHost();
        const string cyclic =
            "from pathlib import Path\n" +
            "class E:\n" +
            "    def __eq__(self, other):\n" +
            "        return Path(\"/delay\").read_text() == \"1\\n2\\n3\\n\"\n" +
            "a = []\n" +
            "b = []\n" +
            "a.append(a)\n" +
            "b.append(b)\n" +
            "print(a == b)\n";
        var script = new LythonEngine().Compile(cyclic);
        var result = await script.RunAsync(host);
        Assert.False(result.Success);
        Assert.Equal("RecursionError", result.Failure?.ExceptionType);

        // Subsequent healthy run still succeeds with clean workers.
        var healthy = new LythonEngine().Compile(DelayedListScript);
        var ok = await healthy.RunAsync(host);
        Assert.True(ok.Success, ok.Failure?.Message);
    }

    [Fact]
    public void EnterPair_WithContext_EnforcesConfiguredRecursionLimit()
    {
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MaxRecursionDepth = 5 };
        var context = new LythonRuntime.ExecutionContext(host, options);
        var state = context.Services.State.StructuralTraversal;

        var scopes = new List<IDisposable>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                scopes.Add(PyStructuralGuard.EnterPair(state, new object(), new object(), span: null, context));
            }

            Assert.Fail("expected RecursionError");
        }
        catch (LythonRuntimeException ex)
        {
            Assert.Equal("RecursionError", ex.ExceptionType);
        }
        finally
        {
            foreach (var s in scopes)
                s.Dispose();
        }

        Assert.Equal(0, state.Depth);
        Assert.Equal(0, state.Pairs?.Count ?? 0);
    }
}
