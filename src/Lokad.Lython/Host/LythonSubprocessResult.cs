namespace Lokad.Lython;

public sealed record LythonSubprocessResult(
    int ReturnCode,
    ReadOnlyMemory<byte> StandardOutputUtf8,
    ReadOnlyMemory<byte> StandardErrorUtf8);
