using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpandedCallExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def sink(*args,**kw): return args,list(kw.items())\nprint(sink(1,*[2,3],flag=4,**{'end':5}))", "((1, 2, 3), [('flag', 4), ('end', 5)])\n" },
        { "events=[]\nclass Values:\n def __iter__(self):\n  events.append('iterate')\n  return iter([1])\ndef mark():\n events.append('keyword')\n return 2\ndef sink(*args,**kw): return args,kw\nprint(sink(*Values(),flag=mark()),events)", "((1,), {'flag': 2}) ['keyword', 'iterate']\n" },
        { "events=[]\ndef sink(**kw): return kw\nclass Mapping:\n def keys(self):\n  def produce():\n   for key in ['a','b']:\n    events.append('key:'+key)\n    yield key\n  return produce()\n def __getitem__(self,key):\n  events.append('get:'+key)\n  return key.upper()\nprint(sink(**Mapping()),events)", "{'a': 'A', 'b': 'B'} ['key:a', 'key:b', 'get:a', 'get:b']\n" },
        { "events=[]\ndef sink(**kw): events.append('called')\nclass Mapping:\n def keys(self): return ['a']\n def __getitem__(self,key):\n  events.append('get')\n  return 2\ntry: sink(a=1,**Mapping())\nexcept TypeError: print(events)", "[]\n" },
        { "events=[]\ndef sink(**kw): events.append('called')\nclass Mapping:\n def keys(self): return [1]\n def __getitem__(self,key):\n  events.append('get')\n  return 2\ntry: sink(**Mapping())\nexcept TypeError: print(events)", "['get']\n" },
        { "def outer(value):\n def invoke(target,values):\n  local=value+1\n  result=target(*values,key=local)\n  return result,locals()['local']\n return invoke\ndef sink(*args,**kw): return args,kw\nprint(outer(3)(sink,[1,2]))", "(((1, 2), {'key': 4}), 4)\n" },
        { "def sink(*args,**kw): return args,kw\ndef run():\n yield sink(*(yield 'values'),**(yield 'keywords'))\ng=run()\nprint(next(g),g.send([1,2]),g.send({'end':3}))", "values keywords ((1, 2), {'end': 3})\n" },
        { "events=[]\nclass Values:\n def __iter__(self):\n  events.append('iterate')\n  return iter([1])\nclass Mapping:\n def keys(self):\n  events.append('keys')\n  return ['end']\n def __getitem__(self,key):\n  events.append('get:'+key)\n  return 2\ndef sink(*args,**kw):\n events.append('call')\n return args,kw\nprint(sink(*Values(),**Mapping()),events)", "((1,), {'end': 2}) ['keys', 'get:end', 'iterate', 'call']\n" },
        { "def inspect():\n value=7\n return locals(*[])['value']\nprint(inspect())", "7\n" },
        { "class Base:\n def value(self): return 3\nclass Child(Base):\n def value(self): return super(*[]).value()+1\nprint(Child().value())", "4\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExpansionPreservesPythonEffectsAndScopes(string source, string expected)
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
    [InlineData("/keys.txt")]
    [InlineData("/items.txt")]
    [InlineData("/call.txt")]
    public async Task IterationMappingAndCallableCanSuspendWithoutReplay(string? pausedPath)
    {
        const string source = """
class Values:
    def __iter__(self):
        with open('iterate.txt','a') as target: target.write('iterate')
        return iter([1,2])
class Mapping:
    def keys(self):
        with open('keys.txt','a') as target: target.write('keys')
        return ['end']
    def __getitem__(self,key):
        with open('items.txt','a') as target: target.write(key)
        return 3
class Target:
    def __call__(self,*args,**kw):
        with open('call.txt','a') as target: target.write('called')
        return args,kw
print(Target()(*Values(),**Mapping()))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var paths = new[] { "/iterate.txt", "/keys.txt", "/items.txt", "/call.txt" };
        var effects = new[] { "iterate", "keys", "end", "called" };
        // A sole * is expanded after ** expressions, but ** mapping expansion
        // precedes its deferred iteration, as in the existing accumulator.
        var order = new[] { "/keys.txt", "/items.txt", "/iterate.txt", "/call.txt" };
        var host = new DelayedLythonHost();
        foreach (var path in paths) host.SeedFile(path, "before");
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
                Assert.Equal("((1, 2), {'end': 3})\n", result.StandardOutput);
            }
            else
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            for (var i = 0; i < paths.Length; i++)
            {
                var completed = pausedPath is null || Array.IndexOf(order, paths[i]) < Array.IndexOf(order, pausedPath);
                Assert.Equal("before" + (completed ? effects[i] : ""), host.ReadText(paths[i]));
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
    public async Task AdmissionDenialPreventsCallingTheTarget()
    {
        const string source = "def sink(*args):\n with open('result.txt','w') as output: output.write('called')\nsink(*range(10000))";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/result.txt", "before");
            var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
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

    [Fact]
    public async Task ReturnedArgumentsRemainFundedAfterTemporaryCleanup()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("def sink(*args): return args\nreturn sink(*values)", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["values"] = new object[] { seed } } };
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
}
