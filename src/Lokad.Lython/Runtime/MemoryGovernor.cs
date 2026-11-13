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
    private WorkspaceCache<object> _functionWorkspace;
    private Text.PyStringOps.CasedWhitespaceIteration? _idleWords;
    private bool _wordReuseEnded;
    private bool _resizingOwnership;

    internal static long WorkspaceBytes(int capacity) => capacity == 0 ? 0 : 64L + 16L * capacity;

    internal object[] RentFunctionWorkspace(int capacity, LythonSourceSpan span)
        => _functionWorkspace.Rent(this, capacity, span);

    internal void ReturnFunctionWorkspace(object[] values, int usedLength)
        => _functionWorkspace.Return(this, values, usedLength);

    // The range array and its private iteration state share one existing loan.
    // Reusing a loan retains its full capacity but clears every guest reference.
    internal Text.PyStringOps.CasedWhitespaceIteration RentWordRanges(int capacity, LythonSourceSpan? span)
    {
        ThrowIfAdmissionFailed();
        var cached = _idleWords;
        _idleWords = null;
        if (cached is not null)
        {
            if (cached.Ranges.Length >= capacity) { cached.BeginLoan(); return cached; }
            Release(cached.StorageBytes);
        }
        var bytes = Text.PyStringOps.CasedWhitespaceIteration.EstimateStorageBytes(capacity);
        Reserve(bytes, span);
        try
        {
            var words = new Text.PyStringOps.CasedWhitespaceIteration(this, capacity);
            Commit(bytes);
            return words;
        }
        catch { ReleaseReserved(bytes); throw; }
    }

    internal void ReturnWordRanges(Text.PyStringOps.CasedWhitespaceIteration words, int usedLength)
    {
        if (!words.EndLoan(usedLength)) return;
        var cached = _idleWords;
        if (!_wordReuseEnded && (cached is null || cached.Ranges.Length < words.Ranges.Length))
        {
            _idleWords = words;
            if (cached is not null) Release(cached.StorageBytes);
        }
        else Release(words.StorageBytes);
    }

    internal void EndWorkspaceReuse()
    {
        try { _functionWorkspace.End(this); }
        finally
        {
            _wordReuseEnded = true;
            var cached = _idleWords;
            _idleWords = null;
            if (cached is not null) Release(cached.StorageBytes);
        }
    }

    // Invocation workspaces use the same one-idle-buffer lifetime policy as the
    // word loan above. Active loans stay charged after idle reuse has ended.
    private struct WorkspaceCache<T>
    {
        private T[]? _idle;
        private bool _ended;

        internal T[] Rent(MemoryGovernor governor, int capacity, LythonSourceSpan? span)
        {
            governor.ThrowIfAdmissionFailed();
            if (capacity == 0) return [];
            var cached = _idle;
            _idle = null;
            if (cached is not null)
            {
                if (cached.Length >= capacity) return cached;
                // Normal idle eviction, never a retry after refused admission.
                governor.Release(WorkspaceBytes(cached.Length));
            }
            var bytes = WorkspaceBytes(capacity);
            governor.Reserve(bytes, span);
            try
            {
                var values = new T[capacity];
                governor.Commit(bytes);
                return values;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }

        internal void Return(MemoryGovernor governor, T[] values, int usedLength)
        {
            // Every prior loan cleared its logical extent. Unused capacity is
            // already empty; do not scan a larger retained backing array.
            Array.Clear(values, 0, usedLength);
            var cached = _idle;
            if (!_ended && values.Length != 0 &&
                (cached is null || cached.Length < values.Length))
            {
                // Publish the surviving owner before a host refund can reenter.
                _idle = values;
                if (cached is not null) governor.Release(WorkspaceBytes(cached.Length));
            }
            else governor.Release(WorkspaceBytes(values.Length));
        }

        internal void End(MemoryGovernor governor)
        {
            _ended = true;
            if (_idle is not { } cached) return;
            _idle = null;
            governor.Release(WorkspaceBytes(cached.Length));
        }
    }

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

    internal LythonRuntimeException RefuseAdmission(string message, long? requestedBytes, LythonSourceSpan? span, Exception? cause = null)
    {
        if (AdmissionFailure is { } first) return first;
        LastDeniedReservationBytes = requestedBytes ?? 0;
        return AdmissionFailure = new LythonRuntimeException("MemoryError", message, span, cause)
        {
            Attribution = new(LythonFailureReason.SharedMemoryAllowance, Observed: CurrentAccountedBytes, Requested: requestedBytes)
        };
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

        // A callback may allocate after the host has published the outer charge.
        // Before that point, same-governor reentry cannot borrow unapproved bytes.
        if (_resizingOwnership && _ownership.Bytes != CurrentAccountedBytes)
            throw RefuseAdmission("memory admission re-entered before pending ownership was published", bytes, span);

        var observed = CurrentAccountedBytes;
        var next = AddChecked(observed, bytes, span);
        // Publish pending ownership before external code. Callback allocations
        // and refunds must include this operation when resizing the shared lease.
        CurrentReservedBytes += bytes;
        try
        {
            var admitted = ResizeOwnership(next);
            ThrowIfAdmissionFailed();
            if (!admitted)
            {
                LastDeniedReservationBytes = bytes;
                throw AdmissionFailure = RuntimeErrors.Attributed("MemoryError", "shared memory budget exceeded", span,
                    LythonFailureReason.SharedMemoryAllowance, observed: observed, requested: bytes);
            }
        }
        catch
        {
            CurrentReservedBytes = Math.Max(0, CurrentReservedBytes - bytes);
            // A callback can fail before changing the host estimate. Do not
            // invoke arbitrary host code again for a refund that changes nothing.
            if (_ownership.Bytes != CurrentAccountedBytes)
                ResizeOwnership(CurrentAccountedBytes);
            throw;
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

        var released = Math.Min(bytes, CurrentCommittedBytes);
        if (released == 0) return;
        CurrentCommittedBytes -= released;
        ResizeOwnership(CurrentAccountedBytes);
    }

    internal void ReleaseReserved(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        var released = Math.Min(bytes, CurrentReservedBytes);
        if (released == 0) return;
        CurrentReservedBytes -= released;
        ResizeOwnership(CurrentAccountedBytes);
    }

    // Every host resize sees its pending logical change. Reentry is allowed
    // once that change is visible through the borrowed reservation's estimate.
    private bool ResizeOwnership(long bytes)
    {
        var wasResizing = _resizingOwnership;
        _resizingOwnership = true;
        try { return _ownership.TryResize(bytes); }
        finally { _resizingOwnership = wasResizing; }
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
            // Admission can dispose this owner. Its old balance has already
            // been returned, but these newly admitted bytes have no user yet.
            if (_governor is null)
            {
                governor.ReleaseReserved(bytes);
                governor.ThrowIfAdmissionFailed();
                throw new ObjectDisposedException(nameof(TemporaryMemoryReservation));
            }
            _reservedBytes = governor.AddChecked(_reservedBytes, bytes, span);
        }

        public void Shrink(long bytes)
        {
            if (bytes <= 0) return;
            var governor = _governor ?? throw new ObjectDisposedException(nameof(TemporaryMemoryReservation));
            if (bytes > _reservedBytes)
                throw new InvalidOperationException("Cannot release another reservation's funding.");
            _reservedBytes -= bytes;
            governor.ReleaseReserved(bytes);
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

            var bytes = _reservedBytes;
            _reservedBytes = 0;
            governor.ReleaseReserved(bytes);
        }
    }
}
