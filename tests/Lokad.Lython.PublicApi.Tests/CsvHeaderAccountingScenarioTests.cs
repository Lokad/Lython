using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>Canonical headers retain their input storage; inference is lazy.</summary>
public sealed class CsvHeaderAccountingScenarioTests
{
    // Retained input capacity still requires admission, without a duplicate header table.
    private const long ExplicitNamesBudgetBytes = 6291456;

    // Access fieldnames to request the otherwise deferred header parse.
    private const long HeaderColumnsBudgetBytes = 15400000;

    [Fact]
    public async Task ManyExplicitFieldnamesStayCharged()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            r = csv.DictReader(["a,b"], ["k"] * 100000)
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(ExplicitNamesBudgetBytes) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            line = "k," * 100000
            line = line + "k"
            r = csv.DictReader([line])
            return r.fieldnames
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(HeaderColumnsBudgetBytes) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            rows1 = list(csv.DictReader(["a,b", "1,2"], ["x", "y"]))
            rows2 = list(csv.DictReader(["a,b", "1,2"]))
            return [rows1[0]["x"], rows1[0]["y"], rows2[0]["a"], rows2[0]["b"]]
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
