using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MethodDefinitionHostBoundaryTests
{
    private static readonly string[] Phases =
        ["first", "second", "default", "keyword-default", "argument", "result", "second-apply", "first-apply", "body"];
    private const string Expected = "['first', 'second', 'default', 'keyword-default', 'argument', 'result', 'second-apply', 'first-apply', 'body']\n";
    private const string Source = """
        events=[]
        def mark(label,value):
            events.append(label)
            with open('/'+label) as f:
                f.read()
            return value
        def deco(label):
            mark(label,None)
            def apply(fn):
                mark(label+'-apply',None)
                return fn
            return apply
        class C[T]:
            @deco('first')
            @deco('second')
            def run[U](self,x:mark('argument',U)=mark('default',3),*,y=mark('keyword-default',4))->mark('result',T):
                'method documentation'
                return mark('body',x+y)
        result=C().run()
        print(events)
        return result
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MethodDefinitionAndBodyPreserveOrderThroughActualHostReads(bool asynchronous)
    {
        var compiled = new LythonEngine().Compile(Source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = Seed(new DelayedLythonHost());
            result = await compiled.RunAsync(host);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        else
        {
            var host = new MockLythonHost();
            foreach (var phase in Phases) host.SeedFile("/" + phase, "ready");
            result = compiled.Run(host);
        }
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7", result.ReturnValue?.ToString());
        Assert.Equal(Expected, result.StandardOutput);
    }

    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("default")]
    [InlineData("keyword-default")]
    [InlineData("argument")]
    [InlineData("result")]
    [InlineData("second-apply")]
    [InlineData("first-apply")]
    [InlineData("body")]
    public async Task CancellationDuringEveryDefinitionPhaseLeavesTheScriptReusable(string pausedPhase)
    {
        var compiled = new LythonEngine().Compile(Source);
        Assert.True(compiled.IsValid);
        var host = Seed(new DelayedLythonHost());
        var started = host.PauseReadUntilCancellation("/" + pausedPhase);
        using var cancellation = new CancellationTokenSource();
        var pending = compiled.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try { await started.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancellation.Cancel(); }
        var canceled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(canceled.Success);
        Assert.Contains("execution canceled", canceled.Failure?.Message, StringComparison.Ordinal);
        Assert.Empty(canceled.StandardOutput);
        var fresh = Seed(new DelayedLythonHost());
        var result = await compiled.RunAsync(fresh);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7", result.ReturnValue?.ToString());
        Assert.Equal(Expected, result.StandardOutput);
        Assert.True(fresh.CompletedAsynchronously > 0);
    }

    private static DelayedLythonHost Seed(DelayedLythonHost host)
    {
        foreach (var phase in Phases) host.SeedFile("/" + phase, "ready");
        return host;
    }
}
