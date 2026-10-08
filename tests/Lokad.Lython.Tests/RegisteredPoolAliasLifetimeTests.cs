using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RegisteredPoolAliasLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedOwnerKeepsLiveAliasesTrackedUntilTheirCollection(bool oldTier)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var alias = RetainAliasAfterOwnerDies(context.State, oldTier);
        Collect();
        Assert.False(alias.TryGetTarget(out _));
        Assert.Single(context.State.LiveReclamationPools());
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedScratchReleasesBeforeSweepNeedsPromotionFunding(bool oldTier)
    {
        var cap = oldTier ? 5440L : 5408L;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var alias = RetainAliasAfterOwnerDies(context.State, oldTier);
        Collect();
        Assert.False(alias.TryGetTarget(out _));
        Assert.Single(context.State.LiveReclamationPools());
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal(0, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.True(context.MemoryGovernor.PeakAccountedBytes <= cap);
    }

    [Fact]
    public void DeniedPromotionKeepsTheActivePoolRegisteredForLaterReclamation()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1312 });
        var alias = DenyWhileAliasLives(context.State);
        Collect();
        Assert.False(alias.TryGetTarget(out _));
        Assert.Single(context.State.LiveReclamationPools());
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
    }

    [Fact]
    public void RetiringOtherSourcesDuringPromotionDoesNotInvalidateRegistryTraversal()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1568 });
        var alias = RetireOtherSourcesWhileAliasLives(context.State);
        Collect();
        Assert.False(alias.TryGetTarget(out _));
        Assert.Single(context.State.LiveReclamationPools());
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
    }

    [Fact]
    public void ExhaustionReliefRetiresBackingWhenTheLastOrphanedAliasesAreCollected()
    {
        const long cap = 2L * 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        DropManyAliasesAfterOwnerDies(context.State);
        context.MemoryGovernor.Reserve(cap, null);
        Assert.Equal(cap, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        context.MemoryGovernor.ReleaseReserved(cap);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
    }

    [Fact]
    public void ExhaustionEnumerationReachesCollectedValuesBeforeFundingPromotion()
    {
        // A retained source alias fills its pool; another pool has a collected
        // 512-byte temporary. Its refund can fund the requested 128 bytes.
        // Enumerating the sources must not spend promotion headroom first.
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1984 });
        var (alias, owner, pool) = CreateRegisteredAlias(context.State, false, scratchBytes: 0);
        var dead = DropCallTemporary(context.State);
        Collect();
        Assert.False(owner.TryGetTarget(out _));
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(1984, context.MemoryGovernor.CurrentCommittedBytes);

        context.MemoryGovernor.Reserve(128, null);

        Assert.Equal(128, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(1344, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(1, pool.Count);
        context.MemoryGovernor.ReleaseReserved(128);
        GC.KeepAlive(alias);
    }

    [Fact]
    public void ExhaustionEnumerationStillDeniesWhenAllAliasChargesAreLive()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1312 });
        var (alias, owner, pool) = CreateRegisteredAlias(context.State, false, scratchBytes: 0);
        Collect();
        Assert.False(owner.TryGetTarget(out _));

        var failure = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(128, null));

        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(1312, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(1, pool.Count);
        GC.KeepAlive(alias);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> DropCallTemporary(ExecutionState state)
    {
        var value = new object();
        state.MemoryGovernor.Reserve(512, null);
        state.MemoryGovernor.Commit(512);
        state.CallTemporaries.Track(value, 512);
        return new WeakReference<object>(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DropManyAliasesAfterOwnerDies(ExecutionState state)
    {
        var (aliases, owner, pool) = CreateManyRegisteredAliases(state);
        Collect();
        Assert.False(owner.TryGetTarget(out _));
        Assert.Contains(pool, state.LiveReclamationPools());
        Assert.Equal(5000, pool.Count);
        aliases.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (List<object> Aliases, WeakReference<object> Owner, ChargeReclamationPool Pool) CreateManyRegisteredAliases(ExecutionState state)
    {
        var owner = new object();
        var pool = new ChargeReclamationPool(state.MemoryGovernor);
        var aliases = new List<object>();
        for (var index = 0; index < 5000; index++)
        {
            var alias = new object();
            aliases.Add(alias);
            state.MemoryGovernor.Reserve(128, null);
            state.MemoryGovernor.Commit(128);
            pool.Track(alias, 128);
        }
        state.RegisterPool(owner, pool);
        return (aliases, new WeakReference<object>(owner), pool);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RetireOtherSourcesWhileAliasLives(ExecutionState state)
    {
        RegisterEmptySources(state);
        var (alias, owner, pool) = CreateRegisteredAlias(state, false, scratchBytes: 0);
        Collect();
        Assert.False(owner.TryGetTarget(out _));
        var live = state.LiveReclamationPools().ToList();
        Assert.Contains(pool, live);
        Assert.Equal(1344, state.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
        var weak = new WeakReference<object>(alias);
        GC.KeepAlive(alias);
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterEmptySources(ExecutionState state)
    {
        for (var index = 0; index < 2; index++)
            state.RegisterPool(new object(), new ChargeReclamationPool(state.MemoryGovernor));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> DenyWhileAliasLives(ExecutionState state)
    {
        var (alias, owner, _) = CreateRegisteredAlias(state, false, scratchBytes: 0);
        Collect();
        Assert.False(owner.TryGetTarget(out _));
        var failure = Assert.Throws<LythonRuntimeException>(() => state.LiveReclamationPools().ToList());
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(1312, state.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
        var weak = new WeakReference<object>(alias);
        GC.KeepAlive(alias);
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RetainAliasAfterOwnerDies(ExecutionState state, bool oldTier)
    {
        var (alias, owner, pool) = CreateRegisteredAlias(state, oldTier);
        Collect();
        Assert.False(owner.TryGetTarget(out _));
        var live = state.LiveReclamationPools().ToList();
        Assert.Contains(pool, live);
        Assert.Equal(2, live.Count);
        Assert.Equal(1, pool.Count);
        Assert.True(state.MemoryGovernor.CurrentCommittedBytes >= 1024 + 2 * ChargeReclamationPool.EntryChargeBytes);
        Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
        var committed = state.MemoryGovernor.CurrentCommittedBytes;
        Assert.Contains(pool, state.LiveReclamationPools());
        Assert.Equal(committed, state.MemoryGovernor.CurrentCommittedBytes);
        var weak = new WeakReference<object>(alias);
        GC.KeepAlive(alias);
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Alias, WeakReference<object> Owner, ChargeReclamationPool Pool) CreateRegisteredAlias(ExecutionState state, bool oldTier, long scratchBytes = 4096)
    {
        var owner = new object();
        var alias = new object();
        var pool = new ChargeReclamationPool(state.MemoryGovernor);
        state.MemoryGovernor.Reserve(1024, null);
        state.MemoryGovernor.Commit(1024);
        pool.Track(alias, 1024);
        if (oldTier) pool.Sweep(full: true);
        var scratch = state.MemoryGovernor.ReserveTemporary(scratchBytes, null);
        state.RegisterPool(owner, pool, scratch);
        return (alias, new WeakReference<object>(owner), pool);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
