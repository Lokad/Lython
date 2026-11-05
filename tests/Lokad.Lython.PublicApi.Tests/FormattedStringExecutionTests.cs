using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormattedStringExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "events=[]\ndef mark():\n events.append('called')\n return 1\ntry: print(f'head{mark():>1000000000s}tail')\nexcept ValueError: print(events)\nprint(len(f\"a{'q':>513s}z\"))", "['called']\n515\n" },
        { "value=True\nprint(f\"a{('yes' if value else 'no'):>5}b{f'{7:03d}'}c\")", "a  yesb007c\n" },
        { "events=[]\ndef mark(name,value):\n events.append(name)\n return value\nclass Value:\n def __str__(self):\n  events.append('convert')\n  return 'é'\nprint(f\"a{mark('value',Value())!s:>{mark('width',3)}}b\",events)", "a  éb ['value', 'convert', 'width']\n" },
        { "events=[]\nclass Value:\n def __format__(self,spec):\n  events.append('format:'+spec)\n  return '😀'\ndef spec():\n events.append('specifier')\n return 3\nprint(f'{Value():>{spec()}}',events)", "😀 ['specifier', 'format:>3']\n" },
        { "def outer(value):\n def render(width):\n  local=value+1\n  text=f'{local:0{width}d}'\n  return text,locals()['local']\n return render\nprint(outer(6)(3))", "('007', 7)\n" },
        { "target='before'\nevents=[]\ndef suffix():\n events.append('suffix')\n return 1\ntry: target=f'{1:bad}{suffix()}'\nexcept ValueError: print(target,events)", "before []\n" },
        { "value='é😀'\nprint(f'{value!s}|{value!r}|{value!a}')\nprint(f'{7=}',f'{f\"{3:02d}\"}')", "é😀|'é😀'|'\\xe9\\U0001f600'\n7=7 03\n" },
        { "def render():\n text=f'L{(yield 1)!s:>{(yield 2)}}R'\n yield text\ng=render()\nprint(next(g),g.send('x'),g.send(3))", "1 2 L  xR\n" },
        { "s=''.join(['é','😀'])\nprint(f'{s}' is s,f'{s:}' is s,format(s,'') is s,format(s,'>1s') is s)\nprint(f'head:{-12345:😀=12,d}|{17:*^7d}|{s:😀^7.1s}:tail')", "True True True True\nhead:-😀😀😀😀😀12,345|**17***|😀😀😀é😀😀😀:tail\n" },
        { "print(f'A{complex(1,-2):😀^16.2f}|{1234.5:🟢=12,.2f}Z')", "A😀😀😀1.00-2.00j😀😀😀|🟢🟢🟢🟢1,234.50Z\n" },
        { "text='é😀'\nclass Result:\n value=f'X{17:*^8d}{text:😀^6.1s}Y'\nprint(Result.value)", "X***17***😀😀é😀😀😀Y\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FieldsPreservePythonEffectsAndScopes(string source, string expected)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
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
    [InlineData("/conversion.txt")]
    [InlineData("/specifier.txt")]
    [InlineData("/format.txt")]
    public async Task ProtocolsSuspendOnceAndKeepPendingStorageFunded(string? pausedPath)
    {
        const string source = """
class Value:
    def __str__(self):
        with open('conversion.txt','a') as target: target.write('convert')
        return 'x'
    def __format__(self,spec):
        with open('format.txt','a') as target: target.write(spec)
        return 'z'
def width():
    with open('specifier.txt','a') as target: target.write('width')
    return 3
print(f'A{Value()!s:>{width()}}{Value():yes}B')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in new[] { "/conversion.txt", "/specifier.txt", "/format.txt" }) host.SeedFile(path, "before");
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
                Assert.Equal("A  xzB\n", result.StandardOutput);
            }
            else
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(pausedPath == "/conversion.txt" ? "before" : "beforeconvert", host.ReadText("/conversion.txt"));
            Assert.Equal(pausedPath is "/conversion.txt" or "/specifier.txt" ? "before" : "beforewidth", host.ReadText("/specifier.txt"));
            Assert.Equal(pausedPath is null ? "beforeyes" : "before", host.ReadText("/format.txt"));
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
    [InlineData(false), InlineData(true)]
    public async Task EscapingFormattedTextRemainsFundedAcrossRuns(bool multipart)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            multipart ? "return f'prefix:{value:>8192}:suffix'" : "return f'{value:>8192}'", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = "é" } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        var formatted = new string(' ', 8191) + "é";
        Assert.Equal(multipart ? "prefix:" + formatted + ":suffix" : formatted, second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task ClosingSuspendedFieldRunsCleanupAndReturnsAllowance(bool throwInto)
    {
        var source = """
events=[]
def render():
    try:
        value=f"{'x':>32768}{(yield 'ready')}!"
        yield value
    finally:
        events.append('closed')
g=render()
print(next(g))
""" + (throwInto ? "\ntry: g.throw(ValueError('stop'))\nexcept ValueError: pass\n" : "\ng.close()\n")
            + "print(events)\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ready\n['closed']\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task DeniedFormattingFailsBeforePublishingOrCatchOutput(bool multipart)
    {
        var source = multipart
            ? "target='before'\ntry: target=f'prefix:{1:200000}:suffix'\nexcept MemoryError: print(target)"
            : "target='before'\ntry: target=f'{1:200000}'\nexcept MemoryError: print(target)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Empty(result.StandardOutput);
                // The rejected first allocation need not produce an accepted charge.
                // Previously the catch's print supplied that charge after refusal.
                Assert.InRange(result.PeakExecutionMemoryBytes, 0, 131072);
                Assert.True(result.DeniedReservationBytes > 131072);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
