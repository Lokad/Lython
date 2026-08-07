namespace Lokad.Lython;

/// <summary>A host-configured limit measured as a count of runtime operations or values.</summary>
public readonly record struct LythonCountLimit(int Count)
{
    /// <summary>Creates a count limit from its host-facing integer representation.</summary>
    public static implicit operator LythonCountLimit(int count) => new(count);
}

/// <summary>A host-configured limit measured in bytes.</summary>
public readonly record struct LythonByteLimit(long Bytes)
{
    /// <summary>Creates a byte limit from its host-facing integer representation.</summary>
    public static implicit operator LythonByteLimit(long bytes) => new(bytes);
}
