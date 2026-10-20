using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG07: padding expansions meet the budget before their buffers are built,
/// instead of materializing first and charging after.
/// </summary>
[Collection("RuntimeStress")]
public sealed class StringPadAccountingScenarioTests
{
    [Theory]
    [InlineData("ljust", false)]
    [InlineData("ljust", true)]
    [InlineData("rjust", false)]
    [InlineData("rjust", true)]
    [InlineData("center", false)]
    [InlineData("center", true)]
    [InlineData("zfill", false)]
    [InlineData("zfill", true)]
    public async Task LiteralPaddingUsesExecutionOwnerAndReusesAllowance(string method, bool asynchronous)
    {
        var compilation = new LythonMemoryBudget(65536);
        var execution = new LythonMemoryBudget(65536);
        using var script = new LythonEngine().Compile("return 'x'." + method + "(32)", compilation);
        Assert.True(script.IsValid);
        var baseline = compilation.ReservedBytes;
        for (var i = 0; i < 2; i++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = execution })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = execution }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(32, Assert.IsType<string>(result.ReturnValue).Length);
                Assert.True(execution.ReservedBytes > 0);
                Assert.Equal(baseline, compilation.ReservedBytes);
            }
            Assert.Equal(0, execution.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, compilation.ReservedBytes);
    }

    [Fact]
    public async Task HugeZfillWidthIsBounded()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return "1".zfill(2000000)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
    [Fact]
    public async Task HugeFormatWidthIsBounded()
    {
        // MG07 probe: interpolated padding built multi-megabyte CLR strings
        // before the governed result charge observed them.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return f"{'x':>2000000}"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FormatPaddingPeakStaysBounded()
    {
        // MG07: the padding preflight must fail before any padding buffer is
        // built, not after materializing it. Both runs report MemoryError;
        // only the governed path avoids the multi-megabyte peak. Allocation
        // is measured process-wide so dedicated workers are included.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            try:
                x = f"{'x':>2000000}"
            except MemoryError:
                return "memory"
            return "unexpected"
            """);
        Assert.True(script.IsValid);
        using var warm = new LythonEngine(Utf8RegexProvider.Instance).Compile("return f\"{'x':>10}\"");
        Assert.True(warm.IsValid);
        var warmOptions = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16777216) };
        using (var warmed = warm.Run(new MockLythonHost(), warmOptions)) Assert.True(warmed.Success);
        using (var warmed = await warm.RunAsync(new MockLythonHost(), warmOptions)) Assert.True(warmed.Success);

        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var beforeSync = GC.GetTotalAllocatedBytes(true);
        using var sync = script.Run(new MockLythonHost(), options);
        var syncAllocated = GC.GetTotalAllocatedBytes(true) - beforeSync;
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        Assert.True(syncAllocated < 1048576, $"format padding allocated {syncAllocated} bytes before failing");

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);

    }

    [Fact]
    public async Task FormatPaddingContractsStayExact()
    {
        // MG07: preflighting padding must not change a byte of formatted
        // output, including non-ASCII fills and the format() builtin path.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            a = f"{'x':>5}"
            b = f"{1:03d}"
            c = f"{'ab':^5}"
            d = f"{'ab':é>4}"
            e = format(7, "04d")
            return [a, b, c, d, e]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new List<object?> { "    x", "001", " ab  ", "ééab", "0007" };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task HugeFloatPrecisionIsBounded()
    {
        // MG07 probe: float precision scales the output without bound from a
        // tiny input, materializing it before the governed result charge.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return f"{1.5:.2000000f}"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            try:
                x = f"{1.5:.20000000f}"
            except MemoryError:
                return "memory"
            return "unexpected"
            """);
        Assert.True(script.IsValid);
        using var warm = new LythonEngine(Utf8RegexProvider.Instance).Compile("return f\"{1.5:.2f}\"");
        Assert.True(warm.IsValid);
        var warmOptions = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16777216) };
        using (var warmed = warm.Run(new MockLythonHost(), warmOptions)) Assert.True(warmed.Success);
        using (var warmed = await warm.RunAsync(new MockLythonHost(), warmOptions)) Assert.True(warmed.Success);

        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var beforeSync = GC.GetTotalAllocatedBytes(true);
        using var sync = script.Run(new MockLythonHost(), options);
        var syncAllocated = GC.GetTotalAllocatedBytes(true) - beforeSync;
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        Assert.True(syncAllocated < 1048576, $"float precision allocated {syncAllocated} bytes before failing");

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);

    }

    [Fact]
    public async Task DecimalPrecisionPeakStaysBounded()
    {
        // MG07: the Decimal precision preflight must fail before digit runs
        // materialize. Mirrors FloatPrecisionPeakStaysBounded on the exact
        // decimal path (stash round-trip would show tens-of-MB peaks without
        // the preflight, as tens of thousands of padded zeros prove).
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            from decimal import Decimal
            try:
                x = f"{Decimal(1.5):.20000000f}"
            except MemoryError:
                return "memory"
            return "unexpected"
            """);
        Assert.True(script.IsValid);

        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var beforeSync = GC.GetTotalAllocatedBytes(true);
        using var sync = script.Run(new MockLythonHost(), options);
        var syncAllocated = GC.GetTotalAllocatedBytes(true) - beforeSync;
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        Assert.True(syncAllocated < 1048576, $"decimal precision allocated {syncAllocated} bytes before failing");

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);

    }

    [Fact]
    public async Task FloatPrecisionContractsStayExact()
    {
        // MG07: preflighting precision must not change formatted output, and
        // small precisions behave exactly as before. Non-finite values
        // render CPython words ("inf", not BCL "Infinity") at any precision.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            a = f"{1.5:.2f}"
            b = f"{1.23456789:.3g}"
            c = f"{0.126:.1%}"
            d = f"{123.456:.0f}"
            e = f"{float('inf'):.100000f}"
            return [a, b, c, d, e]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new List<object?> { "1.50", "1.23", "12.6%", "123", "inf" };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task PercentWidthStaysCharged()
    {
        // MG07: percent width padding streams through the governed builder,
        // so a two-million pad fails on growth charges instead of escaping.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return "%2000000s" % "x"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task LjustWidthStaysCharged()
    {
        // MG07: ljust/rjust/center pad through the same governed builder
        // shape; a two-million pad fails on growth charges.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return "x".ljust(2000000)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        (await script.RunAsync(new MockLythonHost(), options)).Dispose();
        var beforeAsync = GC.GetTotalAllocatedBytes(true);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(GC.GetTotalAllocatedBytes(true) - beforeAsync < 1048576);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
}
