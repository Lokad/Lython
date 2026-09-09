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
    public void EmptyCopyChargesNothing()
    {
        var governor = new MemoryGovernor(0);
        var span = new LythonSourceSpan(0, 0, 0, 0);

        var fromSet = new PySet(new PySet(), governor, span);
        var fromArray = new PySet(Array.Empty<object>(), governor, span);
        var fromList = new PySet(new List<object>(), governor, span);

        Assert.Equal(0, fromSet.Count);
        Assert.Equal(0, fromArray.Count);
        Assert.Equal(0, fromList.Count);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
