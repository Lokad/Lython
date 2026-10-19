using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StarredDisplayExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "print([0,*range(3),3,*'é😀'])", "[0, 0, 1, 2, 3, 'é', '😀']\n" },
        { "def combine(left,right):\n marker=7\n return [*left,marker,*right]\nprint(combine([1,2],(3,4)))", "[1, 2, 7, 3, 4]\n" },
        { "events=[]\ndef mark(name,value):\n events.append(name)\n return value\ndef items():\n events.append('pull1')\n yield 1\n events.append('pull2')\n yield 2\nresult=[mark('a',0),*mark('iterable',items()),mark('b',3),*[4,5]]\nprint(result)\nprint(','.join(events))", "[0, 1, 2, 3, 4, 5]\na,iterable,pull1,pull2,b\n" },
        { "events=[]\ntarget=['old']\ntry: target=[1,*None,events.append('suffix')]\nexcept TypeError: print(target,events)", "['old'] []\n" },
        { "marker=[]\nresult=[*([marker]*2),marker]\nresult[0].append(9)\nprint(result,result[0] is result[2])", "[[9], [9], [9]] True\n" },
        { "def make():\n x=7\n def inner(): return x\n result=[*range(2),x,inner()]\n x=9\n return result,inner(),locals()['x']\nprint(make())", "([0, 1, 7, 7], 9, 9)\n" },
        { "def g():\n values=[0,*(yield [1]),2]\n yield values\niterator=g()\nprint(next(iterator))\nprint(iterator.send((3,4)))", "[1]\n[0, 3, 4, 2]\n" },
        { "events=[]\ndef items():\n try:\n  yield 1\n  raise ValueError('bad')\n finally: events.append('closed')\ntarget=['old']\ntry: target=[0,*items(),2]\nexcept ValueError: print(target,events)", "['old'] ['closed']\n" },
    };

    public static TheoryData<string, string> TuplePrograms => new()
    {
        { "print((0,*range(3),3,*'é😀'))", "(0, 0, 1, 2, 3, 'é', '😀')\n" },
        { "def combine(left,right):\n marker=7\n return (*left,marker,*right)\nprint(combine([1,2],(3,4)))", "(1, 2, 7, 3, 4)\n" },
        { "events=[]\ndef mark(name,value):\n events.append(name)\n return value\ndef items():\n events.append('pull1')\n yield 1\n events.append('pull2')\n yield 2\nresult=(mark('a',0),*mark('iterable',items()),mark('b',3),*[4,5])\nprint(result)\nprint(','.join(events))", "(0, 1, 2, 3, 4, 5)\na,iterable,pull1,pull2,b\n" },
        { "events=[]\ntarget=('old',)\ntry: target=(1,*None,events.append('suffix'))\nexcept TypeError: print(target,events)", "('old',) []\n" },
        { "marker=[]\nresult=(*([marker]*2),marker)\nresult[0].append(9)\nprint(result,result[0] is result[2])", "([9], [9], [9]) True\n" },
        { "def make():\n x=7\n def inner(): return x\n result=(*range(2),x,inner())\n x=9\n return result,inner(),locals()['x']\nprint(make())", "((0, 1, 7, 7), 9, 9)\n" },
        { "def g():\n values=(0,*(yield [1]),2)\n yield values\niterator=g()\nprint(next(iterator))\nprint(iterator.send((3,4)))", "[1]\n(0, 3, 4, 2)\n" },
        { "print((*[],),(*[1],),(*[],*()))", "() (1,) ()\n" },
        { "empty=tuple()\nliteral=()\nprint(literal is empty,(*[],) is empty,(*[],*()) is empty)\nclass Holder: empty=(*[],)\nprint(Holder.empty is empty)", "True True True\nTrue\n" },
    };

    [Theory]
    [MemberData(nameof(TuplePrograms))]
    public Task TupleResultsAndCleanupSurviveBothDrivers(string source, string expected)
        => PythonResultsAndCleanupSurviveBothDrivers(source, expected);

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task PythonResultsAndCleanupSurviveBothDrivers(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UserIterationCanActuallySuspendAndCancel(bool cancel, bool tuple)
    {
        var source = $$"""
class Items:
    def __init__(self): self.index=0
    def __iter__(self): return self
    def __next__(self):
        if self.index==2: raise StopIteration
        with open('events.txt','a') as target: target.write(str(self.index))
        self.index+=1
        return self.index
result={{(tuple ? "(-1,*Items(),99)" : "[-1,*Items(),99]")}}
print(result)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal("before", host.ReadText("/events.txt"));
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("before", host.ReadText("/events.txt"));
                Assert.Empty(result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(tuple ? "(-1, 1, 2, 99)\n" : "[-1, 1, 2, 99]\n", result.StandardOutput);
                Assert.Equal("before01", host.ReadText("/events.txt"));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedGrowthDoesNotPublishThePartialDisplay(bool tuple)
    {
        var source = "target=[17]\ntarget=" + (tuple ? "(1,*range(10000),2)" : "[1,*range(10000),2]") + "\nwith open('result.txt','w') as output: output.write(str(target))";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) };
            var host = new MockLythonHost();
            host.SeedFile("/result.txt", "before");
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal("before", host.ReadText("/result.txt"));
                Assert.InRange(result.PeakExecutionMemoryBytes, 1, 131072);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingDisplaysKeepTheirMagnitudeAliasesFunded(bool tuple)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(tuple ? "return (*source,source[0])" : "return [*source,source[0]]", budget);
        var baseline = budget.ReservedBytes;
        var seed = BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = new object[] { seed } } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { first, second })
        {
            Assert.True(result.Success, result.Failure?.Message);
            if (tuple) Assert.Equal(new object?[] { seed, seed }, Assert.IsType<object?[]>(result.ReturnValue));
            else Assert.Equal(new object?[] { seed, seed }, Assert.IsType<List<object?>>(result.ReturnValue));
        }
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
