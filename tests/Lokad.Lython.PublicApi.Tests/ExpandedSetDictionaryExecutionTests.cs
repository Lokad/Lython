using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpandedSetDictionaryExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetIterationCanSuspendAndCancel(bool cancel)
    {
        const string source = """
def items():
    with open('events.txt','a') as target: target.write('one')
    yield 1
    with open('events.txt','a') as target: target.write('two')
    yield 2
result={0,*items(),3}
print(sorted(result))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("before", host.ReadText("/events.txt"));
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[0, 1, 2, 3]\n", result.StandardOutput);
                Assert.Equal("beforeonetwo", host.ReadText("/events.txt"));
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
    [InlineData("{*source}")]
    [InlineData("{**{'item':seed}}")]
    public async Task EscapingContainersRemainFundedAcrossAnotherRun(string expression)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("return " + expression, budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed, ["source"] = new object[] { seed } } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { first, second })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>());
        }
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<string, string> Programs => new()
    {
        { "print({0,*range(3)}=={0,1,2},len({*'é😀'}))", "True 2\n" },
        { "d={**{'a':1,'same':0},'z':4,**{'same':2,'b':3}}\nprint(list(d),list(d.values()))", "['a', 'same', 'z', 'b'] [1, 2, 4, 3]\n" },
        { "events=[]\nclass Mapping:\n def keys(self):\n  events.append('keys')\n  def produce():\n   for key in ['a','b']:\n    events.append('key:'+key)\n    yield key\n  return produce()\n def __getitem__(self,key):\n  events.append('get:'+key)\n  return key.upper()\nd={**Mapping()}\nprint(d,events)", "{'a': 'A', 'b': 'B'} ['keys', 'key:a', 'key:b', 'get:a', 'get:b']\n" },
        { "events=[]\ndef mark():\n events.append('suffix')\n return 1\ntarget={'old':1}\ntry: target={**[('a',1)],'x':mark()}\nexcept TypeError: print(target,events)", "{'old': 1} []\n" },
        { "events=[]\ndef mark():\n events.append('suffix')\n return 1\ntarget={17}\ntry: target={*[[1]],mark()}\nexcept TypeError: print(target,events)", "{17} []\n" },
        { "class Key:\n def __hash__(self): return 1\n def __eq__(self,other): return isinstance(other,Key)\noriginal=Key()\nreplacement=Key()\nd={original:1,**{replacement:2}}\ns={original,*[replacement]}\nprint(len(d),next(iter(d)) is original,d[replacement],len(s))", "1 True 2 1\n" },
        { "def combine(base):\n prefix=3\n result={**base,'marker':prefix}\n return result,locals()['prefix']\nprint(combine({'a':1}))", "({'a': 1, 'marker': 3}, 3)\n" },
        { "def g():\n result={**(yield {'a':1}),'x':2}\n yield result\niterator=g()\nprint(next(iterator))\nprint(iterator.send({'b':3}))\ndef h():\n result={0,*(yield [1]),2}\n yield result\niterator=h()\nprint(next(iterator))\nprint(sorted(iterator.send((3,4))))", "{'a': 1}\n{'b': 3, 'x': 2}\n[1]\n[0, 2, 3, 4]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ResultsAndOwnershipMatchBothDrivers(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
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
    [InlineData(null)]
    [InlineData("/keys.txt")]
    [InlineData("/items.txt")]
    public async Task MappingProtocolsCanSuspendAndCancel(string? pausedPath)
    {
        const string source = """
class Mapping:
    def keys(self):
        with open('keys.txt','a') as target: target.write('keys')
        return ['a','b']
    def __getitem__(self,key):
        with open('items.txt','a') as target: target.write(key)
        return key.upper()
result={**Mapping(),'suffix':'x'}
print(result)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/keys.txt", "before");
        host.SeedFile("/items.txt", "before");
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
                Assert.Equal("{'a': 'A', 'b': 'B', 'suffix': 'x'}\n", result.StandardOutput);
                Assert.Equal("beforekeys", host.ReadText("/keys.txt"));
                Assert.Equal("beforeab", host.ReadText("/items.txt"));
            }
            else
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.Equal(pausedPath == "/keys.txt" ? "before" : "beforekeys", host.ReadText("/keys.txt"));
                Assert.Equal("before", host.ReadText("/items.txt"));
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
    [InlineData("{0,*range(10000)}")]
    [InlineData("{0:0,**dict.fromkeys(range(10000))}")]
    public async Task AdmissionFailurePreventsPublicationAndRefunds(string expression)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("result=" + expression + "\nwith open('result.txt','w') as target: target.write(str(result))", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/result.txt", "before");
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) };
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
}
