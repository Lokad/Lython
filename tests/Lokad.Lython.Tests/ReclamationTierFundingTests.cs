using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ReclamationTierFundingTests
{
    [Fact]
    public void ReclaimedSlotsDoNotCommitUnusedPredictedTierGrowth()
    {
        // Four 1-byte values + four 128-byte entries + four young slots = 548.
        // The fifth value/entry fit, but its predicted 32-byte growth misses by
        // one byte. Relief prunes a dead entry and promotes the three live ones,
        // leaving the existing young slots available for the new publication.
        var governor = new MemoryGovernor(708);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => new[] { pool };
        var (retained, dead) = FillYoung(governor, pool);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(548, governor.CurrentCommittedBytes);

        var added = new object();
        governor.Reserve(1, null);
        governor.Commit(1);
        pool.Track(added, 1);

        Assert.Equal(4, pool.Count);
        var actualBacking = ActualBacking(pool);
        Assert.Equal(64, actualBacking);
        Assert.Equal(actualBacking, pool.CommittedBackingBytes);
        Assert.Equal(4 * (1 + ChargeReclamationPool.EntryChargeBytes) + actualBacking,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
        GC.KeepAlive(added);
    }

    [Fact]
    public void PrefundedEscapeRefundsTierGrowthMadeUnnecessaryByRelief()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => new[] { pool };
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var list = LythonRuntime.OwnFreshStringSplitListResult(
            PyStringOps.Split(PyString.FromString("x|y"), PyString.FromString("|"), governor, span), span, pool);
        var (retained, dead) = AddThree(governor, pool);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(4, pool.Count);
        var before = governor.CurrentCommittedBytes;
        var pressure = 65536 - before - 31;
        governor.Reserve(pressure, span);

        var escaped = (PyString)list[0];

        Assert.Equal("x", escaped.AsString());
        Assert.Equal(4, pool.Count);
        Assert.True(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(64, ActualBacking(pool));
        Assert.Equal(ActualBacking(pool), pool.CommittedBackingBytes);
        Assert.Equal(before - 129 + 32, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        governor.ReleaseReserved(pressure);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(list);
        GC.KeepAlive(retained);
        GC.KeepAlive(escaped);
    }

    [Fact]
    public void PinnedFullTierStillDeniesGrowthWithoutPublishingOrStrandingReservations()
    {
        var governor = new MemoryGovernor(708);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => new[] { pool };
        var retained = Enumerable.Range(0, 4).Select(_ => new object()).ToArray();
        foreach (var value in retained)
        {
            governor.Reserve(1, null);
            governor.Commit(1);
            pool.Track(value, 1);
        }
        var added = new object();
        governor.Reserve(1, null);
        governor.Commit(1);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => pool.Track(added, 1)).ExceptionType);
            Assert.False(pool.IsTracked(added));
            Assert.Equal(4, pool.Count);
            Assert.Equal(32, ActualBacking(pool));
            Assert.Equal(ActualBacking(pool), pool.CommittedBackingBytes);
            Assert.Equal(549, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        GC.KeepAlive(retained);
        GC.KeepAlive(added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CsvFieldRegistrationCommitsOnlyBackingThatActuallyGrew(bool asynchronous)
    {
        const long cap = 3L * 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, null,
            LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
        var source = new LythonRuntime.CsvRecordSource(new PyList(new object[]
            { PyString.FromString("x,y,q\n"), PyString.FromString("z\n") }), options, span, context);
        var (retained, row, field) = ReadAndDropRow(source, asynchronous);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(row.TryGetTarget(out _));
        Assert.False(field.TryGetTarget(out _));
        Assert.Equal(4, source.Pool.Count);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var reservedBefore = governor.CurrentReservedBytes;
        // Force the first new field's tier-growth reservation to invoke relief.
        // This models a transition, not the intermittent public C05 scenario.
        var pressure = cap - governor.CurrentAccountedBytes - (129 + 128 + 31);
        governor.Reserve(pressure, span);

        var next = asynchronous ? await source.TryMoveNextAsync() : Next(source);

        Assert.NotNull(next);
        Assert.Equal("z", ((PyString)next[0]).AsString());
        Assert.Equal(4, source.Pool.Count);
        Assert.Equal(64, ActualBacking(source.Pool));
        Assert.Equal(ActualBacking(source.Pool), source.Pool.CommittedBackingBytes);
        // The dead three-cell row and one dropped field refund exactly the
        // new one-cell row/field charges. Only real old-tier growth remains.
        Assert.Equal(before + 32, governor.CurrentCommittedBytes);
        Assert.Equal(reservedBefore + pressure, governor.CurrentReservedBytes);
        Assert.True(governor.PeakAccountedBytes <= cap);
        governor.ReleaseReserved(pressure);
        Assert.Null(asynchronous ? await source.TryMoveNextAsync() : Next(source));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(ActualBacking(source.Pool), source.Pool.CommittedBackingBytes);
        GC.KeepAlive(source);
        GC.KeepAlive(retained);
        GC.KeepAlive(next);
    }

    private static PyList? Next(LythonRuntime.CsvRecordSource source) => source.TryMoveNext(out var row) ? row : null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PyString[] Retained, WeakReference<PyList> Row, WeakReference<object> Field)
        ReadAndDropRow(LythonRuntime.CsvRecordSource source, bool asynchronous)
    {
        var row = asynchronous ? source.TryMoveNextAsync().GetAwaiter().GetResult()! : Next(source)!;
        return (new[] { (PyString)row[0], (PyString)row[1] }, new(row), new(row[2]));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object[] Retained, WeakReference<object> Dead) AddThree(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var values = Enumerable.Range(0, 3).Select(_ => new object()).ToArray();
        foreach (var value in values)
        {
            governor.Reserve(1, null);
            governor.Commit(1);
            pool.Track(value, 1);
        }
        return (values.Skip(1).ToArray(), new WeakReference<object>(values[0]));
    }

    private static long ActualBacking(ChargeReclamationPool pool) =>
        new[] { "_young", "_old" }.Sum(name => 8L *
            ((List<ChargeReclamationPool.ReclamationEntry>)typeof(ChargeReclamationPool)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!).Capacity);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object[] Retained, WeakReference<object> Dead) FillYoung(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var values = Enumerable.Range(0, 4).Select(_ => new object()).ToArray();
        foreach (var value in values)
        {
            governor.Reserve(1, null);
            governor.Commit(1);
            pool.Track(value, 1);
        }
        return (values.Skip(1).ToArray(), new WeakReference<object>(values[0]));
    }
}
