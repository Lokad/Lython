using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionDocstringLifetimeTests
{
    public static TheoryData<string> Definitions => new()
    {
        "def f():\n 'café😀'\n return 7\nassert f()==7",
        "def f(value:int=7)->int:\n 'café😀'\n return value\nassert f()==7",
        "def f[T](value:T=7)->T:\n 'café😀'\n return value\nassert f()==7",
        "def f():\n 'café😀'\n yield 7\nassert next(f())==7",
        "def f[T]():\n 'café😀'\n yield T\nassert next(f()) is f.__type_params__[0]",
        "class Values:\n def method(self):\n  'café😀'\n  return 7\nf=Values.method\nassert Values().method()==7",
        "def make():\n value=1\n def f():\n  'café😀'\n  return value\n value=7\n return f\nf=make()\nassert f()==7",
        "def make():\n def f(value=(yield 7)):\n  'café😀'\n  return value\n yield f\ng=make()\nassert next(g)==7\nf=g.send(9)\nassert f()==9",
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    public async Task DefinitionsKeepDocstringsAndMutableMetadata(string definition)
    {
        var source = definition + "\nprint(f.__doc__)\nf.__doc__='changed'\nprint(f.__doc__)\ndel f.__doc__\nprint(f.__doc__)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("café😀\nchanged\nNone\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocstringOwnersSurviveHostIoAndRetainedResults(bool cancel)
    {
        var document = string.Concat(Enumerable.Repeat("café😀", 1024));
        var source = "def values():\n '" + document + "'"
            + "\n yield 7\nwith open('/hold.txt','a') as target:target.write('done')\nreturn values.__doc__";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/hold.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/hold.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
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
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "before" : "beforedone", host.ReadText("/hold.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(document, result.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                var fresh = new MockLythonHost(); fresh.SeedFile("/hold.txt", "before");
                using (var other = script.Run(fresh))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.Equal(document, other.ReturnValue);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
                script.Dispose();
                Assert.True(budget.ReservedBytes > 0);
                Assert.Equal(document, result.ReturnValue);
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        if (cancel) Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
