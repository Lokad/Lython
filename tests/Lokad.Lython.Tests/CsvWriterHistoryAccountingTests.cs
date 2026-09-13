using System.Numerics;
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

    [Fact]
    public void OversizedIntegerConversionTripsBeforeFormatting()
    {
        // 10^2000 formats to 2001 digits (2129 estimated bytes) but the
        // pre-format bound holds 2132, so a 2130 budget trips before the CLR
        // string is built; without the reservation nothing trips.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 2130 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var row = new PyList(new object[] { BigInteger.Pow(10, 2000) });
        var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.CsvWriterMembers.ToCsvRow(row, span, context, out _));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0L, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void OrdinaryConversionsStayExact()
    {
        // Small ints, floats, bools, strings and None convert with exact
        // retention estimates while the build transient releases.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 100000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var row = new PyList(new object[] { PyNone.Instance, PyString.FromString("ab"), new BigInteger(42), true, 1.5 });
        var cells = LythonRuntime.CsvWriterMembers.ToCsvRow(row, span, context, out var converted);
        Assert.Equal(5, cells.Length);
        Assert.Same(PyString.Empty, cells[0].Text);
        Assert.Equal("ab", cells[1].Text.AsString());
        Assert.Equal("42", cells[2].Text.AsString());
        Assert.Equal("True", cells[3].Text.AsString());
        Assert.Equal("1.5", cells[4].Text.AsString());
        Assert.Equal((128L + 2L) + (128L + 4L) + (128L + 3L), converted);
        Assert.Equal(0L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0L, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void WideDictWriterowsSharesKnownSetAcrossRows()
    {
        // MG02: DictWriter.writerows builds the extras known-set once per call
        // instead of once per row, and fills an exact row array instead of
        // draining through a second list; per-thread allocation for 20 wide
        // rows drops accordingly (sync-only per the GC-measurement discipline).
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var fieldNames = new PyString[2000];
        for (var i = 0; i < fieldNames.Length; i++)
        {
            fieldNames[i] = PyString.FromString("f" + i);
        }

        var sharedValue = PyString.FromString("v");
        var rows = new PyList();
        for (var r = 0; r < 20; r++)
        {
            var dict = new PyDict();
            foreach (var name in fieldNames)
            {
                dict.SetItem(name, sharedValue);
            }

            rows.Add(dict);
        }

        // One unmeasured warmup invoke settles JIT so the measured call
        // sees only the writerows steady state.
        var warmup = CreateWriter(context, span);
        var warmupWriter = new LythonRuntime.CsvDictWriterObject(
            warmup,
            fieldNames,
            PyNone.Instance,
            LythonRuntime.CsvExtrasAction.Ignore);
        Assert.True(LythonRuntime.CsvDictWriterMembers.TryGetMember(warmupWriter, "writerows", out var warmupMember));
        var warmupCallable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(warmupMember);
        warmupCallable.Invoke(new[] { CallArgumentValue.Positional(rows) }, span, context);
        var writer = CreateWriter(context, span);
        var dictWriter = new LythonRuntime.CsvDictWriterObject(
            writer,
            fieldNames,
            PyNone.Instance,
            LythonRuntime.CsvExtrasAction.Ignore);
        Assert.True(LythonRuntime.CsvDictWriterMembers.TryGetMember(dictWriter, "writerows", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        callable.Invoke(new[] { CallArgumentValue.Positional(rows) }, span, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(20, writer.Rows.Count);
        Assert.Equal("v", writer.Rows[0][0].Text.AsString());
        Assert.True(allocated < 4500000, $"allocated {allocated}");
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
