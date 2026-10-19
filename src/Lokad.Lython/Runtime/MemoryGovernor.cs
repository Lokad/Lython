using System;

namespace Lokad.Lython.Runtime;

internal sealed class MemoryGovernor
{
    // Callers reserve before allocating, commit after ownership transfers to a
    // governed value, and release when that value discards its backing storage.
    // Reserved and committed bytes are both accounted so allocation cannot pass
    // through an uncharged window; estimates intentionally need only be safe.
    private readonly ILythonMemoryReservation _ownership;
    internal ILythonMemoryBudget Budget { get; }

    public MemoryGovernor(ILythonMemoryBudget? budget)
    {
        Budget = budget ?? new LythonMemoryBudget();
        if (!Budget.TryReserve(0, out var ownership))
            throw RuntimeErrors.Attributed("MemoryError", "shared memory budget refused ownership", null,
                LythonFailureReason.SharedMemoryAllowance);
        _ownership = ownership;
    }

    public long CurrentReservedBytes { get; private set; }

    public long CurrentCommittedBytes { get; private set; }

    public long CurrentAccountedBytes => checked(CurrentReservedBytes + CurrentCommittedBytes);

    public long PeakAccountedBytes { get; private set; }

    // The first denied reservation size, preserved for terminal attribution.
    // Stays zero when no tracked reservation was denied.
    public long LastDeniedReservationBytes { get; private set; }

    // A command cannot recover from refused admission. Keep the first origin
    // even if guest exception handling or cleanup replaces the thrown error.
    internal LythonRuntimeException? AdmissionFailure { get; private set; }

    internal LythonRuntimeException RefuseAdmission(string message, long? requestedBytes, LythonSourceSpan? span)
    {
        if (AdmissionFailure is { } first) return first;
        LastDeniedReservationBytes = requestedBytes ?? 0;
        return AdmissionFailure = RuntimeErrors.Attributed("MemoryError", message, span,
            LythonFailureReason.SharedMemoryAllowance, observed: CurrentAccountedBytes, requested: requestedBytes);
    }

    internal void ThrowIfAdmissionFailed()
    {
        if (AdmissionFailure is { } failure) throw failure;
    }

    // Only fresh, unpublished transaction values can be refunded here.
    // Published storage remains conservatively owned by its execution batch.
    internal void RefundUnpublishedValue(object value)
    {
        if (value is IPyOwnershipSnapshot ownership && ownership.TrySnapshotOwnership(out var bytes)) Release(bytes);
    }

    public void Reserve(long bytes, LythonSourceSpan? span)
    {
        ThrowIfAdmissionFailed();
        if (bytes <= 0)
        {
            return;
        }

        var next = AddChecked(CurrentAccountedBytes, bytes, span);
        if (!_ownership.TryResize(next))
        {
            LastDeniedReservationBytes = bytes;
            throw AdmissionFailure = RuntimeErrors.Attributed("MemoryError", "shared memory budget exceeded", span,
                LythonFailureReason.SharedMemoryAllowance, observed: CurrentAccountedBytes, requested: bytes);
        }

        CurrentReservedBytes += bytes;
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
        _ownership.TryResize(CurrentAccountedBytes);
    }

    internal void ReleaseReserved(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        CurrentReservedBytes = Math.Max(0, CurrentReservedBytes - bytes);
        _ownership.TryResize(CurrentAccountedBytes);
    }

    private long AddChecked(long left, long right, LythonSourceSpan? span)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            LastDeniedReservationBytes = right;
            throw AdmissionFailure = RuntimeErrors.Attributed("MemoryError", "accounted memory size exceeds supported capacity", span,
                LythonFailureReason.ByteBufferCapacityLimit, observed: CurrentAccountedBytes, requested: right);
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
