using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HostStreamsScenarioTests
{
    [Fact]
    public void Input_AndSysStreams_AreHostMediated()
    {
        var host = new MockLythonHost();
        host.SeedStandardInput("typed line\n");

        var result = new LythonEngine().Run(
            """
import sys
value = input("prompt> ")
sys.stdout.write("|tail")
sys.stdout.flush()
sys.stderr.write("warn")
__lython_file = open("/out.txt", "w")
__lython_file.write(value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("typed line", host.ReadText("/out.txt"));
        Assert.Equal("prompt> |tail", result.StandardOutput);
        Assert.Equal("warn", result.StandardError);
        Assert.Equal("prompt> |tail", host.CapturedStandardOutput());
        Assert.Equal("warn", host.CapturedStandardError());
    }

    [Fact]
    public void Input_FailsCleanlyWhenStandardInputIsUnavailable()
    {
        var result = new LythonEngine().Run("input()\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3040");
    }

    [Fact]
    public async Task StreamHostFailures_UseTheSharedSyncAndAsyncTranslation()
    {
        var state = new ExecutionState(new MockLythonHost(), options: null);
        var input = new HostTextInputHandle(new ThrowingTextInput(), state);
        var output = new HostTextOutputHandle(new ThrowingTextOutput(), "<stdout>", state);

        AssertHostFailure(
            Assert.Throws<LythonRuntimeException>(() => input.ReadAll(span: null)),
            "stdin.read",
            "input failed");
        AssertHostFailure(
            await Assert.ThrowsAsync<LythonRuntimeException>(async () => await input.ReadAllAsync(span: null)),
            "stdin.read",
            "input failed");
        AssertHostFailure(
            Assert.Throws<LythonRuntimeException>(() => output.Flush(span: null)),
            "<stdout>.flush",
            "output failed");
        AssertHostFailure(
            await Assert.ThrowsAsync<LythonRuntimeException>(async () => await output.FlushAsync(span: null)),
            "<stdout>.flush",
            "output failed");

        static void AssertHostFailure(LythonRuntimeException exception, string operation, string detail)
        {
            Assert.Equal("RuntimeError", exception.ExceptionType);
            Assert.Equal($"Host {operation} failed: {detail}", exception.Message);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }
    }

    private sealed class ThrowingTextInput : ILythonTextInput, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        public ValueTask<ReadOnlyMemory<byte>> ReadToEndUtf8Async(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return ValueTask.FromException<ReadOnlyMemory<byte>>(new InvalidOperationException("input failed"));
        }

        public ValueTask<ReadOnlyMemory<byte>?> ReadLineUtf8Async(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return ValueTask.FromException<ReadOnlyMemory<byte>?>(new InvalidOperationException("input failed"));
        }
    }

    private sealed class ThrowingTextOutput : ILythonTextOutput, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        public ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            _ = utf8;
            _ = cancellationToken;
            return ValueTask.FromException(new InvalidOperationException("output failed"));
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return ValueTask.FromException(new InvalidOperationException("output failed"));
        }
    }
}
