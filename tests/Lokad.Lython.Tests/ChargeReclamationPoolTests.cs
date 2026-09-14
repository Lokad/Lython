using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// PERF01: old-tier sweeps visit each entry at most once per sweep up to the
// quantum (no wrap-around revisits), and exhaustion relief skips collections
// it cannot benefit from. Cursor/contents below are observed by reflection;
// committed bytes prove release math.
public sealed class ChargeReclamationPoolTests
{
    private static ChargeReclamationPool NewPool(MemoryGovernor governor)
        => new ChargeReclamationPool(governor);

    private static int OldCursor(ChargeReclamationPool pool)
        => (int)typeof(ChargeReclamationPool).GetField("_oldCursor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;

    private static int OldCount(ChargeReclamationPool pool)
    {
        var tier = typeof(ChargeReclamationPool).GetField("_old", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;
        return ((System.Collections.IList)tier).Count;
    }

    private static List<object> TrackLive(ChargeReclamationPool pool, MemoryGovernor governor, int count)
    {
        var keys = new List<object>();
        for (var i = 0; i < count; i++)
        {
            keys.Add(new object());
        }

        for (var i = 0; i < keys.Count; i++)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(keys[i], 128);
        }

        return keys;
    }

    [Fact]
    public void EmptyPoolSweepIsNoop()
    {
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        Assert.Equal(0, pool.Sweep());
        Assert.Equal(0, OldCursor(pool));
    }

    [Fact]
    public void TinyTierWrapsToStart()
    {
        // A complete pass wraps the cursor to the start: with one live entry
        // the first sweep promotes it and visits it, ending back at zero.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 1);
        pool.Sweep();
        Assert.Equal(1, OldCount(pool));
        Assert.Equal(0, OldCursor(pool));
        pool.Sweep();
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(1, OldCount(pool));
        GC.KeepAlive(keys);
    }

    [Fact]
    public void LargeTierAdvancesExactlyOneQuantum()
    {
        // The first sweep promotes the young tier then visits the first
        // quantum of the old tier; later sweeps advance one quantum each and
        // wrap only on completion (the old code wrapped mid-sweep and
        // revisited entries).
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5000);
        pool.Sweep();
        Assert.Equal(5000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        pool.Sweep();
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(5000, pool.Count);
        pool.Sweep();
        Assert.Equal(4096, OldCursor(pool));
        GC.KeepAlive(keys);
    }

    [Fact]
    public void ShrinkingTierReleasesExactDead()
    {
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 100);
        pool.Sweep();
        Assert.Equal(100, OldCount(pool));
        DropHalf(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = pool.Sweep();
        Assert.Equal(50 * (128 + 64), released);
        Assert.Equal(50, pool.Count);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void MixedTierReleasesDeadAndKeepsLive()
    {
        // Scattered dead entries release exactly once across quantum windows:
        // removal compacts from the end, which can only pull unvisited entries
        // into the window, so live entries are never lost or double-visited.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5000);
        pool.Sweep();
        Assert.Equal(5000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        DropOdd(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = 0L;
        var sweeps = 0;
        while (pool.Count > 2500 && sweeps < 10)
        {
            released += pool.Sweep();
            sweeps++;
        }

        Assert.Equal(2500 * (128 + 64), released);
        Assert.Equal(2500, pool.Count);
        GC.KeepAlive(keys);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropHalf(List<object> keys)
    {
        keys.RemoveRange(0, 50);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropOdd(List<object> keys)
    {
        for (var i = 1; i < keys.Count; i += 2)
        {
            keys[i] = null!;
        }
    }

    [Fact]
    public void ImpossibleRequestDeniesWithoutSweeping()
    {
        // A request larger than the whole budget can never fit, so it denies
        // without pausing for a collection: the live pool is untouched.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        var keys = TrackLive(context.State.CallTemporaries, context.MemoryGovernor, 4);
        context.State.CallTemporaries.Sweep();
        var before = context.State.CallTemporaries.Count;
        var failure = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.EnsureCanReserve(2048, null));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(before, context.State.CallTemporaries.Count);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void EmptyPoolsSkipCollectionRelief()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        context.MemoryGovernor.Reserve(1024, null);
        context.MemoryGovernor.Commit(1024);
        Assert.Equal(0, context.State.CallTemporaries.Count);
        var failure = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.EnsureCanReserve(64, null));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }
}
