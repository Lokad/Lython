using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LambdaExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "class Holder:\n method=lambda self,value=3:value\nf=Holder.method\nprint(f.__name__,f.__qualname__,f.__doc__,Holder().method())", "<lambda> Holder.<lambda> None 3\n" },
        { "f=lambda value=[]:value\nf.tag=7\nf().append(1)\nprint(f.tag,f(),f.__defaults__)", "7 [1] ([1],)\n" },
        { "events=[]\ndef default():\n events.append('default')\n return []\nf=lambda value=default():value\nf().append(1)\nprint(f(),events,f.__defaults__)", "[1] ['default'] ([1],)\n" },
        { "def outer(value):\n return lambda step:lambda:value+step\nf=outer(3)(4)\nprint(f(),f.__qualname__)", "7 outer.<locals>.<lambda>.<locals>.<lambda>\n" },
        { "f=lambda value:((local:=value+1),locals()['local'])\nprint(f(3))", "(4, 4)\n" },
        { "class Base:\n def value(self): return 3\nclass Child(Base):\n value=lambda self:super().value()+1\nprint(Child().value())", "4\n" },
        { "value=9\nclass Holder:\n value=2\n method=lambda self:value\nprint(Holder().method())", "9\n" },
        { "f=lambda value=3:(yield value)\ng=f()\nprint(next(g))\ntry:g.send(7)\nexcept StopIteration as done:print(done.value)", "3\n7\n" },
        { "def outer():\n value=1\n f=lambda:value\n value=2\n return f\nprint(outer()())", "2\n" },
        { "def outer():\n value=7\n def middle():\n  return lambda:value\n return middle\nprint(outer()()())", "7\n" },
        { "def make():\n f=lambda value=(yield 'default'):value+1\n yield f\ng=make()\nprint(next(g),g.send(3)(4))", "default 5\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LambdasPreservePythonScopesDefaultsAndMetadata(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LambdaBodyCanSuspendAndCancel(bool cancel)
    {
        const string source = "def read():\n with open('value.txt') as value:return value.read()\nf=lambda:read()\nprint(f())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "value");
        var entered = cancel ? host.PauseReadUntilCancellation("/value.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("value\n", result.StandardOutput);
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReturnedAliasKeepsItsOwnerAfterLambdaInvocation()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("f=lambda:seed\nreturn f()", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(seed, second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
