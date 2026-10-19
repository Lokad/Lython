using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AsciiSigmaCompatibilityTests
{
    [Fact]
    public async Task EveryAsciiScalarPreservesPythonFinalSigmaContext()
    {
        const string source = """
            for point in range(128):
                c = chr(point)
                print(('A' + c + 'Σ').lower()[-1] +
                      (c + 'Σ').lower()[-1] +
                      ('AΣ' + c + 'A').lower()[1] +
                      ('AΣ' + c).lower()[1])
            """;
        // Independent CPython 3.13.2 output: every ASCII scalar before and after sigma,
        // with and without adjacent cased text. Includes controls and punctuation.
        const string expected = "σσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nςσσς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nςσσς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nςσσς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nσσςς\nσσςς\nσσςς\nςσσς\nσσςς\nςσσς\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nςςσσ\nσσςς\nσσςς\nσσςς\nσσςς\nσσςς\n";
        var budget = new LythonMemoryBudget(1048576);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var compiledBaseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(compiledBaseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(compiledBaseline, budget.ReservedBytes);
    }
}
