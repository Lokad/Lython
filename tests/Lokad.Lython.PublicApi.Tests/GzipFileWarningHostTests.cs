using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileWarningHostTests
{
    private const string Source = "import gzip,io\nclass Raw:\n    mode='wb'\n    def __init__(self): self.buffer=io.BytesIO()\n    def write(self,data):\n        print('write')\n        return self.buffer.write(data)\nraw=Raw()\nprint('ready')\nwriter=gzip.GzipFile(fileobj=raw,mtime=0)\nwriter.write(b'payload')\nwriter.close()\nprint('finished',gzip.decompress(raw.buffer.getvalue()))\n";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWarningAwaitsMediatedStderrAndHonorsCancellation(bool cancel)
    {
        var script = new LythonEngine().Compile(Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        var stderr = new PausedTextOutput();
        host.SetStandardError(stderr);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var pending = script.RunAsync(host, new LythonRunOptions
        { SourcePath = "/warning.py", CancellationToken = cancellation.Token });
        await stderr.Started.Task.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        if (cancel) cancellation.Cancel();
        else stderr.Release.TrySetResult();
        var result = await pending;
        Assert.Contains("/warning.py:10: FutureWarning: ", result.StandardError);
        Assert.Equal(1, stderr.Writes);
        if (cancel)
        {
            Assert.False(result.Success);
            Assert.Equal("execution canceled", result.Failure?.Message);
            Assert.Equal("ready\n", result.StandardOutput);
            // A fresh run retries the constructor with a fresh warning registry.
            var fresh = script.Run(new MockLythonHost(), new LythonRunOptions { SourcePath = "/warning.py" });
            Assert.True(fresh.Success, fresh.Failure?.Message);
            Assert.Contains("FutureWarning", fresh.StandardError);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.StartsWith("ready\nwrite\n", result.StandardOutput);
            Assert.EndsWith("finished b'payload'\n", result.StandardOutput);
            Assert.Equal(result.StandardError, Encoding.UTF8.GetString(stderr.Payload));
            Assert.True(stderr.CompletedAsynchronously);
        }
    }

    [Fact]
    public void SynchronousRunRejectsAsyncStderrBeforeStartingTheWarningWrite()
    {
        var host = new MockLythonHost();
        var stderr = new PausedTextOutput();
        host.SetStandardError(stderr);
        var result = new LythonEngine().Run(Source, host);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Equal(0, stderr.Writes);
        Assert.Equal("ready\n", result.StandardOutput);
        Assert.Equal("", result.StandardError);
    }

    private sealed class PausedTextOutput : ILythonTextOutput, ILythonSynchronousHostCapability
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[] Payload { get; private set; } = [];
        public int Writes { get; private set; }
        public bool CompletedAsynchronously { get; private set; }
        public bool CompletesSynchronously => false;
        public async ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            Writes++;
            Payload = utf8.ToArray();
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Assert.Equal(Payload, utf8.ToArray());
            CompletedAsynchronously = true;
        }
        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
