using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BoundMethodAsyncLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task OverlappingSuspendedCallsKeepAllInvocationValues(int width)
    {
        var compiled = new LythonEngine().Compile(Source(width));
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var first = new DelayedLythonHost();
        var second = new DelayedLythonHost();
        first.SeedFile("/value", "first");
        second.SeedFile("/value", "second");
        var results = await Task.WhenAll(compiled.RunAsync(first), compiled.RunAsync(second)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(results, result => Assert.True(result.Success, result.Failure?.Message));
        Assert.Equal("first", results[0].ReturnValue);
        Assert.Equal("second", results[1].ReturnValue);
        Assert.True(first.CompletedAsynchronously > 0 && second.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task CancellationOfSuspendedMethodKeepsCompiledScriptReusable()
    {
        var compiled = new LythonEngine().Compile(Source(2));
        Assert.True(compiled.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "paused");
        var started = host.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var pending = compiled.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try { await started.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancellation.Cancel(); }
        var canceled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(canceled.Success);
        Assert.Contains("execution canceled", canceled.Failure?.Message, StringComparison.Ordinal);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/value", "fresh");
        var result = await compiled.RunAsync(fresh);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("fresh", result.ReturnValue);
    }

    private static string Source(int width)
    {
        var parameters = width == 0 ? "" : width == 1 ? ", a" : ", a, b";
        var returned = width == 0 ? "self,value" : width == 1 ? "self,a,value" : "self,a,b,value";
        var arguments = width == 0 ? "" : width == 1 ? "left" : "left,right";
        var assertions = width == 0 ? "" : width == 1 ? "assert result[1] is left" : "assert result[1] is left and result[2] is right";
        return $"class C:\n def read(self{parameters}):\n  with open('/value') as f:\n   value=f.read()\n  return ({returned})\nleft=[]\nright=[]\nc=C()\nresult=c.read({arguments})\nassert result[0] is c\n{assertions}\nreturn result[{width + 1}]\n";
    }
}
