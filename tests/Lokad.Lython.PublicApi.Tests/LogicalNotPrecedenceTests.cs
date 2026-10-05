using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LogicalNotPrecedenceTests
{
    [Theory]
    [InlineData("not 1 == 2", "True")]
    [InlineData("not 1 in [2]", "True")]
    [InlineData("not 1 + 1", "False")]
    [InlineData("not 1 < 2 < 3", "False")]
    [InlineData("not not 2", "True")]
    [InlineData("not 0 and 7 or 9", "7")]
    [InlineData("(not 1) + 1", "1")]
    [InlineData("-2 ** 2", "-4")]
    [InlineData("2 ** -2", "0.25")]
    [InlineData("'yes' if not 1 == 2 else 'no'", "yes")]
    public async Task ExpressionsFollowPythonPrecedence(string expression, string expected)
    {
        var compiled = new LythonEngine().Compile($"print({expression})");
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected + "\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("1 + not 2")]
    [InlineData("-not 2")]
    [InlineData("2 ** not 2")]
    public void LogicalNotCannotBeAnArithmeticOperand(string expression)
        => Assert.False(new LythonEngine().Compile("print(" + expression + ")").IsValid);
}
