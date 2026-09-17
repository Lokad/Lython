using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: decimal construction shells own lifetime like the other factory results do.
public sealed class DecimalLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task DecimalCtorDiscardCompletes()
        => await AssertCompletes(
            "import decimal\nfor i in range(50000):\n    x = decimal.Decimal('1.5')\nreturn 0\n", "0");

    [Fact]
    public async Task DecimalArithDiscardCompletes()
        => await AssertCompletes(
            "import decimal\na = decimal.Decimal('1.5')\nb = decimal.Decimal('2.5')\nfor i in range(50000):\n    x = a + b\nreturn 0\n", "0");

    [Fact]
    public async Task DecimalBehaves()
        => await AssertCompletes(
            "import decimal\nreturn str(decimal.Decimal('1.5') + decimal.Decimal('2.5'))\n", "4.0");

    [Fact]
    public async Task RetainedDecimalDenied()
    {
        var script = new LythonEngine().Compile(
            "import decimal\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(decimal.Decimal('1.5'))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
