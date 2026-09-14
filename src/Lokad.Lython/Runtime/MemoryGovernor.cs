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

    // Pools whose tracked charges may release once unreachable. On a denied
    // reservation the governor may force a collection, sweep them fully, and
    // retries once, so garbage pressure fails only when retention is real.
    private readonly List<ChargeReclamationPool> _reclamationPools = new();

    internal void RegisterReclamationPool(ChargeReclamationPool pool) => _reclamationPools.Add(pool);
    // Committed level after the last relief: relief repeats only while
    // retention keeps growing, so pinned workloads fail fast instead of
    // paying a collection per caught trip.
    private long _committedAtLastReclaim;

    public void EnsureCanReserve(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        var nextReserved = AddChecked(CurrentReservedBytes, bytes, span);
        var nextAccounted = AddChecked(nextReserved, CurrentCommittedBytes, span);
        if (MaxAccountedBytes is { } maxAccountedBytes && nextAccounted > maxAccountedBytes)
        {
            if (CurrentCommittedBytes > _committedAtLastReclaim)
            {
                ReclaimForExhaustion();
                _committedAtLastReclaim = CurrentCommittedBytes;
            }
            nextReserved = AddChecked(CurrentReservedBytes, bytes, span);
            nextAccounted = AddChecked(nextReserved, CurrentCommittedBytes, span);
            if (nextAccounted > maxAccountedBytes)
            {
                LastDeniedReservationBytes = bytes;
                throw RuntimeErrors.Memory($"execution memory budget exceeded ({maxAccountedBytes})", span);
            }
        }
    }

    // Last-resort relief for a denied reservation: unreachable values
    // cannot release their tracked charges until collected, and a quiet
    // loop may never trigger a collection before the budget trips.
    // Collecting once and sweeping fully before failing proves the
    // denial against live retention instead of garbage pressure.
    // Sweeps never reserve, so this cannot recurse.
    private void ReclaimForExhaustion()
    {
        if (_reclamationPools.Count == 0)
        {
            return;
        }

        GC.Collect();
        foreach (var pool in _reclamationPools)
        {
            pool.Sweep(full: true);
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

    private void ReleaseReserved(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        CurrentReservedBytes = Math.Max(0, CurrentReservedBytes - bytes);
    }

    private static long AddChecked(long left, long right, LythonSourceSpan? span)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            throw RuntimeErrors.Memory("execution memory budget exceeded", span);
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
            _reservedBytes = AddChecked(_reservedBytes, bytes, span);
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
