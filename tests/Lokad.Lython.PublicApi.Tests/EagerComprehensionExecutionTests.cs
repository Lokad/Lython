using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class EagerComprehensionExecutionTests
{
    [Fact]
    public async Task ReturnedStorageStaysFundedAfterIterationScopeEnds()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("return [seed for value in range(2)]", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { first, second })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(2, Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>().Count());
        }
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<string, string> Programs => new()
    {
        { "def convert(value): return value+1\nprint([convert(x) for x in range(5) if x%2])\nprint(sorted({convert(x) for x in range(3)}))\nprint({x:convert(x) for x in range(3)})", "[2, 4]\n[1, 2, 3]\n{0: 1, 1: 2, 2: 3}\n" },
        { "def run(values):\n value='outer'\n rows=[(value,locals()['value'],locals()['values']) for value in values]\n return rows,value,'value' in locals()\nprint(run([1,2]))", "([(1, 1, [1, 2]), (2, 2, [1, 2])], 'outer', True)\n" },
        { "value='outer'\nother='outside'\ntry: rows=[[1//(value-other) for other in range(2)] for value in range(2)]\nexcept ZeroDivisionError: print(value,other)\nprint([value+1 for value in range(2)],value,other)", "outer outside\n[1, 2] outer outside\n" },
        { "value=99\nlate=[lambda:value for value in range(3)]\nearly=[lambda saved=value:saved for value in range(3)]\nprint([f() for f in late],[f() for f in early],value)", "[2, 2, 2] [0, 1, 2] 99\n" },
        { "def run():\n last=-1\n values=[(last:=x+y) for x in range(3) if x for y in range(2)]\n return values,last,'x' in locals(),'y' in locals()\nprint(run())", "([1, 2, 2, 3], 3, False, False)\n" },
        { "values=[1,2,3]\nprint([values for values in values],values)\nprint([(x,y) for x in range(3) if x for y in range(x) if y==0])", "[1, 2, 3] [1, 2, 3]\n[(1, 0), (2, 0)]\n" },
        { "def run():\n yield [value+1 for value in (yield 'items')]\n yield 'value' in locals()\ng=run()\nprint(next(g),g.send([1,2]),next(g))", "items [2, 3] False\n" },
        { "def run():\n yield (value+1 for value in (yield 'items'))\ng=run()\nprint(next(g),list(g.send([1,2])))", "items [2, 3]\n" },
        { "events=[]\ndef values():\n try:\n  yield 1\n  yield 2\n finally: events.append('closed')\ng=values()\ntry: result=[1//0 for value in g]\nexcept ZeroDivisionError: pass\nprint(events,next(g))", "[] 2\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task EagerScopesMatchPython(string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/iterate.txt")]
    [InlineData("/filter.txt")]
    [InlineData("/item.txt")]
    public async Task IterationTruthAndHelperCanSuspendWithoutReplay(string? pausedPath)
    {
        const string source = """
class Values:
    def __iter__(self):
        with open('iterate.txt','a') as output: output.write('iterate')
        return iter([1,2,3])
class Guard:
    def __init__(self,value): self.value=value
    def __bool__(self):
        with open('filter.txt','a') as output: output.write('f'+str(self.value))
        return self.value!=2
def convert(value):
    with open('item.txt','a') as output: output.write('i'+str(value))
    return value*2
print([convert(value) for value in Values() if Guard(value)])
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in new[] { "/iterate.txt", "/filter.txt", "/item.txt" }) host.SeedFile(path, "before");
        var entered = pausedPath is null ? null : host.PauseWriteUntilCancellation(pausedPath);
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
            if (pausedPath is null)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[2, 6]\n", result.StandardOutput);
            }
            else
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(pausedPath == "/iterate.txt" ? "before" : "beforeiterate", host.ReadText("/iterate.txt"));
            Assert.Equal(pausedPath is null ? "beforef1f2f3" : pausedPath == "/item.txt" ? "beforef1" : "before", host.ReadText("/filter.txt"));
            Assert.Equal(pausedPath is null ? "beforei1i3" : "before", host.ReadText("/item.txt"));
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
