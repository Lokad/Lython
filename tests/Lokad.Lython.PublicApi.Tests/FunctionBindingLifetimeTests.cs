using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionBindingLifetimeTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "events=[]\ndef arg(name,value):\n events.append(name)\n return value\ndef pack(a,/,b=2,*,c=3): return a,b,c\nprint(pack(arg('a',1),c=arg('c',30),b=arg('b',20)),events)\npack.__defaults__=(9,)\npack.__kwdefaults__['c']=8\nprint(pack(7))", "(1, 20, 30) ['a', 'c', 'b']\n(7, 9, 8)\n" },
        { "def pick(a=1,*,b=2): return a,b\ndef replacement(a=8,*,c=9): return a,c\ndef argument():\n global pick\n pick=replacement\n return 5\nsaved=pick\nprint(pick(b=argument()))\nprint(pick(),saved())", "(1, 5)\n(8, 9) (1, 2)\n" },
        { "def target(a=1,*,b=2): return a,b\nsaved=target\ndef replace():\n global target\n target=lambda **kw: sorted(kw.items())\n return 9\nprint(target(b=replace()))\nprint(target(b=3),saved(b=4))", "(1, 9)\n[('b', 3)] (1, 4)\n" },
        { "def make(value):\n def pick(*,x=value): return x\n return pick()\nprint(make(7),make(9))", "7 9\n" },
        { "def pick(a=1,*,b=2): return a,b\ndef invoke(): return pick()\nprint(invoke())\npick.__defaults__=(7,)\npick.__kwdefaults__={'b':8}\nprint(invoke())\npick.__defaults__=None\ntry: invoke()\nexcept TypeError: print('missing')", "(1, 2)\n(7, 8)\nmissing\n" },
        { "def pick(a=1,*,b=2): return a,b\ndef invoke(): return pick(b=8)\nprint(invoke())\ndef other(c=3,*,b=4): return c,b\npick=other\nprint(invoke())", "(1, 8)\n(3, 8)\n" },
        { "class C:\n def take(self,a=1,*,b=2): return a,b\nc=C()\nsaved=c.take\nprint(c.take(b=3),saved(a=4,b=5))\nC.take.__defaults__=(9,)\nC.take.__kwdefaults__={'b':8}\nprint(c.take(),saved())", "(1, 3) (4, 5)\n(9, 8) (9, 8)\n" },
        { "def pick(a=1,*,b=2): return a,b\nfor missing in [False,True,False]:\n pick.__defaults__=None if missing else (7,)\n try: print(pick(b=8))\n except TypeError: print('missing')", "(7, 8)\nmissing\n(7, 8)\n" },
        { "def pick(*,a=1,b=2): return a,b\nfor defaults in [{'a':3,'b':4},{'a':5},{'a':6,'b':7}]:\n pick.__kwdefaults__=defaults\n try: print(pick())\n except TypeError: print('missing')", "(3, 4)\nmissing\n(6, 7)\n" },
        { "class Value:\n def __mul__(self,count): raise MemoryError('guest')\ndef grow(value,*,count=2): return value*count\ntry: grow(value=Value())\nexcept MemoryError as error: print(str(error))\nprint('continued')", "guest\ncontinued\n" },
        { "def pick(a=1,*,b=2): return a,b\ndef duplicate(): return {'b':9}\ntry: print(pick(b=3,**duplicate()))\nexcept TypeError: print('duplicate')\nprint(pick(**{'a':7,'b':8}))", "duplicate\n(7, 8)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TargetsDefaultsAndArgumentEffectsRemainLive(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
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
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(65)]
    public async Task WideDefaultedSignaturesKeepTheirOrdinaryBindingAndRetention(int width)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => $"p{i}={i}"));
        var source = $"def pick({parameters}): return p{width - 1}\npayload=['kept']\nvalue=pick(p{width - 1}=payload)\npayload=None\nreturn value";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal("kept", Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(retained.ReturnValue)));
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var other = await script.RunAsync(new MockLythonHost()))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedInputSurvivesWithdrawalAndJoinedHostWork(bool cancel)
    {
        const string source = """
            def keep(value,*,marker='called'):
                globals().update(payload=None)
                with open('events.txt','a') as target: target.write(marker)
                return value
            payload=['é'*8192]
            return keep(value=payload)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        LythonExecutionResult? result = null;
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(new string('é', 8192), Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue)));
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(cancel ? "before" : "beforecalled", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalInFunctionBodyRemainsTerminalThroughFinallyReturn(bool asynchronous)
    {
        const string source = """
            def grow(value,*,count=8192):
                return value*count
            payload='é'*256
            try: grow(value=payload)
            except MemoryError: print('suppressed')
            finally: return payload
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(2, result.Failure?.Span?.Line);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var healthy = new LythonEngine().Run("print('ok')", new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(healthy.Success, healthy.Failure?.Message);
            Assert.Equal("ok\n", healthy.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
