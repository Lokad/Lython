using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class FunctionDefaultMetadataTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(a=1,*,b=2): return a,b\nprint(f.__defaults__,f.__kwdefaults__)\nprint(f.__defaults__ is f.__defaults__,f.__kwdefaults__ is f.__kwdefaults__)", "(1,) {'b': 2}\nTrue True\n" },
        { "def f(x=5): return x\nf.__defaults__=(7,)\nprint(f())", "7\n" },
        { "def f(*,x=2): return x\ndefaults=f.__kwdefaults__\ndefaults['x']=9\nprint(f(),defaults is f.__kwdefaults__)", "9 True\n" },
        { "def f(a=1,*,b=2): return a,b\ndel f.__defaults__\ndel f.__kwdefaults__\nprint(f.__defaults__,f.__kwdefaults__)\ntry: f()\nexcept TypeError: print('missing')", "None None\nmissing\n" },
        { "def f(x=1): return x\ntry: f.__defaults__=[]\nexcept TypeError: print('tuple-required')\ntry: f.__kwdefaults__=()\nexcept TypeError: print('dict-required')\nprint(f())", "tuple-required\ndict-required\n1\n" },
        { "def f(a,b): return a,b\nf.__defaults__=(1,2,3)\nprint(f())", "(2, 3)\n" },
        { "def f(x=1): return x\ndef argument():\n    f.__defaults__=(9,)\n    return []\nprint(f(*argument()))", "9\n" },
        { "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        print('eq')\n        return True\ndef f(*,x): return x\nf.__kwdefaults__={Key():42}\nprint(f())", "eq\n42\n" },
        { "f=lambda x=2,*,y=3:(x,y)\nprint(f.__defaults__,f.__kwdefaults__)\nf.__defaults__=(5,)\nf.__kwdefaults__['y']=7\nprint(f())\ndel f.__defaults__\nprint(f.__defaults__)", "(2,) {'y': 3}\n(5, 7)\nNone\n" },
        { "def f(x=1,*,y=2): return x,y\nf.__defaults__=None\nf.__kwdefaults__=None\nprint(f.__defaults__,f.__kwdefaults__)\nprint(f(3,y=4))", "None None\n(3, 4)\n" },
        { "from collections import defaultdict,Counter\ndef f(*,x=1): return x\nd=defaultdict(lambda:99,x=7)\nf.__kwdefaults__=d\nprint(f(),f.__kwdefaults__ is d)\nc=Counter(x=8)\nf.__kwdefaults__=c\nprint(f(),f.__kwdefaults__ is c)\ndel c['x']\ntry: f()\nexcept TypeError: print('missing')", "7 True\n8 True\nmissing\n" },
        { "from collections import namedtuple\nDefaults=namedtuple('Defaults','value')\ndef f(x): return x\nd=Defaults(7)\nf.__defaults__=d\nprint(f(),f.__defaults__ is d)", "7 True\n" },
        { "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        f.__kwdefaults__={'x':99,'y':100}\n        return other=='x'\ndef f(*,x,y): return x,y\nf.__kwdefaults__={Key():7,'y':8}\nprint(f())\nprint(f())", "(7, 100)\n(99, 100)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task MetadataAndBindingUseTheSameLiveDefaults(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    private const string YieldingLookup = "class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        f.__kwdefaults__={'x':99,'y':100}\n        with open('events.txt','a') as target: target.write('eq\\n')\n        return other=='x'\ndef f(*,x,y): return x,y\nf.__kwdefaults__={Key():7,'y':8}\nprint(f())\nprint(f())";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeywordLookupCanYieldAndSubsequentParametersSeeReplacement(bool retainAlias)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = retainAlias
            ? YieldingLookup.Replace("f.__kwdefaults__={Key():7,'y':8}", "original={Key():7,'y':8}\nf.__kwdefaults__=original")
            : YieldingLookup;
        using var script = new LythonEngine().Compile(source, budget);
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
                Assert.Equal("(7, 100)\n(99, 100)\n", result.StandardOutput);
                Assert.Equal("eq\n", delayed?.ReadText("/events.txt") ?? mock!.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancelledKeywordLookupDrainsBeforeReturningAllowance()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(YieldingLookup, budget);
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
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task DeniedReplacementPreservesTheOriginalDefaults()
    {
        var budget = new LythonMemoryBudget(128 * 1024);
        using var script = new LythonEngine().Compile("def f(x=1): return x\nold=f.__defaults__\ntry: f.__defaults__=(0,)*10000\nexcept MemoryError: print('denied')\nprint(f(),f.__defaults__ is old)", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("denied\n1 True\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task EscapedDefaultsRemainFundedAfterMetadataIsCleared()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("def f(value='x'*4096): return value\nsaved=f.__defaults__\nf.__defaults__=None\nreturn saved[0]", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('x', 4096), Assert.IsType<string>(retained.ReturnValue));
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
