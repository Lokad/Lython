using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonOptionUnwindTests
{
    public static TheoryData<string, string> Boundaries => new()
    {
        { "truth", "class Flag:\n def __bool__(self):read();return False\njson.dumps([1],ensure_ascii=Flag())" },
        { "getter", "class E(json.JSONEncoder):\n def __getattribute__(self,name):\n  if name=='ensure_ascii':read()\n  return object.__getattribute__(self,name)\nE().encode([1])" },
        { "setter", "class E(json.JSONEncoder):\n def __setattr__(self,name,value):\n  if name=='ensure_ascii':read()\n  object.__setattr__(self,name,value)\nE().encode([1])" },
        { "descriptor", "class E(json.JSONEncoder):\n @property\n def ensure_ascii(self):return self._ascii\n @ensure_ascii.setter\n def ensure_ascii(self,value):read();self._ascii=value\nE().encode([1])" },
        { "separators", "def separators():\n read()\n yield ','\n yield ':'\njson.dumps([1,2],separators=separators())" },
        { "override", "class E(json.JSONEncoder):\n def iterencode(self,value,_one_shot=False):read();return iter(['custom'])\nE().encode([1])" },
        { "decoder-truth", "class Flag:\n def __bool__(self):read();return False\njson.JSONDecoder(parse_int=Flag()).decode('1')" },
        { "sorted-default", "class Value:pass\nlist(json.JSONEncoder(sort_keys=True,default=lambda x:read()).iterencode({'a':Value(),'z':1}))" },
    };

    private const string Read = """
        import json
        def read():
            with open('/cleanup','w') as target:
                target.write('done')
                target.flush()
                with open('/value') as source:
                    source.read()
                try:
                    if fail: storage='x'*4194304
                finally:
                    return 1
        """;

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task InFlightCancellationAwaitsCleanupAndBudgetReuse(string name, string boundary)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Read + "\n" + boundary, budget);
        Assert.True(script.IsValid, name);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/value", "1");
        var started = host.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            Globals = new Dictionary<string, object?> { ["fail"] = false }
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal("done", host.ReadText("/cleanup"));
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost(); fresh.SeedFile("/value", "1");
        using (var reused = await script.RunAsync(fresh, new LythonRunOptions
            { Globals = new Dictionary<string, object?> { ["fail"] = false } }))
        {
            Assert.True(reused.Success, reused.Failure?.Message);
            Assert.True(fresh.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task RefusalThroughFinallyIsTerminalAndKeepsEarlierEffects(string name, string boundary)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var source = Read + "\ntry:\n " + boundary.Replace("\n", "\n ")
            + "\nexcept MemoryError:pass\nwith open('/later','w') as target:target.write('bad')";
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/value", "1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/value", "1");
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = true } };
            using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(immediate, options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal("done", asynchronous ? delayed.ReadText("/cleanup") : immediate.ReadText("/cleanup"));
            Assert.Throws<InvalidOperationException>(() => asynchronous ? delayed.ReadText("/later") : immediate.ReadText("/later"));
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
