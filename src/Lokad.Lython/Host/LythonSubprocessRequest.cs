namespace Lokad.Lython;

public enum LythonSubprocessStreamMode
{
    Inherit,
    Pipe,
    DevNull,
    StandardOutput,
}

public enum LythonSubprocessInvocationMode
{
    Direct,
    Shell,
}

public enum LythonSubprocessContentMode
{
    Binary,
    Text,
}

public enum LythonSubprocessTextEncoding
{
    Utf8,
    Utf8WithSignature,
}

public enum LythonSubprocessTextErrorMode
{
    Strict,
    Ignore,
    Replace,
    BackslashReplace,
}

public readonly record struct LythonSubprocessOutputLimit
{
    public LythonSubprocessOutputLimit(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Subprocess output limits cannot be negative.");
        }

        Bytes = bytes;
    }

    public long Bytes { get; }
}

public sealed record LythonSubprocessRequest(
    IReadOnlyList<string> Args,
    string? Cwd,
    IReadOnlyDictionary<string, string>? Environment,
    ReadOnlyMemory<byte> StandardInputUtf8,
    LythonSubprocessStreamMode StandardInput,
    LythonSubprocessStreamMode StandardOutput,
    LythonSubprocessStreamMode StandardError,
    LythonSubprocessInvocationMode InvocationMode,
    LythonSubprocessContentMode ContentMode,
    LythonSubprocessTextEncoding TextEncoding,
    LythonSubprocessTextErrorMode TextErrorMode,
    TimeSpan? Timeout,
    LythonSubprocessOutputLimit? OutputLimit);
