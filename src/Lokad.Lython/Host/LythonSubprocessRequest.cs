namespace Lokad.Lython;

public enum LythonSubprocessStreamMode
{
    Inherit,
    Pipe,
    DevNull,
    StandardOutput,
}

public sealed record LythonSubprocessRequest(
    IReadOnlyList<string> Args,
    string? Cwd,
    IReadOnlyDictionary<string, string>? Environment,
    ReadOnlyMemory<byte> StandardInputUtf8,
    LythonSubprocessStreamMode StandardInput,
    LythonSubprocessStreamMode StandardOutput,
    LythonSubprocessStreamMode StandardError,
    bool UseShell,
    bool TextMode,
    string? Encoding,
    string? Errors,
    int? TimeoutMilliseconds,
    long? MaxOutputBytes);
