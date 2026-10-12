using System;

namespace Lokad.Lython.Runtime;

internal sealed class MemoryGovernor
{
    // Callers reserve before allocating, commit after ownership transfers to a
    // governed value, and release when that value discards its backing storage.
    // Reserved and committed bytes are both accounted so allocation cannot pass
    // through an uncharged window; estimates intentionally need only be safe.
    public MemoryGovernor(long? maxAccountedBytes)
    {
        MaxAccountedBytes = maxAccountedBytes;
    }

    public long? MaxAccountedBytes { get; }

    public long CurrentReservedBytes { get; private set; }

    public long PeakReservedBytes { get; private set; }

    public long CurrentCommittedBytes { get; private set; }

    public long PeakCommittedBytes { get; private set; }

    public long CurrentAccountedBytes => checked(CurrentReservedBytes + CurrentCommittedBytes);

    public long PeakAccountedBytes { get; private set; }

    // MG24: the most recent denied reservation size, for failure attribution.
    // Stays zero when nothing was ever denied; a later denial overwrites it.
    public long LastDeniedReservationBytes { get; private set; }

    // The run's one storage pool. Untracked standalone charges still deny
    // without forcing a collection that cannot release accounted storage.
    internal ChargeReclamationPool? ReclamationPool { get; set; }

    // Successful reservation or commit since the last relief. Scratch can
    // reserve before a following payload commits; both need an opportunity to
    // collect the garbage a partial drain left behind. Net committed
    // bytes may stay flat or fall while replacements allocate and old values
    // become collectible. Repeated pinned denials without new allocation still
    // fail fast instead of paying a collection per caught trip.
    private bool _hasAllocatedSinceReclaim;

    public void EnsureCanReserve(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        var nextAccounted = AddChecked(CurrentAccountedBytes, bytes, span);
        if (MaxAccountedBytes is { } maxAccountedBytes && nextAccounted > maxAccountedBytes)
        {
            if (bytes > maxAccountedBytes)
            {
                // The request alone exceeds the budget: no relief could fit
                // it, so deny without pausing the process for a collection.
                LastDeniedReservationBytes = bytes;
                throw RuntimeErrors.Attributed("MemoryError", $"execution memory budget exceeded ({maxAccountedBytes})", span,
                    LythonFailureReason.ExecutionMemoryLimit, maxAccountedBytes, CurrentAccountedBytes, bytes);
            }

            if (_hasAllocatedSinceReclaim)
            {
                // Sweep/collection makes no admissions or guest callbacks.
                // A failed pending allocation does not authorize another retry.
                _hasAllocatedSinceReclaim = false;
                ReclaimForExhaustion(nextAccounted - maxAccountedBytes);
            }
            nextAccounted = AddChecked(CurrentAccountedBytes, bytes, span);
            if (nextAccounted > maxAccountedBytes)
            {
                LastDeniedReservationBytes = bytes;
                throw RuntimeErrors.Attributed("MemoryError", $"execution memory budget exceeded ({maxAccountedBytes})", span,
                    LythonFailureReason.ExecutionMemoryLimit, maxAccountedBytes, CurrentAccountedBytes, bytes);
            }
        }
    }

    // Last-resort relief for a denied reservation: unreachable tracked values
    // cannot release their charges until collected, and a quiet loop may never
    // trigger a collection before the budget trips. Collecting and sweeping
    // fully before failing attributes the denial to live retention rather
    // than garbage pressure for tracked storage. Untracked retained charges
    // still deny without relief.
    private void ReclaimForExhaustion(long shortfallBytes)
    {
        if (ReclamationPool is not { Count: > 0 } pool)
        {
            return;
        }

        // Release already-collected targets first: when bursty drops pile up
        // faster than collections run, that alone can cover the shortfall and
        // skip the process-wide pause. The net committed comparison keeps this
        // exact; sweeps themselves allocate nothing and require no funding.
        var committedBefore = CurrentCommittedBytes;
        pool.Sweep(full: true);

        if (committedBefore - CurrentCommittedBytes >= shortfallBytes)
        {
            return;
        }

        // Most dropped streaming values are young. Collect only as far as
        // needed to fund this reservation; old unreachable storage still gets
        // a full collection before denial. CLR generations need no owner-age
        // registry, and all existing live snapshots remain charged.
        for (var generation = 0; generation <= GC.MaxGeneration; generation++)
        {
            GC.Collect(generation);
            pool.Sweep(full: true);

            if (committedBefore - CurrentCommittedBytes >= shortfallBytes)
            {
                break;
            }
        }
    }

