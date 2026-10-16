namespace Lokad.Lython.Runtime;

internal sealed class ProjectionBudget
{
    private readonly ILythonMemoryReservation? _ownership;

    public ProjectionBudget(long? maxBytes, ILythonMemoryBudget? owner = null)
    {
        MaxBytes = maxBytes;
        if (owner is not null && !owner.TryReserve(0, out _ownership))
            throw new ProjectionException("shared memory budget refused projection ownership")
                { Attribution = new(LythonFailureReason.SharedMemoryAllowance, Observed: 0, Requested: 0) };
    }

    public long? MaxBytes { get; }

    public long CurrentBytes { get; private set; }

    internal ProjectionException? AdmissionFailure { get; private set; }

    // Check-first like execution accounting: a denial leaves the counter
    // (and therefore the reported peak) at accepted charges only. The
    // comparisons are arranged so the addition itself cannot overflow.
    public void Reserve(long bytes)
    {
        if (AdmissionFailure is { } failure) throw failure;
        if (bytes <= 0)
        {
            return;
        }

        if (MaxBytes is { } maxBytes)
        {
            if (bytes > maxBytes || CurrentBytes > maxBytes - bytes)
            {
                throw AdmissionFailure = new ProjectionException($"projection memory budget exceeded ({maxBytes})")
                    { Attribution = new(LythonFailureReason.ProjectionMemoryLimit, maxBytes, CurrentBytes, bytes) };
            }
        }
        else if (CurrentBytes > long.MaxValue - bytes)
        {
            throw AdmissionFailure = new ProjectionException("projection memory budget exceeded")
                { Attribution = new(LythonFailureReason.ProjectionMemoryLimit, null, CurrentBytes, bytes) };
        }

        if (_ownership is not null && !_ownership.TryResize(checked(CurrentBytes + bytes)))
            throw AdmissionFailure = new ProjectionException("shared memory budget exceeded during projection")
                { Attribution = new(LythonFailureReason.SharedMemoryAllowance, Observed: CurrentBytes, Requested: bytes) };
        CurrentBytes += bytes;
    }
}
