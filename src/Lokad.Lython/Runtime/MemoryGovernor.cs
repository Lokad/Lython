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
            throw RuntimeErrors.Memory($"execution memory budget exceeded ({maxAccountedBytes})", span);
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

    public void Commit(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        var committed = Math.Min(bytes, CurrentReservedBytes);
        CurrentReservedBytes -= committed;
        CurrentCommittedBytes = checked(CurrentCommittedBytes + committed);

        if (CurrentCommittedBytes > PeakCommittedBytes)
        {
            PeakCommittedBytes = CurrentCommittedBytes;
        }
    }

    public void Release(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

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
