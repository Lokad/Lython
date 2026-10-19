using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormattedStringExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "events=[]\ndef mark(name,value):\n events.append(name)\n return value\nclass Value:\n def __str__(self):\n  events.append('convert')\n  return 'é'\nprint(f\"a{mark('value',Value())!s:>{mark('width',3)}}b\",events)", "a  éb ['value', 'convert', 'width']\n" },
        { "events=[]\nclass Value:\n def __format__(self,spec):\n  events.append('format:'+spec)\n  return '😀'\ndef spec():\n events.append('specifier')\n return 3\nprint(f'{Value():>{spec()}}',events)", "😀 ['specifier', 'format:>3']\n" },
        { "def outer(value):\n def render(width):\n  local=value+1\n  text=f'{local:0{width}d}'\n  return text,locals()['local']\n return render\nprint(outer(6)(3))", "('007', 7)\n" },
        { "target='before'\nevents=[]\ndef suffix():\n events.append('suffix')\n return 1\ntry: target=f'{1:bad}{suffix()}'\nexcept ValueError: print(target,events)", "before []\n" },
        { "value='é😀'\nprint(f'{value!s}|{value!r}|{value!a}')\nprint(f'{7=}',f'{f\"{3:02d}\"}')", "é😀|'é😀'|'\\xe9\\U0001f600'\n7=7 03\n" },
        { "def render():\n text=f'L{(yield 1)!s:>{(yield 2)}}R'\n yield text\ng=render()\nprint(next(g),g.send('x'),g.send(3))", "1 2 L  xR\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FieldsPreservePythonEffectsAndScopes(string source, string expected)
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
        using var script = new LythonEngine().Compile(source, budget);
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

    [Fact]
    public async Task EscapingFormattedTextRemainsFundedAcrossRuns()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("return f'{value:>8192}'", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = "é" } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(new string(' ', 8191) + "é", second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task DeniedFormattingFailsBeforePublishingOrCatchOutput()
    {
        const string source = "target='before'\ntry: target=f'{1:200000}'\nexcept MemoryError: print(target)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
