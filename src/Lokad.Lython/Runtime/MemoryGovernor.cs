namespace Lokad.Lython.Runtime;

internal sealed class MemoryGovernor
{
    public MemoryGovernor(long? maxReservedBytes)
    {
        MaxReservedBytes = maxReservedBytes;
    }

    public long? MaxReservedBytes { get; }

    public long CurrentReservedBytes { get; private set; }

    public long PeakReservedBytes { get; private set; }

    public long CurrentCommittedBytes { get; private set; }

    public long PeakCommittedBytes { get; private set; }

    public long CurrentLiveBytes => checked(CurrentReservedBytes + CurrentCommittedBytes);

    public long PeakLiveBytes { get; private set; }

    public void EnsureCanReserve(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        var nextReserved = AddChecked(CurrentReservedBytes, bytes, span);
        var nextLive = AddChecked(nextReserved, CurrentCommittedBytes, span);
        if (MaxReservedBytes is { } maxReservedBytes && nextLive > maxReservedBytes)
        {
            throw RuntimeErrors.Memory($"execution memory budget exceeded ({maxReservedBytes})", span);
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

        var currentLive = CurrentLiveBytes;
        if (currentLive > PeakLiveBytes)
        {
            PeakLiveBytes = currentLive;
        }
    }

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

    private static long AddChecked(long left, long right, LythonSourceSpan? span)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            throw RuntimeErrors.Memory("execution memory budget exceeded", span);
        }

        return left + right;
    }
}
