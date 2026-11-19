using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class FunctionDefaultMetadataTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def add(a=1,*,b=2): return a+b\nprint(add(),add(3,b=4),add(b=8))\nadd.__defaults__=(10,)\nadd.__kwdefaults__={'b':20}\nprint(add(),add(b=5))", "3 7 9\n30 15\n" },
        { "saved=[]\ndef pick(*,value=saved): return value\nprint(pick() is saved,pick(value=saved) is saved)\npick.__kwdefaults__={'value':['changed']}\npicked=pick()\nprint(picked is pick.__kwdefaults__['value'],saved)", "True True\nTrue []\n" },
        { "def f(a=0,b=1,c=2,d=3,e=4,f=5,g=6,h=7,i=8,j=9,k=10,l=11,m=12,n=13,o=14,p=15,q=16): return q\nprint(f(),f(q=23),f(0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16))", "16 23 16\n" },
        { "def f(a=1,*,b=2): return a,b\nprint(f.__defaults__,f.__kwdefaults__)\nprint(f.__defaults__ is f.__defaults__,f.__kwdefaults__ is f.__kwdefaults__)", "(1,) {'b': 2}\nTrue True\n" },
        { "def f(x=5): return x\nf.__defaults__=(7,)\nprint(f())", "7\n" },
        { """
            shared = []
            def factory(n):
                def source(value=n+10000, alias=shared, real=n/2, large=10**80):
                    return value, alias, real, large
                defaults = source.__defaults__
                return source, lambda copy=defaults[0], *, alias=defaults[1]: (copy, alias)
            function, copy = factory(3)
            defaults = function.__defaults__
            values = function()
            print(values[0], values[1] is shared, values[2], values[3] == 10**80)
            print(copy()[0] is defaults[0], copy()[1] is shared)
            print(tuple(x is y for x,y in zip(defaults, values)))
            """, "10003 True 1.5 True\nTrue True\n(True, True, True, True)\n" },
        { "def f(*,x=2): return x\ndefaults=f.__kwdefaults__\ndefaults['x']=9\nprint(f(),defaults is f.__kwdefaults__)", "9 True\n" },
        { """
            def factory(seed):
                def f(*, before, left=seed, middle, right=seed):
                    return before, left, middle, right
                return f
            first, second = factory(2), factory(3)
            print(first(before=0, middle=1))
            defaults = second.__kwdefaults__
            first.__kwdefaults__ = defaults
            defaults['right'] = 4
            print(first(before=0, middle=1))
            del defaults['left']
            try: first(before=0, middle=1)
            except TypeError: print('missing')
            defaults['left'] = []
            print(first(before=0, middle=1)[1] is defaults['left'])
            """, "(0, 2, 1, 2)\n(0, 3, 1, 4)\nmissing\nTrue\n" },
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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

    [Fact]
    public async Task BoundPositionalDefaultSurvivesReplacementDuringSuspendedKeywordLookup()
    {
        const string source = """
            def make(seed):
                def f(value=seed+7, /, *, flag): return value
                return f
            f = make(10003)
            initial = f.__defaults__
            replacement = 10021
            class Key:
                def __hash__(self): return hash('flag')
                def __eq__(self, other):
                    f.__defaults__ = (replacement,)
                    f.__kwdefaults__ = {'flag': None}
                    with open('/events.txt', 'a') as events: events.write('replaced')
                    return other == 'flag'
            f.__kwdefaults__ = {Key(): None}
            value = f()
            print(value, value is initial[0], f() is replacement)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("10010 True True\n", result.StandardOutput);
            Assert.Equal("replaced", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task KeywordLookupCanYieldAndSubsequentParametersSeeReplacement(bool retainAlias, bool singleResult)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = retainAlias
            ? YieldingLookup.Replace("f.__kwdefaults__={Key():7,'y':8}", "original={Key():7,'y':8}\nf.__kwdefaults__=original")
            : YieldingLookup;
        if (singleResult) source = source.Replace("def f(*,x,y): return x,y", "def f(*,x,y): return x");
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
                Assert.Equal(singleResult ? "7\n99\n" : "(7, 100)\n(99, 100)\n", result.StandardOutput);
                Assert.Equal("eq\n", delayed?.ReadText("/events.txt") ?? mock!.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledKeywordLookupDrainsBeforeReturningAllowance(bool singleResult)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = singleResult ? YieldingLookup.Replace("def f(*,x,y): return x,y", "def f(*,x,y): return x") : YieldingLookup;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
    public async Task KeywordOverflowKeepsOriginalAliasesWhileDefaultLookupSuspends()
    {
        var source = YieldingLookup
            .Replace("def f(*,x,y): return x,y",
                "def f(*,x,y,**extra): return x,y,next(iter(extra)) is name,extra[name] is retained")
            .Replace("print(f())\nprint(f())",
                "name=''.join(['café','_field'])\nretained=[]\nprint(f(**{name:retained}))\nprint(f(**{name:retained}))");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "");

        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(7, 100, True, True)\n(99, 100, True, True)\n", result.StandardOutput);
            Assert.Equal("eq\n", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task DeniedDefaultReplacementFailsBeforeGuestContinuation()
    {
        var budget = new LythonMemoryBudget(128 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("def f(x=1): return x\nold=f.__defaults__\ntry: f.__defaults__=(0,)*10000\nexcept MemoryError: print('denied')\nprint(f(),f.__defaults__ is old)", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SuspendingDefaultRefusalKeepsArgumentsAndCannotResumeTheCaller(bool asynchronous, bool singleResult)
    {
        const string source = """
class Key:
 def __hash__(self): return hash('x')
 def __eq__(self,other):
  global payload
  payload=None
  f.__kwdefaults__={'x':99,'y':100}
  with open('events.txt','a') as target: target.write('eq\n')
  if fail:
   try: unused='z'*2000000
   except MemoryError: print('caught-default')
   finally: return True
  return other=='x'
def f(value,*,x,y): return len(value[0]),x,y
f.__kwdefaults__={Key():7,'y':8}
payload=['é'*8192]
try:
 print(f(value=payload))
except BaseException:
 print('caught-caller')
finally:
 if fail: return 0
""";
        var budget = new LythonMemoryBudget(512 * 1024);
        var program = singleResult ? source.Replace("return len(value[0]),x,y", "return len(value[0])") : source;
        using var script = new LythonEngine().Compile(program, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var fail in new[] { true, false })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/events.txt", "");
            else mock!.SeedFile("/events.txt", "");
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = fail } };
            using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
            {
                Assert.Equal(!fail, result.Success);
                if (fail)
                {
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Empty(result.StandardOutput);
                }
                else Assert.Equal(singleResult ? "8192\n" : "(8192, 7, 100)\n", result.StandardOutput);
                Assert.Equal("eq\n", delayed?.ReadText("/events.txt") ?? mock!.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RefusedKeywordStoragePrecedesDefaultExpressionEffects(bool asynchronous, bool lambda)
    {
        var defaults = string.Join(", ", Enumerable.Range(0, 64).Select(i => $"key{i}=0"));
        var declaration = lambda
            ? $"target = lambda *, effect=time.time(), {defaults}: effect"
            : $"def target(*, effect=time.time(), {defaults}): return effect";
        using var script = new LythonEngine().Compile($"""
            import time
            time.time()
            {declaration}
            print('unreachable')
            """);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 4096);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.True(budget.AtGate > 0);
            Assert.Equal(0, host.AtSecondGate);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDefinitionRefundsDefaultStorageOnlyWhilePrivate(bool asynchronous)
    {
        const int keywordCount = 512;
        var parameters = string.Join(", ", Enumerable.Range(0, keywordCount).Select(i => "key" + i + "=shared"));
        var afterPrivateFailure = await Run(escapesThroughDecorator: false);
        var afterEscapingFailure = await Run(escapesThroughDecorator: true);
        // Both definition operations have unwound. Their difference should
        // cover the still-live keyword table, apart from minor scratch.
        Assert.True(afterEscapingFailure - afterPrivateFailure >= keywordCount * IntPtr.Size,
            $"Private failure held {afterPrivateFailure} bytes, escaping failure held {afterEscapingFailure} bytes.");

        async Task<long> Run(bool escapesThroughDecorator)
        {
            var declaration = escapesThroughDecorator
                ? $"    @capture\n    def target(first=shared, /, *, {parameters}): return first"
                : $"    def target(first=shared, /, *, {parameters}) -> fail(): return first";
            var source = """
                import time
                shared = []
                escaped = []
                def fail():
                    time.time()
                    raise ValueError('stop')
                def capture(function):
                    escaped.append(function)
                    fail()
                try:
                """ + "\n" + declaration + "\n" + """
                except ValueError:
                    pass
                time.time()
                """ + (escapesThroughDecorator
                    ? $"\nreturn len(escaped[0].__kwdefaults__) == {keywordCount} and escaped[0]() is shared"
                    : "\nreturn not escaped");
            using var script = new LythonEngine().Compile(source);
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 64 * 1024);
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(true, result.ReturnValue);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
            return host.AtSecondGate;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedDefaultsRemainFundedAfterMetadataIsCleared(bool callFirst)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = callFirst
            ? "def f(value='x'*4096): return value\nsaved=f()\nf.__defaults__=None\nreturn saved"
            : "def f(value='x'*4096): return value\nsaved=f.__defaults__\nf.__defaults__=None\nreturn saved[0]";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
