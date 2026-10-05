using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorFunctionScenarioTests
{
    [Theory]
    [InlineData("next(g)")]
    [InlineData("g.send(None)")]
    [InlineData("list(g)")]
    [InlineData("list(Container())")]
    public async Task PullsAndDelegationAwaitHostEffects(string pull)
    {
        var script = Compile("""
            events=[]
            def child():
                with open('/data.txt') as f:
                    events.append('read'+f.read())
                yield 1
            def generate():
                yield from child()
            class Container:
                def __iter__(self):
                    yield from generate()
            g=generate()
            print(events)
            """ + "\n" + pull + "\nprint(events)\n");
        var host = new MockLythonHost();
        host.SeedFile("/data.txt", "!");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("[]\n['read!']\n", sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/data.txt", "!");
        var result = await script.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("g.close()", "GeneratorExit")]
    [InlineData("g.throw(ValueError('oops'))", "ValueError")]
    public async Task InjectedExceptionsAwaitContextCleanup(string action, string kind)
    {
        var script = Compile("""
            events=[]
            class Manager:
                def __enter__(self):return self
                def __exit__(self, kind, value, trace):
                    with open('/data.txt') as f:
                        events.append(kind.__name__+f.read())
                    return False
            def generate():
                with Manager():
                    try:yield 1
                    finally:
                        with open('/data.txt') as f:
                            events.append('finally'+f.read())
            g=generate()
            print(next(g))
            try:
            """ + "\n    " + action + "\nexcept ValueError:pass\nprint(events)\n");
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/data.txt", "!");
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal($"1\n['finally!', '{kind}!']\n", sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/data.txt", "!");
        var result = await script.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }

    [Fact]
    public async Task RetainedFramesRemainCharged()
    {
        var script = Compile("""
            def generate(value):
                yield value
            kept=[]
            for i in range(10000):
                kept.append(generate(i))
            print('unreachable')
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal("", result.StandardOutput);
        }
    }

    [Fact]
    public async Task CancellationDuringPullDoesNotPoisonReusableScript()
    {
        var script = Compile("""
            def generate():
                with open('/data.txt') as f:
                    yield f.read()
            print(list(generate()))
            """);
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", "!");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/data.txt");
        var running = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await entered.WaitAsync(TimeSpan.FromSeconds(20));
        cancellation.Cancel();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/data.txt", "!");
        var rerun = await script.RunAsync(fresh);
        Assert.True(rerun.Success, rerun.Failure?.Message);
        Assert.Equal("['!']\n", rerun.StandardOutput);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        return script;
    }
}
