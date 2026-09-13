using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG02: file-backed writers stream output without retaining history, while
/// in-memory history pays for its rows, arrays and converted values.
/// </summary>
public sealed class CsvWriterAccountingScenarioTests
{
    [Fact]
    public async Task FileBackedWriterDropsRowHistory()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            i = 0
            while i < 2000:
                writer.writerow([i, "x"])
                i = i + 1
            handle.close()
            return writer.getvalue()
            """);
        Assert.True(script.IsValid);
        // An empty getvalue proves Rows stayed empty through all writes: a
        // file-backed writer streams output instead of retaining history.
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);

        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("", Assert.IsType<string>(sync.ReturnValue));
        Assert.Equal(2001, syncHost.ReadText("/out.csv").Split('\n').Length);
        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task InMemoryHistoryIsCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            writer = csv.writer()
            i = 0
            while i < 2000:
                writer.writerow([i, "y" * 50])
                i = i + 1
            return 1
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task QuotedFieldsRenderExactly()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            writer = csv.writer()
            writer.writerow(["a,b", "line1\nline2", "x\"y", "plain"])
            return writer.getvalue()
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("\"a,b\",\"line1\nline2\",\"x\"\"y\",plain", Assert.IsType<string>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("\"a,b\",\"line1\nline2\",\"x\"\"y\",plain", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FailedHostWriteSurfacesExplicitly()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            writer.writerow(["a", "b"])
            handle.close()
            return 1
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.FailNextWriteText("/out.csv", "disk is full");
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.NotNull(sync.Failure);

        var asyncHost = new MockLythonHost();
        asyncHost.FailNextWriteText("/out.csv", "disk is full");
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.False(asyncResult.Success);
        Assert.NotNull(asyncResult.Failure);
    }


    [Fact]
    public async Task HugeFieldRenderIsBounded()
    {
        // MG02: a single oversized field meets its render reservation before
        // the buffer is built, instead of materializing uncharged.
        var script = new LythonEngine().Compile(
            """
            import csv
            writer = csv.writer()
            writer.writerow(["x" * 200000])
            return 1
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 300000 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }


    [Fact]
    public async Task WriterowsPreservesRowsWrittenBeforeConversionError()
    {
        // MG02: writerows is not transactional: rows streamed before a
        // mid-operation conversion failure stay streamed (file-backed) and
        // retained (in-memory) instead of rolling back.
        var script = new LythonEngine().Compile("""
            import csv
            f = open("/out.csv", "w")
            w = csv.writer(f)
            m = csv.writer()
            try:
                w.writerows([[1], [[2]], [3]])
            except TypeError:
                pass
            try:
                m.writerows([[1], [[2]], [3]])
            except TypeError:
                pass
            f.close()
            return m.getvalue()
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("1\n", syncHost.ReadText("/out.csv"));
        Assert.Equal("1", Assert.IsType<string>(sync.ReturnValue));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("1\n", asyncHost.ReadText("/out.csv"));
        Assert.Equal("1", Assert.IsType<string>(asyncResult.ReturnValue));
    }
}
