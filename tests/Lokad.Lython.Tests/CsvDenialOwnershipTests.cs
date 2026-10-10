using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvDenialOwnershipTests
{
    private const long Cap = 3L * 1024 * 1024;
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void DeniedFieldRegistrationReturnsUnpublishedPayload()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new object();
        var pool = new ChargeReclamationPool(governor);
        using var scratch = governor.ReserveTemporary(0, Span);
        context.State.RegisterPool(owner, pool, scratch);
        var parser = new LythonRuntime.CsvRecordParser(Options(), context, Span, scratch, pool);
        parser.Feed("\""); // Pending quoted field contains one synthesized newline.
        var committed = governor.CurrentCommittedBytes;
        var reserved = governor.CurrentReservedBytes;
        // The 129-byte field fits, but its 128-byte registration cannot.
        var pressure = Cap - governor.CurrentAccountedBytes - (129 + 127);
        governor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\","));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, pool.Count);
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.True(governor.PeakAccountedBytes <= Cap);
        governor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("ok\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal("ok", ((PyString)row[0]).AsString());
        scratch.Dispose();
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Fact]
    public void DeniedRowRegistrationReturnsUnpublishedBacking()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new object();
        var pool = new ChargeReclamationPool(governor);
        using var scratch = governor.ReserveTemporary(0, Span);
        context.State.RegisterPool(owner, pool, scratch);
        var parser = new LythonRuntime.CsvRecordParser(Options(), context, Span, scratch, pool);
        parser.Feed("x,\""); // Retained first field and pending newline field.
        Assert.Equal(1, pool.Count);
        var committed = governor.CurrentCommittedBytes;
        var reserved = governor.CurrentReservedBytes;
        var backing = pool.CommittedBackingBytes;
        // Finish the field and allocate the row, then deny the row's entry fee.
        var pressure = Cap - governor.CurrentAccountedBytes - (129 + 128 + 192 + 127);
        governor.Reserve(pressure, Span);

        var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\""));

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(2, pool.Count); // Both fields stay rooted in the partial row.
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.Equal(committed + 129 + 128 + pool.CommittedBackingBytes - backing,
            governor.CurrentCommittedBytes);
        Assert.True(governor.PeakAccountedBytes <= Cap);
        governor.ReleaseReserved(pressure);
        parser.ResetRecord();
        parser.Feed("ok\n");
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Equal("ok", ((PyString)row[0]).AsString());
        scratch.Dispose();
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedPartialRowsReturnAllScratch(bool asynchronous)
    {
        var context = Context();
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var abandoned = AbandonPartialSource(context, asynchronous);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes > reserved);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(abandoned.TryGetTarget(out _));
        foreach (var _ in context.State.LiveReclamationPools()) { }
        Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(context);
    }

    [Theory]
    [InlineData("x,\"", 96L)]
    [InlineData("a,b,c,d,e,\"", 160L)]
    public void PartialRowsFundTheirCurrentScratchCapacityOnce(string input, long expected)
    {
        var context = Context();
        var owner = new object();
        var pool = new ChargeReclamationPool(context.MemoryGovernor);
        using var scratch = context.MemoryGovernor.ReserveTemporary(0, Span);
        context.State.RegisterPool(owner, pool, scratch);
        var parser = new LythonRuntime.CsvRecordParser(Options(), context, Span, scratch, pool);
        parser.Feed(input);
        // 32-byte field builder plus 64/128 bytes for four/eight row slots.
        Assert.Equal(expected, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(owner);
        GC.KeepAlive(parser);
    }

    [Fact]
    public void EmptyPhysicalLinesPublishIndependentlyTrackedRows()
    {
        var context = Context();
        var owner = new object();
        var pool = new ChargeReclamationPool(context.MemoryGovernor);
        using var scratch = context.MemoryGovernor.ReserveTemporary(0, Span);
        context.State.RegisterPool(owner, pool, scratch);
        var parser = new LythonRuntime.CsvRecordParser(Options(), context, Span, scratch, pool);
        parser.Feed(string.Empty);
        Assert.True(parser.TryTakeReady(out var row));
        Assert.Empty(row);
        Assert.True(pool.IsTracked(row));
        Assert.Equal(1, pool.Count);
        GC.KeepAlive(owner);
        GC.KeepAlive(row);
    }

    [Fact]
    public void DeniedScratchGrowthKeepsEarlierFundingAndCanRetry()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var owner = new object();
        var pool = new ChargeReclamationPool(governor);
        using var scratch = governor.ReserveTemporary(0, Span);
        context.State.RegisterPool(owner, pool, scratch);
        var parser = new LythonRuntime.CsvRecordParser(Options(), context, Span, scratch, pool);
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
        scratch.Dispose();
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
        Assert.Equal(1, source.Pool.Count);
        return new(source);
    }

    private static LythonRuntime.ExecutionContext Context() => new(new MockLythonHost(),
        new LythonRunOptions { MaxExecutionMemoryBytes = Cap });

    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral,
        PyString.FromString("\""), LythonRuntime.CsvQuotingMode.Minimal, true, null,
        false, PyString.FromString("\n"), false);
}
