namespace Lokad.Lython.Runtime;

internal sealed class ProjectionBudget
{
    public ProjectionBudget(long? maxBytes)
    {
        MaxBytes = maxBytes;
    }

    public long? MaxBytes { get; }

    public long CurrentBytes { get; private set; }

    public void Reserve(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        checked
        {
            CurrentBytes += bytes;
        }

        if (MaxBytes is { } maxBytes && CurrentBytes > maxBytes)
        {
            throw new ProjectionException($"projection memory budget exceeded ({maxBytes})");
        }
    }
}
