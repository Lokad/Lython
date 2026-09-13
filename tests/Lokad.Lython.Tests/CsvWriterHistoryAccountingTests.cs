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
}
