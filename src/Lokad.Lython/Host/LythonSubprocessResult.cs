namespace Lokad.Lython;

/// <summary>Contains the raw result returned by a host subprocess runner.</summary>
/// <param name="ReturnCode">The subprocess return code.</param>
/// <param name="StandardOutputUtf8">Captured standard-output bytes.</param>
/// <param name="StandardErrorUtf8">Captured standard-error bytes.</param>
public sealed record LythonSubprocessResult(
    int ReturnCode,
    ReadOnlyMemory<byte> StandardOutputUtf8,
    ReadOnlyMemory<byte> StandardErrorUtf8);
