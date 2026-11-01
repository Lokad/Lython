using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ArithmeticPrecedenceTests
{
    [Theory]
    [InlineData("|", "LA1061", "Expected expression after bitwise '|'.")]
    [InlineData("^", "LA1062", "Expected expression after bitwise '^'.")]
    [InlineData("&", "LA1063", "Expected expression after bitwise '&'.")]
    [InlineData("<<", "LA1064", "Expected expression after shift operator.")]
    [InlineData("+", "LA1041", "Expected expression after additive operator.")]
    [InlineData("*", "LA1042", "Expected expression after multiplicative operator.")]
    public void MissingOperandRetainsItsDiagnostic(string token, string code, string message)
    {
        using var script = new LythonEngine().Compile("value = 1 " + token);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Message == message);
    }

    [Fact]
    public async Task ProtocolsFollowTheParsedTreeAndEvaluateOperandsOnceInOrder()
    {
        const string source = """
            events = []
            class Expr:
                def __init__(self, text): self.text = text
                def __matmul__(self, other): return Expr('(' + self.text + '@' + other.text + ')')
                def __mul__(self, other): return Expr('(' + self.text + '*' + other.text + ')')
                def __add__(self, other): return Expr('(' + self.text + '+' + other.text + ')')
                def __lshift__(self, other): return Expr('(' + self.text + '<<' + other.text + ')')
            def mark(text):
                events.append(text)
                return Expr(text)
            value = mark('a') @ mark('b') * mark('c') + mark('d') << mark('e')
            print(' '.join(events))
            print(value.text)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("a b c d e\n((((a@b)*c)+d)<<e)\n", result.StandardOutput);
        }
    }

    // Goldens from isolated CPython 3.13, independent of Lython's parser.
    [Theory]
    [InlineData("5 | 2 ^ 7 & 3", "5")]
    [InlineData("4 << 2 + 1", "32")]
    [InlineData("128 >> 1 + 2", "16")]
    [InlineData("20 - 8 - 3", "9")]
    [InlineData("24 // 4 * 2 % 5", "2")]
    [InlineData("3 + 4 * 2 << 1 & 31 ^ 1 | 32", "55")]
    [InlineData("2 ** 3 ** 2", "512")]
    [InlineData("-2 ** 2 * 3 + 5", "-7")]
    [InlineData("2 ** -2 * 8", "2.0")]
    [InlineData("not 1 | 2 == 3 and 7 or 9", "9")]
    [InlineData("1 + 2 < 4 << 1 < 3 * 4", "True")]
    [InlineData("8 - 3 + 2", "7")]
    [InlineData("20 / 4 / 2", "2.5")]
    [InlineData("~1 & 7 ^ 3 | 8", "13")]
    [InlineData("1 << 3 << 1", "16")]
    [InlineData("64 >> 2 >> 1", "8")]
    public async Task MixedPrecedenceAndAssociativityMatchPython(string expression, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("print(" + expression + ")", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected + "\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
