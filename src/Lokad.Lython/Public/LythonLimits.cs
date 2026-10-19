namespace Lokad.Lython;

/// <summary>A host-configured Python call-depth limit.</summary>
public readonly record struct LythonCountLimit(int Count)
{
    /// <summary>Creates a count limit from its host-facing integer representation.</summary>
    public static implicit operator LythonCountLimit(int count) => new(count);
}
