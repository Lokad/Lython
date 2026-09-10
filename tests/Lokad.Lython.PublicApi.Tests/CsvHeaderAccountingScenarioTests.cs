using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: header tables are retained for the run. Explicit field-name lists
/// drain geometrically beside the owned array, and header rows commit their
/// array at the slot rate; names themselves stay aliased to existing owners.
/// </summary>
public sealed class CsvHeaderAccountingScenarioTests
{
    // 100k explicit names: the input backing owns ~1.6MB, so it fits 6MB
    // pre-fix; the 2MB drain transient plus the 1.6MB array trip post-fix.
    private const long ExplicitNamesBudgetBytes = 6291456;

    // 100k header columns: ~200KB line plus ~12.9MB field payloads and ~1.6MB
    // row backing fit 15.4MB pre-fix; the 1.6MB header array trips post-fix.
    private const long HeaderColumnsBudgetBytes = 15400000;

    [Fact]
    public async Task ManyExplicitFieldnamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.DictReader(["a,b"], ["k"] * 100000)
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ExplicitNamesBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ExplicitNamesBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ExplicitNamesBudgetBytes);
    }

    [Fact]
    public async Task ManyHeaderColumnsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import csv
            line = "k," * 100000
            line = line + "k"
            r = csv.DictReader([line])
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HeaderColumnsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= HeaderColumnsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HeaderColumnsBudgetBytes);
    }

    [Fact]
    public async Task SmallHeaderShapesBehave()
    {
        var script = new LythonEngine().Compile("""
            import csv
            r1 = csv.DictReader(["a,b", "1,2"], ["x", "y"])
            r2 = csv.DictReader(["a,b", "1,2"])
            return [r1[0]["x"], r1[0]["y"], r2[0]["a"], r2[0]["b"]]
            """);
        Assert.True(script.IsValid);
        // Explicit names keep the first row as data, matching CPython.
        var expected = new List<object?> { "a", "b", "1", "2" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}