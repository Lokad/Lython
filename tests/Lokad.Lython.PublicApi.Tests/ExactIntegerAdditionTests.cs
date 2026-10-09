using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExactIntegerAdditionTests
{
    [Theory]
    [InlineData("return str(18446744073709551615 + 1)", "18446744073709551616")]
    [InlineData("x = 18446744073709551615\nx += 1\nreturn str(x)", "18446744073709551616")]
    [InlineData("return str(-18446744073709551616 + -1)", "-18446744073709551617")]
    [InlineData("x = -18446744073709551616\nx += -1\nreturn str(x)", "-18446744073709551617")]
    [InlineData("x = 1 << 256\nreturn str(x + -x)", "0")]
    [InlineData("x = 1 << 256\nx += -x\nreturn str(x)", "0")]
    [InlineData("x = 1 << 256\nx += x\nreturn str(x.bit_length())", "258")]
    [InlineData("return str(True + True)", "2")]
    [InlineData("x = True\nx += True\nreturn str(x)", "2")]
    [InlineData("return str(1 + 0.5)", "1.5")]
    [InlineData("x = 1\nx += 0.5\nreturn str(x)", "1.5")]
    public async Task ExactnessPromotionAndMixedFallbacks(string source, string expected)
        => await AssertResult(source, expected);

    private static async Task AssertResult(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        var asyncResult = await script.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }

    [Theory]
    [InlineData("class Right:\n def __radd__(self, other): return 'reflected'\nreturn 1 + Right()", "reflected")]
    [InlineData("class Left:\n def __add__(self, other): return 'custom'\nreturn Left() + 1", "custom")]
    [InlineData("class Left:\n def __iadd__(self, other): return 'in-place'\nx = Left()\nx += 1\nreturn x", "in-place")]
    public async Task UserMethodsRemainInGeneralDispatch(string source, string expected)
    {
        await AssertResult(source, expected);
    }

    [Theory]
    [InlineData("value = value + i")]
    [InlineData("value += i")]
    public async Task RetainedHeapAdditionResultsKeepTheirCharges(string addition)
    {
        var script = new LythonEngine().Compile($"""
            seed = 1 << 10000
            values = []
            for i in range(20000):
                value = seed
                {addition}
                values.append(value)
            return len(values)
            """);
        Assert.True(script.IsValid);
        const long budget = 65536;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        var sync = script.Run(new MockLythonHost(), options);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.PeakExecutionMemoryBytes <= budget);
        }
    }
}
