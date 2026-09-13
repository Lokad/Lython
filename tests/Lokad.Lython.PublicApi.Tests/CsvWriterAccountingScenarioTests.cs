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
    public async Task FileBackedWriterowsTripSmallBudget()
    {
        // MG21: file-backed output buffers through governed byte builders, so
        // a large buffered document trips a small budget instead of growing
        // uncharged behind the streaming writes.
        var script = new LythonEngine().Compile("""
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            pad = "y" * 50
            i = 0
            while i < 2000:
                writer.writerow([i, pad])
                i = i + 1
            handle.close()
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
    public async Task FundedFileBackedWriterowsStreamsFully()
    {
        var script = new LythonEngine().Compile("""
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            pad = "y" * 50
            i = 0
            while i < 2000:
                writer.writerow([i, pad])
                i = i + 1
            handle.close()
            return 1
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(2001, syncHost.ReadText("/out.csv").Split('\n').Length);

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(2001, asyncHost.ReadText("/out.csv").Split('\n').Length);
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
    public async Task WriterowStaysUsableAfterConversionError()
    {
        // MG02: a conversion failure happens during row conversion, before
        // any history or output effect, so both writers stay usable and the
        // failed row leaves nothing behind.
        var script = new LythonEngine().Compile("""
            import csv
            f = open("/out.csv", "w")
            w = csv.writer(f)
            m = csv.writer()
            try:
                w.writerow([[1]])
            except TypeError:
                pass
            try:
                m.writerow([[1]])
            except TypeError:
                pass
            w.writerow(["a"])
            m.writerow(["a"])
            f.close()
            return m.getvalue()
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a\n", syncHost.ReadText("/out.csv"));
        Assert.Equal("a", Assert.IsType<string>(sync.ReturnValue));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a\n", asyncHost.ReadText("/out.csv"));
        Assert.Equal("a", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task WriterStaysUsableAfterOversizedConversionFailure()
    {
        // MG02: an oversized value trips the budget while converting the
        // guarded row (before unbounded retained growth), so the failed row
        // retains nothing and the writer stays usable for later rows.
        var script = new LythonEngine().Compile("""
            import csv
            w = csv.writer()
            try:
                w.writerow([int("9" * 100000)])
            except MemoryError:
                pass
            w.writerow(["a"])
            return w.getvalue()
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 60000 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", Assert.IsType<string>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task CaughtOversizedRowKeepsWriterReusable()
    {
        // MG02: a row whose render transient trips the budget leaves nothing
        // behind on a file-backed writer (which retains no history), and the
        // writer stays usable afterwards since the failure committed nothing
        // extra. (On in-memory writers the tripped row stays retained like
        // any prior write; the white-box pin covers those counters.)
        var script = new LythonEngine().Compile("""
            import csv
            handle = open("/out.csv", "w")
            w = csv.writer(handle)
            big = "x" * 200000
            try:
                w.writerow([big])
            except MemoryError:
                pass
            w.writerow(["a"])
            w.writerow(["b"])
            handle.close()
            return 1
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 300000 };
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a\nb\n", syncHost.ReadText("/out.csv"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a\nb\n", asyncHost.ReadText("/out.csv"));
    }

    [Fact]
    public async Task FailedHostWriteLeavesNoPartialRow()
    {
        // MG02: a failing host write surfaces explicitly without partial row
        // bytes, and a later run on the same host (one-shot failure consumed)
        // writes normally.
        var script = new LythonEngine().Compile("""
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            writer.writerow(["a", "b"])
            handle.close()
            return 1
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/out.csv", "");
        syncHost.FailNextWriteText("/out.csv", "disk is full");
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.NotNull(sync.Failure);
        Assert.Equal("", syncHost.ReadText("/out.csv"));
        var syncRetry = script.Run(syncHost);
        Assert.True(syncRetry.Success, syncRetry.Failure?.Message);
        Assert.Equal("a,b\n", syncHost.ReadText("/out.csv"));

        var asyncHost = new MockLythonHost();
        asyncHost.SeedFile("/out.csv", "");
        asyncHost.FailNextWriteText("/out.csv", "disk is full");
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.False(asyncResult.Success);
        Assert.NotNull(asyncResult.Failure);
        Assert.Equal("", asyncHost.ReadText("/out.csv"));
        var asyncRetry = await script.RunAsync(asyncHost);
        Assert.True(asyncRetry.Success, asyncRetry.Failure?.Message);
        Assert.Equal("a,b\n", asyncHost.ReadText("/out.csv"));
    }

    [Fact]
    public async Task CancelledWriterowsFailsDeterministically()
    {
        // MG02: cancellation surfaces mid-stream as an explicit failure with
        // a partial file prefix, never as budget-shaped or CLR leakage; a
        // pre-cancelled token fails before any write.
        var script = new LythonEngine().Compile("""
            import csv
            handle = open("/out.csv", "w")
            writer = csv.writer(handle)
            i = 0
            while i < 200000:
                writer.writerow([i])
                i = i + 1
            handle.close()
            return i
            """);
        Assert.True(script.IsValid);
        using var cts = new CancellationTokenSource();
        var host = new MockLythonHost();
        var runTask = Task.Run(() => script.Run(host, new LythonRunOptions { CancellationToken = cts.Token }));
        await Task.Delay(25);
        cts.Cancel();
        var result = await runTask;
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
        // Buffered output publishes only on flush/close, so a run cancelled
        // mid-stream leaves no file behind (nothing partial to roll back).
        Assert.Throws<InvalidOperationException>(() => host.ReadText("/out.csv"));

        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();
        var asyncResult = await script.RunAsync(
            new MockLythonHost(),
            new LythonRunOptions { CancellationToken = preCancelled.Token });
        Assert.False(asyncResult.Success);
        Assert.NotNull(asyncResult.Failure);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message, StringComparison.Ordinal);
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
