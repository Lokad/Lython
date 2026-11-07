using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnicodeIntegerTextTests
{
    // Independently checked with CPython 3.13 through the public probe.
    [Theory]
    [InlineData("print(int('١٢'))", "12\n")]
    [InlineData("print(int('１２'))", "12\n")]
    [InlineData("print(int('𝟙𝟚'))", "12\n")]
    [InlineData("print(int('٠x_f',0))", "15\n")]
    [InlineData("print(int('٠_٠',0))", "0\n")]
    public async Task DecimalDigitTextMatchesPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
