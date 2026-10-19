using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class DictionaryGetCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "d={'zero':0,'false':False,'none':None}\nprint(d.get('zero',9),d.get('false',9),d.get('none',9),d.get('missing'),d.get('missing',7))", "0 False None None 7\n" },
        { "d={1:'number',(2,3):'tuple'}\nprint(d.get(True),d.get((2,3)),d.get(4,9))", "number tuple 9\n" },
        { "d={}\ndef fallback():\n    print('default')\n    return 7\ntry: d.get([],fallback())\nexcept TypeError: print('unhashable')\nprint(d)", "default\nunhashable\n{}\n" },
        { "d={}\ndef argument():\n    print('argument')\n    return 'x'\ntry: d.get(key=argument())\nexcept TypeError: print('keyword')\ntry: d.get('x',default=7)\nexcept TypeError: print('keyword')\nprint(d)", "argument\nkeyword\nkeyword\n{}\n" },
        { "d={'x':7}\nmethod=d.get\nfor target in [method,dict.get.__get__(d,dict)]:\n    try: target(**{'key':'x'})\n    except TypeError: print('keyword')\ntry: dict.get(d,'x',default=9)\nexcept TypeError: print('keyword')\nprint(method('x'),dict.get(d,'x'))", "keyword\nkeyword\nkeyword\n7 7\n" },
        { "d={}\ntry: [d.get(key='x') for i in range(1)]\nexcept TypeError: print('keyword')\ntry: d.get()\nexcept TypeError: print('missing')\ntry: d.get(1,2,3)\nexcept TypeError: print('extra')", "keyword\nmissing\nextra\n" },
        { "d={'x':1}\ndef fallback():\n    d['x']=9\n    return 7\nprint(d.get('x',fallback()))", "9\n" },
        { "d={'x':7}\nold=d\ndef argument():\n    global d\n    d={'x':99}\n    return 'x'\nprint(d.get(argument(),0),d.get('x'),old)", "7 99 {'x': 7}\n" },
        { "class C:\n    def __getattribute__(self,name):\n        print('lookup',name)\n        return lambda key,default: (key,default)\ndef argument(value):\n    print('argument',value)\n    return value\nprint(C().get(argument(1),argument(2)))", "lookup get\nargument 1\nargument 2\n(1, 2)\n" },
        { "class C: get={}\ndef argument():\n    print('argument')\n    return 'x'\ntry: C().get(argument(),7)\nexcept TypeError: print('not-callable')", "argument\nnot-callable\n" },
        { "def f(key,default): return key,default\nclass C: pass\nc=C()\nc.get=f\nprint(c.get(1,2))", "(1, 2)\n" },
        { "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        print('eq')\n        return other=='x'\nkey=Key()\nd={key:7}\nprint(d.get('x',9))", "eq\n7\n" },
        { "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        d[key]=9\n        return other=='x'\nkey=Key()\nd={key:7}\nprint(d.get('x',0))", "9\n" },
        { "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        d.clear()\n        return True\nkey=Key()\nd={key:7}\nprint(d.get('x','missing'),d)", "missing {}\n" },
        { "d={}\nfallback=[]\nvalue=d.get('x',fallback)\nfallback.append(7)\nprint(value is fallback,value)\nd={'x':9}\nprint(d.get is d.get,d.get.__self__ is d)", "True [7]\nFalse True\n" },
        { "def produce(d):\n    yield d.get('x')\n    yield d.get('y',7)\nprint(list(produce({'x':3})))", "[3, 7]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task GetPreservesLookupBindingAndAliases(string source, string expected)
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
    }

    private const string YieldingLookup = "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        global d\n        d={'x':99}\n        with open('events.txt','a') as target: target.write('eq\\n')\n        return other=='x'\nkey=Key()\nd={key:7}\nold=d\nmethod=d.get\nfallback=[]\nprint(CALL)\nprint(old is d)";

    [Theory]
    [InlineData("d.get('x',fallback)")]
    [InlineData("method('x',fallback)")]
    [InlineData("dict.get(d,'x',fallback)")]
    [InlineData("d.get(*('x',fallback))")]
    [InlineData("[d.get('x',fallback) for i in range(1)][0]")]
    [InlineData("d['x']")]
    [InlineData("d.__getitem__('x')")]
    [InlineData("dict.__getitem__(d,'x')")]
    [InlineData("d.__getitem__(*('x',))")]
    [InlineData("[d['x'] for i in range(1)][0]")]
    [InlineData("reader('x')")]
    public async Task StoredKeyEqualityCanYieldAcrossCallForms(string call)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = YieldingLookup.Replace("CALL", call);
        if (call == "reader('x')") source = source.Replace("method=d.get", "reader=d.__getitem__");
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/events.txt", "");
            else mock!.SeedFile("/events.txt", "");
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("7\nFalse\n", result.StandardOutput);
                Assert.Equal("eq\n", delayed?.ReadText("/events.txt") ?? mock!.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("d.get('x',fallback)")]
    [InlineData("method('x',fallback)")]
    public async Task SuspendedMissKeepsTheEvaluatedDefault(string call)
    {
        var source = YieldingLookup.Replace("global d", "global d,fallback")
            .Replace("d={'x':99}", "d={'x':99}\n        fallback=None")
            .Replace("return other=='x'", "return False")
            .Replace("fallback=[]", "fallback=['a'*4096]\nsaved=fallback")
            .Replace("print(CALL)\nprint(old is d)", "value=CALL\nprint(value is saved)\nreturn value[0]")
            .Replace("CALL", call);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n", result.StandardOutput);
            Assert.Equal(new string('a', 4096), Assert.IsType<string>(result.ReturnValue));
            Assert.Equal("eq\n", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("d.get('x',fallback)")]
    [InlineData("d['x']")]
    [InlineData("d.__getitem__('x')")]
    public async Task CancelledEqualityDrainsBeforeAllowanceReturns(string call)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(YieldingLookup.Replace("CALL", call), budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/events.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/events.txt"));
        }
        finally
        {
            cancellation.Cancel();
            result?.Dispose();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("d={'x':'a'*4096}\nvalue=d.get('x')\nd.clear()\nreturn value")]
    [InlineData("fallback=['a'*4096]\nvalue={}.get('x',fallback)\nfallback=None\nreturn value[0]")]
    public async Task RetainedValuesAndDefaultsStayFunded(string source)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('a', 4096), Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(next.Success, next.Failure?.Message);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
