namespace Lokad.Lython.Runtime;

internal sealed class LegacyApproximateMemoryDiagnostics
{
    public LegacyApproximateMemoryDiagnostics(long? maxBytes)
    {
        MaxBytes = maxBytes;
    }

    public long? MaxBytes { get; }

    public long CurrentBytes { get; private set; }

    public long PeakBytes { get; private set; }

    public void TrackBytes(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        var next = checked(CurrentBytes + bytes);
        if (MaxBytes is { } maxBytes && next > maxBytes)
        {
            throw RuntimeErrors.Runtime($"execution memory budget exceeded ({maxBytes})", span);
        }

        CurrentBytes = next;
        if (CurrentBytes > PeakBytes)
        {
            PeakBytes = CurrentBytes;
        }
    }
}
