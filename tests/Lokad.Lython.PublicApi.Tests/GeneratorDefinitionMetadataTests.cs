using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorDefinitionMetadataTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def values(x:int=3)->int:\n 'numbers'\n yield x\nprint(next(values()),values.__annotations__=={'x':int,'return':int},values.__doc__)", "3 True numbers\n" },
        { "events=[]\ndef decorate():\n events.append('decorator')\n def apply(f):\n  events.append('apply')\n  assert f.__annotations__=={'x':int,'return':int}\n  return f\n return apply\ndef value(name):\n events.append(name)\n return 3 if name=='default' else int\n@decorate()\ndef values(x:value('annotation')=value('default'))->value('return'):\n yield x\nprint(next(values()),events)", "3 ['decorator', 'default', 'annotation', 'return', 'apply']\n" },
        { "from __future__ import annotations\ndef values(x:missing())->Result:\n yield x\nprint(next(values(7)),values.__annotations__=={'x':'missing()','return':'Result'})", "7 True\n" },
        { "def make():\n T=17\n def values[T](x:T=T)->T:\n  yield x,T\n T=20\n return values\nf=make()\nx,t=next(f())\nprint(x,t is f.__type_params__[0],f.__annotations__['x'] is t,f.__annotations__['return'] is t)", "17 True True True\n" },
        { "def outer[T](value):\n def values()->T:\n  yield value,T\n value=7\n return values\nf=outer(2)\nx,t=next(f())\nprint(x,t is f.__annotations__['return'],t.__name__)", "7 True T\n" },
        { "def make():\n n=1\n def values[T]():\n  nonlocal n\n  n+=1\n  yield n,T\n return values\nf=make()\na=next(f())\nb=next(f())\nprint(a[0],b[0],a[1] is b[1])", "2 3 True\n" },
        { "def make():\n value=1\n def values(x:int=0):\n  yield value+x\n value=2\n return values\nf=make()\nprint(list(f()),list(f(3)))", "[2] [5]\n" },
        { "events=[]\ndef bound():events.append('bound');return int\ndef values[T:bound()]()->T:\n yield T\nt=next(values())\nprint(events,t is values.__type_params__[0])\nprint(t.__bound__ is int,t.__bound__ is int,events)", "[] True\nTrue True ['bound']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task MetadataAndCapturedNamespacesPreservePython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("cancel")]
    [InlineData("error")]
    [InlineData("memory")]
    public async Task SuspendedMetadataKeepsOwnersUntilCleanup(string mode)
    {
        const string source = """
            def make():
                payload='held'*1024
                def annotate():
                    with open('/hold.txt','a') as target:
                        target.write('done')
                    if mode=='error':
                        raise ValueError('annotation failed')
                    if mode=='memory':
                        return 'x'*8388608
                    return str
                def values[T](value:annotate())->T:
                    yield value,payload,T
                return values
            try:
                values=make()
            except MemoryError:
                print('suppressed')
            value,payload,parameter=next(values('argument'))
            print(value,parameter is values.__type_params__[0])
            return payload
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/hold.txt", "before");
        var entered = mode == "cancel" ? host.PauseWriteUntilCancellation("/hold.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["mode"] = mode } };
        var pending = script.RunAsync(host, options, cancellation.Token);
        LythonExecutionResult? result = null;
        try
        {
            if (entered is not null)
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(mode == "ok", result.Success);
            Assert.Equal(mode == "ok" ? "argument True\n" : "", result.StandardOutput);
            Assert.Equal(mode == "cancel" ? "before" : "beforedone", host.ReadText("/hold.txt"));
            if (mode == "cancel") Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else if (mode == "memory") Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            else if (mode == "error") Assert.Equal("ValueError", result.Failure?.ExceptionType);
            else
            {
                Assert.Equal(string.Concat(Enumerable.Repeat("held", 1024)), result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost(); fresh.SeedFile("/hold.txt", "before");
        options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["mode"] = "ok" } };
        using (var reused = await script.RunAsync(fresh, options)) Assert.True(reused.Success, reused.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
