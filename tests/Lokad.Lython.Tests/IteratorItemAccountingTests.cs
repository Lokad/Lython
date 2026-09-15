using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// M05: per-item enumerate tuples track exactly once at their TryMoveNext factory
/// (64 B tuple plus one 128 B entry beside the already-tracked 128 B shell), aliases
/// dedup to the first registration, and abandoned pulls release through a full sweep.
/// Deterministic via explicit collection plus a full drain.
/// </summary>
public sealed class IteratorItemAccountingTests
{
    [Fact]
    public void EnumerateItemsTrackExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList(new object[] { new BigInteger(10), new BigInteger(20) }, context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var countBefore = context.State.CallTemporaries.Count;
        var iterator = new PyEnumerateIterator(source, BigInteger.Zero, span, context);
        // The shell entry is owned by the enumerate() call site; mirror it here.
        context.State.CallTemporaries.TrackFreshMutable(iterator, PyIteratorBase.IteratorValueBytes, span);
        Assert.True(iterator.TryMoveNext(out var first));
        Assert.True(iterator.TryMoveNext(out var second));
        Assert.Equal(new BigInteger(0), ((PyTuple)first)[0]);
        Assert.Equal(new BigInteger(1), ((PyTuple)second)[0]);
        // Shell (128) plus its entry (128) and first tier growth (32), then one
        // 64 B tuple and one 128 B entry per retained item.
        Assert.Equal(committedBefore + 128L + 128L + 32L + 2L * (64L + 128L), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(countBefore + 3, context.State.CallTemporaries.Count);
        GC.KeepAlive(iterator);
        GC.KeepAlive(source);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void EnumerateRetrackDedups()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList(new object[] { new BigInteger(10) }, context.MemoryGovernor, span);
        var iterator = new PyEnumerateIterator(source, BigInteger.Zero, span, context);
        // The shell entry is owned by the enumerate() call site; mirror it here.
        context.State.CallTemporaries.TrackFreshMutable(iterator, PyIteratorBase.IteratorValueBytes, span);
        Assert.True(iterator.TryMoveNext(out var first));
        var count = context.State.CallTemporaries.Count;
        context.State.CallTemporaries.TrackCallResult(first, span);
        context.State.CallTemporaries.TrackFreshMutable(first, 64L);
        Assert.Equal(count, context.State.CallTemporaries.Count);
        GC.KeepAlive(iterator);
        GC.KeepAlive(source);
        GC.KeepAlive(first);
    }

    [Fact]
    public void AbandonedEnumerateItemsReclaim()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        AbandonEnumeratePulls(context, span);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        Assert.True(committed < 100000, "committed=" + committed + " reserved=" + reserved);
        Assert.True(reserved < 100000, "committed=" + committed + " reserved=" + reserved);
    }

    // All pulled tuples and the iterator die with this frame, so no test slots root them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonEnumeratePulls(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var range = new PyRange(BigInteger.Zero, new BigInteger(2000), BigInteger.One);
        var iterator = new PyEnumerateIterator(range, BigInteger.Zero, span, context);
        for (var i = 0; i < 2000; i++)
        {
            Assert.True(iterator.TryMoveNext(out _));
        }
    }
}