    public void Reserve(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        EnsureCanReserve(bytes, span);

        CurrentReservedBytes += bytes;
        _hasAllocatedSinceReclaim = true;
        if (CurrentReservedBytes > PeakReservedBytes)
        {
            PeakReservedBytes = CurrentReservedBytes;
        }

        var currentAccounted = CurrentAccountedBytes;
        if (currentAccounted > PeakAccountedBytes)
        {
            PeakAccountedBytes = currentAccounted;
        }
    }

    public TemporaryMemoryReservation ReserveTemporary(long bytes, LythonSourceSpan? span)
        => new(this, bytes, span);

    /// <summary>
    /// Moves reserved bytes to committed ownership, capping at what is reserved.
    /// The cap is deliberate tolerance: independent release paths cannot drive
    /// accounting negative, but callers must still pair every reserve with a
    /// matching commit or release. Debug builds throw on unpaired use.
    /// </summary>
    // Publishing metadata whose fee was funded earlier is still allocation
    // progress, even though ownership transfer changes no accounting total.
    // Give a later denial the same reclamation opportunity as an ordinary
    // successful commit; repeated denials without progress still fail fast.
    internal void NotePrefundedAllocation() => _hasAllocatedSinceReclaim = true;

    public void Commit(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

#if DEBUG
        if (bytes > CurrentReservedBytes)
        {
            throw new InvalidOperationException($"MemoryGovernor.Commit({bytes}) exceeds {CurrentReservedBytes} reserved bytes.");
        }
#endif

        var committed = Math.Min(bytes, CurrentReservedBytes);
        CurrentReservedBytes -= committed;
        CurrentCommittedBytes = checked(CurrentCommittedBytes + committed);
        if (committed > 0) _hasAllocatedSinceReclaim = true;

        if (CurrentCommittedBytes > PeakCommittedBytes)
        {
            PeakCommittedBytes = CurrentCommittedBytes;
        }
    }

    /// <summary>
    /// Returns committed bytes, flooring at zero for the same defensive reason
    /// as <see cref="Commit"/>: over-release is absorbed, never an exception.
    /// Debug builds throw on unpaired use.
    /// </summary>
    public void Release(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

#if DEBUG
        if (bytes > CurrentCommittedBytes)
        {
            throw new InvalidOperationException($"MemoryGovernor.Release({bytes}) exceeds {CurrentCommittedBytes} committed bytes.");
        }
#endif

        CurrentCommittedBytes = Math.Max(0, CurrentCommittedBytes - bytes);
    }

    internal void ReleaseReserved(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        CurrentReservedBytes = Math.Max(0, CurrentReservedBytes - bytes);
    }

    private long AddChecked(long left, long right, LythonSourceSpan? span)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            throw RuntimeErrors.Attributed("MemoryError", "execution memory budget exceeded", span,
                LythonFailureReason.ExecutionMemoryLimit, MaxAccountedBytes, CurrentAccountedBytes, right);
        }

        return left + right;
    }

    internal sealed class TemporaryMemoryReservation : IDisposable
    {
        private MemoryGovernor? _governor;
        private long _reservedBytes;

        internal TemporaryMemoryReservation(MemoryGovernor governor, long bytes, LythonSourceSpan? span)
        {
            _governor = governor;
            Grow(bytes, span);
        }

        public void Grow(long bytes, LythonSourceSpan? span)
        {
            if (bytes <= 0)
            {
                return;
            }

            var governor = _governor ?? throw new ObjectDisposedException(nameof(TemporaryMemoryReservation));
            governor.Reserve(bytes, span);
            _reservedBytes = governor.AddChecked(_reservedBytes, bytes, span);
        }

        public void Shrink(long bytes)
        {
            if (bytes <= 0) return;
            var governor = _governor ?? throw new ObjectDisposedException(nameof(TemporaryMemoryReservation));
            if (bytes > _reservedBytes)
                throw new InvalidOperationException("Cannot release another reservation's funding.");
            governor.ReleaseReserved(bytes);
            _reservedBytes -= bytes;
        }

        // Transfer only this reservation's funding to its final storage owner.
        // Dispose then refunds remaining scratch, never another lease's bytes.
        public void Commit(long bytes)
        {
            var governor = _governor ?? throw new ObjectDisposedException(nameof(TemporaryMemoryReservation));
            if (bytes < 0 || bytes > _reservedBytes)
                throw new InvalidOperationException("Cannot commit another reservation's funding.");
            governor.Commit(bytes);
            _reservedBytes -= bytes;
        }

        public void Dispose()
        {
            var governor = Interlocked.Exchange(ref _governor, null);
            if (governor is null)
            {
                return;
            }

            governor.ReleaseReserved(_reservedBytes);
            _reservedBytes = 0;
        }
    }
}
