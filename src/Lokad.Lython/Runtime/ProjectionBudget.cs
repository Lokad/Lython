namespace Lokad.Lython.Runtime;

internal sealed class ProjectionBudget
{
    public ProjectionBudget(long? maxBytes)
    {
        MaxBytes = maxBytes;
    }

    public long? MaxBytes { get; }

    public long CurrentBytes { get; private set; }

    // Check-first like execution accounting: a denial leaves the counter
    // (and therefore the reported peak) at accepted charges only. The
    // comparisons are arranged so the addition itself cannot overflow.
    public void Reserve(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        if (MaxBytes is { } maxBytes)
        {
            if (bytes > maxBytes || CurrentBytes > maxBytes - bytes)
            {
                throw new ProjectionException($"projection memory budget exceeded ({maxBytes})")
                    { Attribution = new(LythonFailureReason.ProjectionMemoryLimit, maxBytes, CurrentBytes, bytes) };
            }
        }
        else if (CurrentBytes > long.MaxValue - bytes)
        {
            throw new ProjectionException("projection memory budget exceeded")
                { Attribution = new(LythonFailureReason.ProjectionMemoryLimit, null, CurrentBytes, bytes) };
        }

        CurrentBytes += bytes;
    }
}
