namespace Lokad.Lython;

public sealed record LythonSubprocessRequest(
    IReadOnlyList<string> Args,
    string? Cwd,
    IReadOnlyDictionary<string, string>? Environment,
    ReadOnlyMemory<byte> StandardInputUtf8,
    int? TimeoutMilliseconds,
    long? MaxOutputBytes);
