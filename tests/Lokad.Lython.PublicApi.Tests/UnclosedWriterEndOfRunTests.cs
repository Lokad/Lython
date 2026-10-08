using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Execution-end ownership for open text writers: successful execution
// publishes outstanding writers (flush + close) instead of silently
// discarding accepted writes. A top-level return publishes too, while a
// publication failure replaces the outcome. Failures still attempt
// publication without masking the original error; cancellation publishes
// nothing, so a cancelled run leaves no file behind.
public sealed class UnclosedWriterEndOfRunTests
{
    [Fact]
    public void AnonymousWrite_PublishesAtSuccessfulEnd()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("print(open('/out.txt', 'w').write('yes'))", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3\n", result.StandardOutput);
        Assert.True(host.Exists("/out.txt"));
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task AnonymousWrite_PublishesAtSuccessfulEndAsync()
    {
        var host = new MockLythonHost();
        var result = await new LythonEngine().RunAsync("print(open('/out.txt', 'w').write('yes'))", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3\n", result.StandardOutput);
        Assert.True(await host.ExistsAsync("/out.txt", CancellationToken.None));
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task AnonymousWrite_AwaitsDelayedPublication()
    {
        var host = new DelayedLythonHost();
        var result = await new LythonEngine().RunAsync("print(open('/out.txt', 'w').write('yes'))", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AssignedHandleWithoutClose_Publishes()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void HandleReassignedToNone_Publishes()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')\nf = None", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void TopLevelReturn_PublishesDirtyWriter()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')\nreturn 7", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(7), result.ReturnValue);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task TopLevelReturn_PublishesDirtyWriterAsync()
    {
        var host = new MockLythonHost();
        var result = await new LythonEngine().RunAsync("f = open('/out.txt', 'w')\nf.write('yes')\nreturn 7", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(7), result.ReturnValue);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FlushThenDirtySuffix_PublishesBoth()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')\nf.flush()\nf.write('!')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("yes!", host.ReadText("/out.txt"));
    }

    [Fact]
    public void BareOpen_PublishesEmptyFile()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("open('/out.txt', 'w')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.Exists("/out.txt"));
        Assert.Equal(string.Empty, host.ReadText("/out.txt"));
    }

    [Fact]
    public void AppendWithoutClose_Publishes()
    {
        var host = new MockLythonHost();
        host.SeedFile("/out.txt", "old");
        var result = new LythonEngine().Run("f = open('/out.txt', 'a')\nf.write('yes')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("oldyes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FailureStillPublishes_PreservesFailure()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')\nraise ValueError('boom')", host);
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task FailureStillPublishes_PreservesFailureAsync()
    {
        var host = new DelayedLythonHost();
        var result = await new LythonEngine().RunAsync("f = open('/out.txt', 'w')\nf.write('yes')\nraise ValueError('boom')", host);
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ExplicitClose_RemainsIdempotent()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("f = open('/out.txt', 'w')\nf.write('yes')\nf.close()\nf.close()", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ContextManager_Publishes()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run("with open('/out.txt', 'w') as f:\n    f.write('yes')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("yes", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledRunWithNormalLimits_PublishesNothing(bool asynchronous)
    {
        using var cts = new CancellationTokenSource();
        var script = new LythonEngine().Compile("f = open('/out.txt', 'w')\nf.write('yes')\nprint('buffered')\nwhile True:\n    pass");
        Assert.True(script.IsValid);
        var buffered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MockLythonHost { OnStandardOutputWrite = () => buffered.TrySetResult(true) };
        var options = new LythonRunOptions { CancellationToken = cts.Token };
        var runTask = asynchronous ? script.RunAsync(host, options) : Task.Run(() => script.Run(host, options));
        try
        {
            // Rendezvous after the writer holds bytes, before canceling the run.
            await buffered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("execution canceled", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
            Assert.False(host.Exists("/out.txt"));
        }
        finally
        {
            cts.Cancel();
        }
    }
}
