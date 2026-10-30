using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SynchronousHostBorrowTests
{
    public static IEnumerable<object[]> BrokenModes()
    {
        foreach (var binary in new[] { false, true })
        foreach (var flush in new[] { false, true })
        foreach (var completion in new[] { "success", "fault", "cancel" })
            yield return [binary, flush, completion];
    }

    [Theory, MemberData(nameof(BrokenModes))]
    public Task BrokenPromiseJoinsBorrowBeforeFailureAndNeverRetries(bool binary, bool flush, string completion)
        => Check(binary, flush, completion, synchronous: true);

    [Theory]
    [InlineData(false), InlineData(true)]
    public Task HonestAsyncHostKeepsNormalCompletion(bool binary)
        => Check(binary, flush: false, "success", synchronous: false);

    private static async Task Check(bool binary, bool flush, string completion, bool synchronous)
    {
        var budget = new LythonMemoryBudget(1048576);
        var host = new PendingHost(synchronous, completion);
        var source = "with open('/out','" + (binary ? "wb" : "w") + "') as f:\n f.write(" + (binary ? "b" : "") + "'x'*8192)\n" + (flush ? " f.flush()\n" : "");
        var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var run = synchronous ? Task.Run(() => script.Run(host)) : script.RunAsync(host);
        try
        {
            await host.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline + 8192);
            host.Release.TrySetResult();
            using (var result = await run)
            {
                if (synchronous)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
                    Assert.Contains("violated its synchronous host capability contract", result.Failure!.Message);
                }
                else Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(1, host.AppendCalls);
                Assert.Equal(completion == "success" ? 8192 : completion == "fault" ? 4096 : 0, host.PublishedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally
        {
            host.Release.TrySetResult();
            using var drained = await run;
            foreach (var pending in host.Pending)
            {
                try { await pending; }
                catch (IOException) { }
                catch (OperationCanceledException) { }
            }
            script.Dispose();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PendingHost(bool synchronous, string completion) : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost _files = new();
        public bool CompletesSynchronously => synchronous;
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<Task> Pending = [];
        public int AppendCalls, PublishedBytes;
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
            => new PendingFile(this, await _files.OpenFileAsync(request, options, budget, token));
        private sealed class PendingFile(PendingHost host, ILythonByteFile file) : ILythonByteFile, ILythonSynchronousHostCapability
        {
            private long _position = file.Position;
            public bool CompletesSynchronously => host.CompletesSynchronously;
            public long Position => _position;
            public long? LengthHint => file.LengthHint;
            public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => file.ReadAsync(buffer, token);
            public async ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
            {
                await host.Append(buffer, token).ConfigureAwait(false);
                _position += buffer.Length;
                return new LythonFileWriteResult(buffer.Length);
            }
            public ValueTask FlushAsync(CancellationToken token) => file.FlushAsync(token);
            public ValueTask DisposeAsync() => file.DisposeAsync();
        }
        private ValueTask Append(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            AppendCalls++;
            var task = Complete(data, token);
            Pending.Add(task);
            Entered.TrySetResult();
            return new ValueTask(task);
        }
        private async Task Complete(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            await Release.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (completion == "cancel") throw new OperationCanceledException();
            PublishedBytes += completion == "fault" ? data.Span.Length / 2 : data.Span.Length;
            if (completion == "fault") throw new IOException("partial publication");
        }
        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => _files.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => _files.ListDirAsync(path, token);
        public ValueTask MkDirAsync(string path, CancellationToken token) => _files.MkDirAsync(path, token);
        public ValueTask RemoveAsync(string path, CancellationToken token) => _files.RemoveAsync(path, token);
        public ValueTask CopyAsync(string source, string target, CancellationToken token) => _files.CopyAsync(source, target, token);
        public ValueTask MoveAsync(string source, string target, CancellationToken token) => _files.MoveAsync(source, target, token);
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => _files.StatAsync(path, token);
    }
}
