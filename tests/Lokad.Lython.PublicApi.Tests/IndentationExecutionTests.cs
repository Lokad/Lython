using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IndentationExecutionTests
{
    public static IEnumerable<object[]> ValidCases()
    {
        yield return ["mixed-prefix-nesting", "if True:\n       \t\tif True:\n\t         print('inner')\n       \t\tprint('outer')\n", "inner\nouter\n"];
        yield return ["uniform-tabs", "if True:\n\tif True:\n\t\tprint(3)\n", "3\n"];
        yield return ["consistent-mixed-prefix", "if True:\n \tvalue=1\n \tprint(value)\n", "1\n"];
        yield return ["form-feed-reset", "if True:\n\tvalue=1\n        \f\tvalue=2\n\tprint(value)\n", "2\n"];
        yield return ["final-comment", "if True:\n  print('kept')\n      # only trailing comment", "kept\n"];
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public async Task PythonColumnRulesPreserveValuesAndBatchOwnership(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
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
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> InvalidCases()
    {
        yield return ["ambiguous-two-spaces", "if True:\n  value=1\n\tprint('bad')\n", "tabs and spaces", 20, 3, 2];
        yield return ["ambiguous-eight-spaces", "if True:\n        value=1\n\tprint('bad')\n", "tabs and spaces", 26, 3, 2];
        yield return ["ambiguous-growth", "if True:\n    if True:\n\tprint('bad')\n", "tabs and spaces", 23, 3, 2];
        yield return ["unmatched-dedent", "if True:\n    value=1\n  print('bad')\n", "outer indentation level", 23, 3, 3];
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void AmbiguousOrUnmatchedIndentationFailsBeforeExecution(string name, string source, string message, int position, int line, int column)
    {
        _ = name;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.False(script.IsValid);
            var diagnostic = Assert.Single(script.Diagnostics);
            Assert.Equal("LA0001", diagnostic.Code);
            Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(new LythonSourceSpan(position, 1, line, column), diagnostic.Span);
            using var result = script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
