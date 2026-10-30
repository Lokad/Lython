namespace Lokad.Lython.Runtime;

internal sealed class ProjectionBudget
{
    private readonly ILythonMemoryReservation? _ownership;
    private readonly CancellationToken _cancellationToken;

    public ProjectionBudget(ILythonMemoryBudget? owner, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        if (owner is not null && !owner.TryReserve(0, out _ownership))
            throw new ProjectionException("shared memory budget refused projection ownership")
                { Attribution = new(LythonFailureReason.SharedMemoryAllowance, Observed: 0, Requested: 0) };
    }

    public long CurrentBytes { get; private set; }

    internal ProjectionException? AdmissionFailure { get; private set; }

    internal void CheckCancellation()
    {
        if (AdmissionFailure is { } failure) throw failure;
        if (_cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(null);
    }

    // Check-first like execution accounting: a denial leaves the counter
    // (and therefore the reported peak) at accepted charges only. The
    // comparisons are arranged so the addition itself cannot overflow.
    public void Reserve(long bytes)
    {
        CheckCancellation();
        if (bytes <= 0)
        {
            return;
        }

        if (CurrentBytes > long.MaxValue - bytes)
        {
            throw AdmissionFailure = new ProjectionException("projection size exceeds supported accounting capacity");
        }

        if (_ownership is not null && !_ownership.TryResize(checked(CurrentBytes + bytes)))
            throw AdmissionFailure = new ProjectionException("shared memory budget exceeded during projection")
                { Attribution = new(LythonFailureReason.SharedMemoryAllowance, Observed: CurrentBytes, Requested: bytes) };
        CurrentBytes += bytes;
        CheckCancellation();
    }
}
