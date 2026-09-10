using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: open file handles own their shell plus path payload, so retained
/// handles accumulate instead of riding the call budget-free. Buffer content
/// was already governed.
/// </summary>
public sealed class FileHandleAccountingScenarioTests
{
    // 20k retained handles own 64B plus a 16B list slot each, so they fit
    // 1MB pre-fix and trip post-fix. Read handles over an empty file isolate
    // the shell since no text is retained.
    private const long HandleBudgetBytes = 1048576;

    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/r", "");
        return host;
    }

    [Fact]
    public async Task ManyRetainedWritersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append(open("/f.txt", "w"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HandleBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= HandleBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HandleBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedReadersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append(open("/r"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HandleBudgetBytes };
        var sync = script.Run(SeededHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= HandleBudgetBytes);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HandleBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedAppendersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append(open("/f.txt", "a"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HandleBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= HandleBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HandleBudgetBytes);
    }

    [Fact]
    public async Task HandlesBehave()
    {
        var script = new LythonEngine().Compile("""
            f = open("/w.txt", "w")
            f.write("hi")
            f.close()
            g = open("/w.txt")
            text = g.read()
            g.close()
            return text
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("hi", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("hi", asyncResult.ReturnValue);
    }
}
