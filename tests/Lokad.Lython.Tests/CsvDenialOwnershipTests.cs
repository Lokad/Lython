using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvDenialOwnershipTests
{
    private const long Cap = 3L * 1024 * 1024;
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void DeniedFieldGrowthPreservesPhysicalCapacityAndFunding()
    {
        var context = Context();
        var source = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(source);
        parser.Feed("\"" + new string('x', 15)); // newline fills the initial16 chars
        var field = (StringBuilder)typeof(LythonRuntime.CsvRecordParser)
            .GetField("_field", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(parser)!;
        Assert.Equal(16, field.Length);
        Assert.Equal(16, field.Capacity);
        var owned = source.OwnedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var pressure = Cap - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("x"));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(16, field.Length);
        Assert.Equal(16, field.Capacity);
        Assert.Equal(owned, source.OwnedBytes);
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(pressure, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("ok\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal("ok", ((PyString)row[0]).AsString());
    }

    [Fact]
    public void EofReleasesSourceScratchWithoutRefundingEscapedFields()
    {
        var context = Context();
        var source = new LythonRuntime.CsvRecordSource(
            new PyList(new object[] { PyString.FromString("left,right\n") }), Options(), Span, context);
        var escaped = ReadEscapedField(source);
        Assert.True(source.OwnedBytes > LythonRuntime.CsvRecordSource.ShellBytes);
        Assert.False(source.TryMoveNext(out _));
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
        GC.Collect();
        Assert.Equal("left", escaped.AsString());
        Assert.True(((IPyOwnershipSnapshot)escaped).TrySnapshotOwnership(out var bytes));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= source.OwnedBytes + bytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(source);
        GC.KeepAlive(escaped);
    }

    [Fact]
    public void DeniedSourceAdmissionRefundsItsUnpublishedStorage()
    {
        var context = Context();
        var pressure = Cap - (LythonRuntime.CsvRecordSource.ShellBytes + 32 - 1);
        context.MemoryGovernor.Reserve(pressure, Span);
        var error = Assert.Throws<LythonRuntimeException>(() =>
            new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(pressure, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.ReleaseReserved(pressure);
        var source = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        Assert.False(source.TryMoveNext(out _));
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PyString ReadEscapedField(LythonRuntime.CsvRecordSource source)
    {
        Assert.True(source.TryMoveNext(out var row));
        return (PyString)row[0];
    }

    [Fact]
    public void DeniedFieldPayloadAdmissionPreservesCurrentParserStorage()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(owner);
        parser.Feed("\""); // Pending quoted field contains one synthesized newline.
        var committed = governor.CurrentCommittedBytes;
        var reserved = governor.CurrentReservedBytes;
        // Deny the 129-byte UTF-8 field before its storage is allocated.
        var pressure = Cap - governor.CurrentAccountedBytes - 128;
        governor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\","));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.True(governor.PeakAccountedBytes <= Cap);
        governor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("ok\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal("ok", ((PyString)row[0]).AsString());
        parser.ReleaseScratch();
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Fact]
    public void DeniedRowBackingAdmissionPreservesCompletedFields()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(owner);
        parser.Feed("x,\""); // Retained first field and pending newline field.
        var committed = governor.CurrentCommittedBytes;
        var reserved = governor.CurrentReservedBytes;
        // Finish the field, then deny private row backing before publication.
        var pressure = Cap - governor.CurrentAccountedBytes - (129 + 192 - 1);
        governor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\""));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.Equal(committed + 129,
            governor.CurrentCommittedBytes);
        Assert.True(governor.PeakAccountedBytes <= Cap);
        governor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("ok\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal("ok", ((PyString)row[0]).AsString());
        parser.ReleaseScratch();
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialRowsStayOwnedUntilTheirExplicitBatchEnds(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(Cap);
        using var owner = budget.CreateScope();
        var context = Context(owner);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var abandoned = AbandonPartialSource(context, asynchronous);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(abandoned.TryGetTarget(out _));
        Assert.True(budget.ReservedBytes > committed);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(context);
    }

    [Theory]
    [InlineData("x,\"", 96L)]
    [InlineData("a,b,c,d,e,\"", 160L)]
    public void PartialRowsFundTheirCurrentScratchCapacityOnce(string input, long expected)
    {
        var context = Context();
        var owner = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(owner);
        parser.Feed(input);
        // 32-byte field builder plus 64/128 bytes for four/eight row slots.
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes + expected, owner.OwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(parser);
    }

    [Fact]
    public void EmptyPhysicalLinesPublishIndependentlyTrackedRows()
    {
        var context = Context();
        var owner = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(owner);
        parser.Feed(string.Empty);
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Empty(row);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Fact]
    public void DeniedScratchGrowthKeepsEarlierFundingAndCanRetry()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new LythonRuntime.CsvRecordSource(new PyList([]), Options(), Span, context);
        var parser = Parser(owner);
        parser.Feed("a,b,c,d,\"");
        var reserved = governor.CurrentReservedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - (129 + 128 + 127);
        governor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\""));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.False(parser.TryTakeReady(out _));
        governor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("a,b,c,d,e\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, row.Select(value => ((PyString)value).AsString()));
        parser.ReleaseScratch();
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.True(governor.PeakAccountedBytes <= Cap);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Fact]
    public void ShrinkingScratchCannotSpendAnotherReservationsFunding()
    {
        var governor = new MemoryGovernor(Cap);
        governor.Reserve(128, Span);
        using var scratch = governor.ReserveTemporary(96, Span);
        scratch.Shrink(64);
        Assert.Equal(160, governor.CurrentReservedBytes);
        Assert.Throws<InvalidOperationException>(() => scratch.Shrink(64));
        Assert.Equal(160, governor.CurrentReservedBytes);
        scratch.Dispose();
        Assert.Equal(128, governor.CurrentReservedBytes);
        governor.ReleaseReserved(128);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<LythonRuntime.CsvRecordSource> AbandonPartialSource(
        LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var source = new LythonRuntime.CsvRecordSource(
            new PyList(new object[] { PyString.FromString("x,\""), 42 }), Options(), Span, context);
        var error = asynchronous
            ? Assert.Throws<LythonRuntimeException>(() => source.TryMoveNextAsync().GetAwaiter().GetResult())
            : Assert.Throws<LythonRuntimeException>(() => source.TryMoveNext(out _));
        Assert.Equal("TypeError", error.ExceptionType);
        return new(source);
    }

    private static LythonRuntime.CsvRecordParser Parser(LythonRuntime.CsvRecordSource source)
        => (LythonRuntime.CsvRecordParser)typeof(LythonRuntime.CsvRecordSource)
            .GetField("_parser", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!;

    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget? budget = null) => new(new MockLythonHost(),
        new LythonRunOptions { MaxExecutionMemoryBytes = Cap, MemoryBudget = budget });

    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral,
        PyString.FromString("\""), LythonRuntime.CsvQuotingMode.Minimal, true, null,
        false, PyString.FromString("\n"), false);
}
