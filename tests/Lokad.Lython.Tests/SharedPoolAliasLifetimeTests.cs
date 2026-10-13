using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class SharedPoolAliasLifetimeTests
{
    [Fact]
    public void CollectedCsvSourceKeepsItsEscapingFieldIndependentlyFunded()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (alias, source, row) = ReadAndDropSource(context);
        Collect();
        Assert.False(source.TryGetTarget(out _));
        Assert.False(row.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.True(context.State.CallTemporaries.IsTracked(alias));
        Assert.True(((IPyOwnershipSnapshot)alias).TrySnapshotOwnership(out var owned));
        Assert.Equal(owned + ChargeReclamationPool.EntryChargeBytes + context.State.CallTemporaries.CommittedBackingBytes,
            context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal("kept", alias.AsString());
        GC.KeepAlive(alias);
    }

    [Fact]
    public void ExhaustionReclaimsDeadStorageWithoutRefundingALiveAlias()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 32768 });
        var governor = context.MemoryGovernor;
        var alias = PyString.FromString(new string('x', 16000), governor);
        context.State.CallTemporaries.TrackFreshString(alias);
        var dead = DropPayload(context, 12000);
        Collect();
        Assert.False(dead.TryGetTarget(out _));
        governor.Reserve(14000, null); // requires the collected payload's refund
        Assert.Equal(14000, governor.CurrentReservedBytes);
        Assert.True(context.State.CallTemporaries.IsTracked(alias));
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.True(((IPyOwnershipSnapshot)alias).TrySnapshotOwnership(out var owned));
        Assert.Equal(owned + ChargeReclamationPool.EntryChargeBytes + context.State.CallTemporaries.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        governor.ReleaseReserved(14000);
        GC.KeepAlive(alias);
    }

    [Fact]
    public void ExhaustionRejectsWhenTheAliasStorageRemainsLive()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 32768 });
        var governor = context.MemoryGovernor;
        var alias = PyString.FromString(new string('x', 16000), governor);
        context.State.CallTemporaries.TrackFreshString(alias);
        var before = governor.CurrentCommittedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(20000, null));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.True(context.State.CallTemporaries.IsTracked(alias));
        GC.KeepAlive(alias);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> DropPayload(LythonRuntime.ExecutionContext context, int bytes)
    {
        var value = PyString.FromString(new string('d', bytes), context.MemoryGovernor);
        context.State.CallTemporaries.TrackFreshString(value);
        return new(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PyString Alias, WeakReference<LythonRuntime.CsvRecordSource> Source, WeakReference<PyList> Row)
        ReadAndDropSource(LythonRuntime.ExecutionContext context)
    {
        var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, null,
            LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
        var source = new LythonRuntime.CsvRecordSource(new PyList(new object[] { PyString.FromString("kept,dropped\n") }),
            options, new(0, 0, 0, 0), context);
        Assert.True(source.TryMoveNext(out var row));
        return ((PyString)row[0], new(source), new(row));
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
