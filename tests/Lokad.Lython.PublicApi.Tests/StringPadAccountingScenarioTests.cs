using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG07: padding expansions meet the budget before their buffers are built,
/// instead of materializing first and charging after.
/// </summary>
public sealed class StringPadAccountingScenarioTests
{
    [Fact]
    public async Task HugeZfillWidthIsBounded()
    {
        var script = new LythonEngine().Compile(
            """
            return "1".zfill(2000000)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
    [Fact]
    public async Task HugeFormatWidthIsBounded()
    {
        // MG07 probe: interpolated padding built multi-megabyte CLR strings
        // before the governed result charge observed them.
        var script = new LythonEngine().Compile(
            """
            return f"{'x':>2000000}"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FormatPaddingPeakStaysBounded()
    {
        // MG07: the padding preflight must fail before any padding buffer is
        // built, not after materializing it. Both runs report MemoryError;
        // only the governed path avoids the multi-megabyte peak. Allocation
        // is measured on the synchronous run only: awaits may resume on a
        // different thread, which would invalidate the per-thread counter.
        var script = new LythonEngine().Compile(
            """
            try:
                x = f"{'x':>2000000}"
            except MemoryError:
                return "memory"
            return "unexpected"
            """);
        Assert.True(script.IsValid);
        var warm = new LythonEngine().Compile("return f\"{'x':>10}\"");
        Assert.True(warm.IsValid);
        var warmOptions = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        Assert.True(warm.Run(new MockLythonHost(), warmOptions).Success);
        Assert.True((await warm.RunAsync(new MockLythonHost(), warmOptions)).Success);

        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var beforeSync = GC.GetAllocatedBytesForCurrentThread();
        var sync = script.Run(new MockLythonHost(), options);
        var syncAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSync;
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("memory", Assert.IsType<string>(sync.ReturnValue));
        Assert.True(syncAllocated < 1048576, $"format padding allocated {syncAllocated} bytes before failing");

        // The asynchronous path shares the padding implementation; only the
        // outcome is asserted here.
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("memory", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FormatPaddingContractsStayExact()
    {
        // MG07: preflighting padding must not change a byte of formatted
        // output, including non-ASCII fills and the format() builtin path.
        var script = new LythonEngine().Compile(
            """
            a = f"{'x':>5}"
            b = f"{1:03d}"
            c = f"{'ab':^5}"
            d = f"{'ab':é>4}"
            e = format(7, "04d")
            return [a, b, c, d, e]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "    x", "001", " ab  ", "ééab", "0007" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task HugeFloatPrecisionIsBounded()
    {
        // MG07 probe: float precision scales the output without bound from a
        // tiny input, materializing it before the governed result charge.
        var script = new LythonEngine().Compile(
            """
            return f"{1.5:.2000000f}"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FloatPrecisionPeakStaysBounded()
    {
        // MG07: the precision preflight must fail before BCL formatting
        // materializes tens of megabytes. Both runs report MemoryError; only
        // the governed path avoids the peak. Allocation is measured on the
        // synchronous run only (awaits invalidate the per-thread counter).
        var script = new LythonEngine().Compile(
            """
            try:
                x = f"{1.5:.20000000f}"
            except MemoryError:
                return "memory"
            return "unexpected"
            """);
        Assert.True(script.IsValid);
        var warm = new LythonEngine().Compile("return f\"{1.5:.2f}\"");
        Assert.True(warm.IsValid);
        var warmOptions = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        Assert.True(warm.Run(new MockLythonHost(), warmOptions).Success);
        Assert.True((await warm.RunAsync(new MockLythonHost(), warmOptions)).Success);

        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var beforeSync = GC.GetAllocatedBytesForCurrentThread();
        var sync = script.Run(new MockLythonHost(), options);
        var syncAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSync;
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("memory", Assert.IsType<string>(sync.ReturnValue));
        Assert.True(syncAllocated < 1048576, $"float precision allocated {syncAllocated} bytes before failing");

        // The asynchronous path shares the formatting implementation; only the
        // outcome is asserted here.
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("memory", Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FloatPrecisionContractsStayExact()
    {
        // MG07: preflighting precision must not change formatted output, and
        // small precisions plus non-finite values behave exactly as before.
        var script = new LythonEngine().Compile(
            """
            a = f"{1.5:.2f}"
            b = f"{1.23456789:.3g}"
            c = f"{0.126:.1%}"
            d = f"{123.456:.0f}"
            e = f"{float('inf'):.100000f}"
            return [a, b, c, d, e]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "1.50", "1.23", "12.6%", "123", "Infinity" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
