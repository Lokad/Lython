using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ArgparseFileLifetimeTests
{
    [Theory]
    [InlineData("r")]
    [InlineData("w")]
    [InlineData("a")]
    public async Task CancellationAwaitsPendingFileTypeBeforeReleasingItsOwner(string mode)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source(mode), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new HeldOpenHost();
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await host.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.True(host.Finished.Task.IsCompleted);
            Assert.Empty(result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel(); host.Release.TrySetResult();
            using var finished = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("r")]
    [InlineData("w")]
    [InlineData("a")]
    public async Task RefusalAfterFileTypeSuspendsStaysTerminalThroughFinallyReturn(string mode)
    {
        var budget = new AdmissionGateBudget();
        using var script = new LythonEngine().Compile(Source(mode, allocate: true), budget);
        Assert.True(script.IsValid);
        var baseline = budget.Root.ReservedBytes;
        var host = new HeldOpenHost();
        var run = script.RunAsync(host);
        try
        {
            await host.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            budget.Gate(0); host.Release.TrySetResult();
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(host.Finished.Task.IsCompleted);
            Assert.Empty(result.StandardOutput);
        }
        finally
        {
            host.Release.TrySetResult();
            using var finished = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.Root.ReservedBytes);
        budget.Gate(4 * 1024 * 1024);
        using (var control = new LythonEngine().Run("return 1", new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            Assert.True(control.Success, control.Failure?.Message);
        Assert.Equal(baseline, budget.Root.ReservedBytes);
    }

    private static string Source(string mode, bool allocate = false) => "import argparse\np=argparse.ArgumentParser(add_help=False)\np.add_argument('--x',type=argparse.FileType('" + mode + "'))\ntry:\n p.parse_args(['--x','/file'])\n" + (allocate ? " storage='x'*8388608\n" : "") + "finally:return 0";

    private sealed class HeldOpenHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();
        public HeldOpenHost() => _inner.SeedFile("/file", "data");
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => _inner.StatAsync(path,token);
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            Started.TrySetResult();
            try { await Release.Task.WaitAsync(token); return await _inner.OpenFileAsync(path,options,budget,token); }
            finally { Finished.TrySetResult(); }
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string p, CancellationToken t) => _inner.ReadTextUtf8Async(p,t);
        public ValueTask WriteTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => _inner.WriteTextUtf8Async(p,b,t);
        public ValueTask AppendTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => _inner.AppendTextUtf8Async(p,b,t);
        public ValueTask<bool> ExistsAsync(string p, CancellationToken t) => _inner.ExistsAsync(p,t);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => _inner.ListDirAsync(p,t);
        public ValueTask MkDirAsync(string p, CancellationToken t) => _inner.MkDirAsync(p,t);
        public ValueTask RemoveAsync(string p, CancellationToken t) => _inner.RemoveAsync(p,t);
        public ValueTask CopyAsync(string a, string b, CancellationToken t) => _inner.CopyAsync(a,b,t);
        public ValueTask MoveAsync(string a, string b, CancellationToken t) => _inner.MoveAsync(a,b,t);
    }
}
