using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG02/MG11: writer objects own their shell beside governed history and
/// field names; row payloads stay owned by the writer-history accounting.
/// </summary>
public sealed class CsvWriterShellAccountingScenarioTests
{
    // 20k retained writers own 128B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix. 20k dictionary writers add a 64B shell.
    private const long WriterBudgetBytes = 1572864;
    // Field-name arrays from the header slice already own ~48B each; the
    // shells add ~192B on top, so they fit 2.5MB pre-fix and trip post-fix.
    private const long DictWriterBudgetBytes = 2500000;

    [Fact]
    public async Task ManyRetainedWritersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import csv
            objs = []
            i = 0
            while i < 20000:
                objs.append(csv.writer())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = WriterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= WriterBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= WriterBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedDictWritersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import csv
            f = open("/w.csv", "w")
            fn = ["a"]
            objs = []
            i = 0
            while i < 20000:
                objs.append(csv.DictWriter(f, fn))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DictWriterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task WritersBehave()
    {
        var script = new LythonEngine().Compile("""
            import csv
            w = csv.writer()
            w.writerow(["a", "b"])
            f = open("/w.csv", "w")
            dw = csv.DictWriter(f, ["a", "b"])
            dw.writeheader()
            dw.writerow({"a": 1, "b": 2})
            return w.getvalue()
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a,b", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a,b", asyncResult.ReturnValue);
    }
}