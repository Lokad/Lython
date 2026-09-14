using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG05: set construction must reserve before it copies. The snapshot array
/// used to materialize before the table charge; lazy sources drained fully
/// into an uncharged array before the first reservation.
/// </summary>
public sealed class SetConstructionAccountingTests
{
    [Fact]
    public void LazySourceConstructionStopsAtBudgetInsteadOfSnapshotting()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var pulls = 0;
        var lazy = Enumerable.Range(0, 100000).Select(value =>
        {
            pulls++;
            return (object)new BigInteger(value);
        });

        var failure = Assert.Throws<LythonRuntimeException>(() => new PySet(lazy, context.MemoryGovernor, span));

        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.True(pulls < 100000, "Lazy source was drained into a snapshot before the budget check.");
        Assert.True(pulls > 100, "Construction made no incremental progress before failing.");
    }

    [Fact]
    public void SizedCopyHoldsSnapshotTransientlyAlongsideTable()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PySet(Enumerable.Range(0, 100).Select(static value => (object)new BigInteger(value)));

        var copy = new PySet(source, context.MemoryGovernor, span);

        Assert.Equal(100, copy.Count);
        Assert.True(copy.SetEquals(source));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes > context.MemoryGovernor.CurrentCommittedBytes,
            "Snapshot scratch was never covered while the table was retained.");
    }

    [Fact]
    public void EmptyCopiesOwnShells()
    {
        // Empty payloads and snapshot scratch stay free, but each distinct
        // object owns its shell: three empty copies commit three shells.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);

        var fromSet = new PySet(new PySet(), context.MemoryGovernor, span);
        var fromArray = new PySet(Array.Empty<object>(), context.MemoryGovernor, span);
        var fromList = new PySet(new List<object>(), context.MemoryGovernor, span);

        Assert.Equal(0, fromSet.Count);
        Assert.Equal(0, fromArray.Count);
        Assert.Equal(0, fromList.Count);
        Assert.Equal(3 * 128, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void EmptyCopyDeniedWithoutShellBudget()
    {
        var governor = new MemoryGovernor(0);
        var span = new LythonSourceSpan(0, 0, 0, 0);

        var failure = Assert.Throws<LythonRuntimeException>(() => new PySet(governor, span));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }

    [Fact]
    public void IntersectFilterScratchStaysBounded()
    {
        // The N-item filter list coexists with the live table; pre-fix only
        // the table was charged, so no new peak could appear during the op.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var left = new PySet(Enumerable.Range(0, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var right = new PySet(Enumerable.Range(0, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;

        left.IntersectWith(right);

        Assert.Equal(20000, left.Count);
        Assert.True(left.SetEquals(right));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes > peakBefore,
            "Filter scratch was never covered while the table was retained.");
    }

    [Fact]
    public void ExceptFilterScratchStaysBounded()
    {
        // Same transient site as IntersectWith, pinned separately so each
        // reservation line is covered.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var left = new PySet(Enumerable.Range(0, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var right = new PySet(Enumerable.Range(10000, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;

        left.ExceptWith(right);

        Assert.Equal(10000, left.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes > peakBefore,
            "Filter scratch was never covered while the table was retained.");
    }

    [Fact]
    public void SymmetricDifferenceScratchStaysBounded()
    {
        // Identical inputs keep the rebuilt table within existing capacity, so
        // only the combined scratch reservation can move the peak.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var left = new PySet(Enumerable.Range(0, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var right = new PySet(Enumerable.Range(0, 20000).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;

        left.SymmetricExceptWith(right);

        Assert.Equal(0, left.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes > peakBefore,
            "Symmetric scratch was never covered while the tables were retained.");
    }

    [Fact]
    public void EmptyFiltersOwnShellsOnly()
    {
        // Attaching owns each distinct shell, but empty filter bounds still
        // allocate and reserve nothing, so the operations add no payload.
        var governor = new MemoryGovernor(1024);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var empty = new PySet();
        empty.AttachMemoryGovernor(governor);
        var other = new PySet();
        other.AttachMemoryGovernor(governor);

        empty.IntersectWith(other);
        empty.ExceptWith(other);
        empty.SymmetricExceptWith(other);

        Assert.Equal(0, empty.Count);
        Assert.Equal(2 * 128, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
    [Fact]
    public void ClearKeepsShellButReleasesCapacity()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var set = new PySet(Enumerable.Range(0, 100).Select(static value => (object)new BigInteger(value)), context.MemoryGovernor, span);
        var grown = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(grown > 128, "Capacity growth committed nothing beyond the shell.");

        set.Clear();
        Assert.Equal(0, set.Count);
        Assert.Equal(128, context.MemoryGovernor.CurrentCommittedBytes);

        set.Add(new BigInteger(1));
        Assert.Equal(1, set.Count);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 128, "Regrowth after clear paid no capacity.");
    }

    [Fact]
    public void AttachOwnsShellOnce()
    {
        var first = new MemoryGovernor(1024);
        var second = new MemoryGovernor(1024);
        var set = new PySet();

        set.AttachMemoryGovernor(first);
        Assert.Equal(128, first.CurrentCommittedBytes);

        set.AttachMemoryGovernor(second);
        Assert.Equal(128, first.CurrentCommittedBytes);
        Assert.Equal(0, second.CurrentCommittedBytes);
    }

    [Fact]
    public void CopyCtorOwnsShell()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PySet(context.MemoryGovernor, span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;

        var copy = new PySet(source);

        Assert.Equal(0, copy.Count);
        Assert.Equal(before + 128, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
