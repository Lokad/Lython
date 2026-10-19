using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnicodeFormatCharacterTests
{
    [Theory]
    [InlineData("format('😀z','>4')", "  😀z")]
    [InlineData("format('😀z','<4')", "😀z  ")]
    [InlineData("format('😀z','^5')", " 😀z  ")]
    [InlineData("format('😀z','.1s')", "😀")]
    [InlineData("format('😀z','>4.1s')", "   😀")]
    [InlineData("format('😀z','.0s')", "")]
    [InlineData("format('café','>6')", "  café")]
    [InlineData("format('e\u0301😀','^5')", " e\u0301😀 ")]
    [InlineData("format('x','🧡>3')", "🧡🧡x")]
    [InlineData("format('x','🧡<3')", "x🧡🧡")]
    [InlineData("format('x','🧡^4')", "🧡x🧡🧡")]
    [InlineData("format('x',chr(0)+'>3')", "\u0000\u0000x")]
    [InlineData("format(12,'🧡>4d')", "🧡🧡12")]
    [InlineData("format(-12,'🧡=5d')", "-🧡🧡12")]
    [InlineData("format(3.5,'🧡^6.1f')", "🧡3.5🧡🧡")]
    [InlineData("format(complex(1,2),'🧡>10')", "🧡🧡🧡🧡(1+2j)")]
    [InlineData("format(Decimal('1.5'),'🧡>5')", "🧡🧡1.5")]
    [InlineData("format(True,'🧡>3')", "🧡🧡1")]
    [InlineData("'{0:🧡>3}'.format('x')", "🧡🧡x")]
    [InlineData("'{item:>4.1s}'.format_map({'item':'😀z'})", "   😀")]
    [InlineData("f\"{'😀z':>4.1s}\"", "   😀")]
    [InlineData("f'{value!r:>12}'", "    'café 😀'")]
    [InlineData("format('😀','0.0s')", "")]
    public async Task WidthPrecisionAndFillUsePythonCharacters(string expression, string expected)
    {
        var source = (expression.Contains("Decimal(", StringComparison.Ordinal) ? "from decimal import Decimal\n" : "")
            + "value='café 😀'\nreturn " + expression;
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("format('x','ab>3')", "ValueError")]
    [InlineData("format('x','🧡x>4')", "ValueError")]
    [InlineData("format(1j,'0>8')", "ValueError")]
    [InlineData("format(None,'🧡>3')", "TypeError")]
    public async Task InvalidSpecificationsKeepTheirPythonError(string expression, string expected)
    {
        using var script = new LythonEngine().Compile("return " + expression);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal(expected, result.Failure?.ExceptionType);
        }
    }

    [Fact]
    public async Task SupplementaryFillRefusesExpansionBeforePublication()
    {
        using var script = new LythonEngine().Compile("value=format('x','🧡>1000000')\nwith open('/events.txt','a') as output:output.write('after')");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost(); host.SeedFile("/events.txt", "before");
            using var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) })
                : script.Run(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) });
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.DeniedReservationBytes > 3 * 1024 * 1024);
            Assert.Equal("before", host.ReadText("/events.txt"));
        }
    }
}
