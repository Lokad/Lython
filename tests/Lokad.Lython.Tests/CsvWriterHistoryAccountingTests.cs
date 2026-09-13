using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG02: the history reservation precedes insertion, so a row whose charge
/// trips the budget is never retained; a failed reservation leaves no
/// uncharged history behind and commits nothing.
/// </summary>
public sealed class CsvWriterHistoryAccountingTests
{
    [Fact]
    public void FailedHistoryReservationRetainsNothing()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 200 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var options = new LythonRuntime.CsvOptions(
            PyStringOps.CommaLiteral,
            null,
            LythonRuntime.CsvQuotingMode.Minimal,
            true,
            null,
            false,
            PyString.FromString("\n"),
            false);
        var writer = new LythonRuntime.CsvWriterObject(options, null, context.MemoryGovernor, span);
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        var row = new LythonRuntime.CsvCell[] { new(PyString.Empty, LythonRuntime.CsvCellKind.Text) };
        var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.CsvWriterMembers.WriteRow(writer, row, span, context, 0));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Empty(writer.Rows);
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0L, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void HistoryGrowthTransientTripsBeforeUnchargedOverlap()
    {
        // Thirty-two one-cell rows fill the history list to capacity 32 (128
        // shell plus 32 x 80). The 33rd row fits its own 80-byte charge under
        // 3100 but not the 512-byte backing-array overlap, so it trips
        // retaining nothing new and leaking no reserve. Earlier doublings
        // (32/64/128/256 bytes) all fit this budget.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 3100 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var writer = CreateWriter(context, span);
        for (var i = 0; i < 32; i++)
        {
            WriteOneCellRow(writer, span, context);
        }

        Assert.Equal(32, writer.Rows.Count);
        Assert.Equal(2688L, context.MemoryGovernor.CurrentCommittedBytes);
        var failure = Assert.Throws<LythonRuntimeException>(() => WriteOneCellRow(writer, span, context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(32, writer.Rows.Count);
        Assert.Equal(2688L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0L, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void FundedHistoryGrowthSucceeds()
    {
        // The same 33rd row fits when the budget also covers the 512-byte
        // backing-array overlap, locking the trip boundary above.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 3300 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var writer = CreateWriter(context, span);
        for (var i = 0; i < 33; i++)
        {
            WriteOneCellRow(writer, span, context);
        }

        Assert.Equal(33, writer.Rows.Count);
        Assert.Equal(2768L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0L, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LythonRuntime.CsvWriterObject CreateWriter(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var options = new LythonRuntime.CsvOptions(
            PyStringOps.CommaLiteral,
            null,
            LythonRuntime.CsvQuotingMode.Minimal,
            true,
            null,
            false,
            PyString.FromString("\n"),
            false);
        return new LythonRuntime.CsvWriterObject(options, null, context.MemoryGovernor, span);
    }

    private static void WriteOneCellRow(LythonRuntime.CsvWriterObject writer, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var row = new LythonRuntime.CsvCell[] { new(PyString.Empty, LythonRuntime.CsvCellKind.Text) };
        LythonRuntime.CsvWriterMembers.WriteRow(writer, row, span, context, 0);
    }
}
