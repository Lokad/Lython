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
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token) => _files.ReadTextUtf8Async(path, token);
        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> data, CancellationToken token) => _files.WriteTextUtf8Async(path, data, token);
        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> data, CancellationToken token) => _files.WriteBytesAsync(path, data, token);
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> data, CancellationToken token) => Append(data, token);
        public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> data, CancellationToken token) => Append(data, token);
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
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => _files.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => _files.ListDirAsync(path, token);
        public ValueTask MkDirAsync(string path, CancellationToken token) => _files.MkDirAsync(path, token);
        public ValueTask RemoveAsync(string path, CancellationToken token) => _files.RemoveAsync(path, token);
        public ValueTask CopyAsync(string source, string target, CancellationToken token) => _files.CopyAsync(source, target, token);
        public ValueTask MoveAsync(string source, string target, CancellationToken token) => _files.MoveAsync(source, target, token);
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => _files.StatAsync(path, token);
    }
}
