using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class StringStripCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "print(repr('  café😀 \\t'.strip()),repr(' \\t\\n'.strip()),repr(''.strip()))", "'café😀' '' ''\n" },
        { "text='café😀'\nprint(text.strip() is text,text.strip('') is text)", "True True\n" },
        { "text='\\x1c\\x1f\\u00a0\\u2003café😀\\u2003\\u00a0\\x1f\\x1c'\nprint(repr(text.strip()))", "'café😀'\n" },
        { "print(repr('\\u200bword\\u200b'.strip()))", "'\\u200bword\\u200b'\n" },
        { "text=r' \\ud800 '\nprint(text.strip()==r'\\ud800')", "True\n" },
        { "text='  café  '\nprint(text.strip(None),text.strip(' c'),text.strip(*(None,)))", "café afé café\n" },
        { "text=' x '\ndef argument():\n    print('argument')\n    return ' '\ntry: text.strip(chars=argument())\nexcept TypeError: print('keyword')\nprint(repr(text))", "argument\nkeyword\n' x '\n" },
        { "text=' x '\nmethod=text.strip\nfor target in [method,str.strip.__get__(text,str)]:\n    try: target(**{'chars':' '})\n    except TypeError: print('keyword')\ntry: str.strip(text,chars=' ')\nexcept TypeError: print('keyword')\nprint(method(),str.strip(text))", "keyword\nkeyword\nkeyword\nx x\n" },
        { "try: ' x '.strip(1)\nexcept TypeError: print('type')\ntry: ' x '.strip(' ','x')\nexcept TypeError: print('extra')\ntry: str.strip()\nexcept TypeError: print('receiver')", "type\nextra\nreceiver\n" },
        { "text=' x '\nprint(text.strip is text.strip,text.strip.__self__ is text)\nmethod=text.strip\ntext=' y '\nprint(method(),text.strip())", "False True\nx y\n" },
        { "class C:\n    def __getattribute__(self,name):\n        print('lookup',name)\n        return lambda: 'ok'\nprint(C().strip())", "lookup strip\nok\n" },
        { "class C: strip='not-callable'\ntry: C().strip()\nexcept TypeError: print('not-callable')", "not-callable\n" },
        { "def f(): return 'ok'\nclass C: pass\nc=C()\nc.strip=f\nprint(c.strip())", "ok\n" },
        { "class C:\n    def strip(self,chars=None): return chars\nprint(C().strip(chars='x'))", "x\n" },
        { "text=' x '\nprint([text.strip() for i in range(2)])\ndef produce():\n    yield text.strip()\nprint(list(produce()))", "['x', 'x']\n['x']\n" },
        { "class C:\n    @property\n    def strip(self):\n        print('property')\n        return lambda: 'ok'\nprint(C().strip())", "property\nok\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task StripPreservesWhitespaceBindingIdentityAndFallback(string source, string expected)
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

    private const string YieldingGetter = "class C:\n    @property\n    def strip(self):\n        with open('events.txt','a') as target: target.write('lookup\\n')\n        return lambda: 'ok'\nprint(C().strip())";

    [Fact]
    public async Task DynamicGetterCanSuspendBeforeInvocation()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(YieldingGetter, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok\n", result.StandardOutput);
            Assert.Equal("lookup\n", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancelledGetterDrainsBeforeAllowanceReturns()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(YieldingGetter, budget);
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

    [Fact]
    public async Task RetainedTrimmedStorageRemainsFunded()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("text=' '*1024+'x'*4096+' '*1024\nvalue=text.strip()\ntext=None\nreturn value", budget);
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
