using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExactIntegerUnaryTests
{
    [Theory]
    [InlineData("-")]
    [InlineData("~")]
    public async Task ReturnedHeapIntegersKeepTheirSurvivingOwner(string token)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("return " + token + "seed", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var expected = token == "-" ? -seed : ~seed;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(expected, Assert.IsType<System.Numerics.BigInteger>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost(), options)) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    public static TheoryData<string, string> Programs => new()
    {
        { "for value in (-1,0,1,1<<100):\n print(+value is value,-value,~value,not value)", "True 1 0 False\nTrue 0 -1 True\nTrue -1 -2 False\nTrue -1267650600228229401496703205376 -1267650600228229401496703205377 False\n" },
        { "events=[]\nclass Base: pass\nclass Number(Base):\n def __init__(self,value): pass\n def __pos__(self):\n  events.append('pos')\n  return 41\n def __neg__(self):\n  events.append('neg')\n  return 42\n def __invert__(self):\n  events.append('invert')\n  return 43\n def __bool__(self):\n  events.append('bool')\n  return False\nvalue=Number(7)\nprint(+value,-value,~value,not value,events)", "41 42 43 True ['pos', 'neg', 'invert', 'bool']\n" },
        { "events=[]\nclass Value:\n def __bool__(self):\n  events.append('bool')\n  return True\nprint(not Value(),events)", "False ['bool']\n" },
        { "class Value:\n __neg__=None\ntry: print(-Value())\nexcept TypeError: print('not-callable')", "not-callable\n" },
        { "class Value:\n def __invert__(self): return NotImplemented\nprint(~Value() is NotImplemented)", "True\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ScalarAndExcludedProtocolsMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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
    [InlineData("+", "pos", false)]
    [InlineData("-", "neg", false)]
    [InlineData("~", "invert", false)]
    [InlineData("not ", "bool", false)]
    [InlineData("+", "pos", true)]
    [InlineData("-", "neg", true)]
    [InlineData("~", "invert", true)]
    [InlineData("not ", "bool", true)]
    public async Task GuestProtocolsStillSuspendAndCancelWithoutReplay(string token, string method, bool cancel)
    {
        var source = $$"""
class Base: pass
class Number(Base):
    def __init__(self,value): pass
    def __{{method}}__(self):
        with open('events.txt','a') as target: target.write('called')
        return {{(method == "bool" ? "True" : "42")}}
value='before'
value={{token}}Number(7)
print(value)
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before its host operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel) Assert.False(result.Success);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : method == "bool" ? "False\n" : "42\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforecalled", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
