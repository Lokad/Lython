using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: a quoted field spanning many physical lines accumulates in the parser
/// field builder across feeds, so per-line bounds cannot see it. The builder
/// peak rides a transient reservation; only the finished payload commits.
/// The line source repeats one shared small string, so the input itself stays
/// far below the budget and only the builder peak can trip it.
/// </summary>
public sealed class CsvMultilineFieldScenarioTests
{
    // Final payload is ~1.6MB (16k x 100-char lines plus newlines), so it fits
    // comfortably; the builder transient (~2 bytes x doubled capacity) is ~4MB.
    private const long MultilineFieldBudgetBytes = 2097152;

    private const string MultilineFieldScript = """
        import csv
        import itertools
        q = chr(34)
        pad = "x" * 100
        lines = itertools.chain([q + "a"], itertools.repeat(pad, 16000), ["b" + q])
        rows = list(csv.reader(lines))
        return len(rows[0][0])
        """;

    [Fact]
    public async Task HugeMultilineFieldTripsBuilderBudget()
    {
        var script = new LythonEngine().Compile(MultilineFieldScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MultilineFieldBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SmallMultilineFieldParsesExactly()
    {
        var script = new LythonEngine().Compile("""
            import csv
            import itertools
            q = chr(34)
            pad = "x" * 5
            lines = itertools.chain([q + "a"], itertools.repeat(pad, 3), ["b" + q])
            rows = list(csv.reader(lines))
            return rows
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { "a\nxxxxx\nxxxxx\nxxxxx\nb" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}