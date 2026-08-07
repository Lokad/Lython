namespace Lokad.Lython;

/// <summary>Specifies how one subprocess standard stream is connected.</summary>
public enum LythonSubprocessStreamMode
{
    Inherit,
    Pipe,
    DevNull,
    StandardOutput,
}

/// <summary>Specifies whether a host invokes an argument vector directly or through a shell.</summary>
public enum LythonSubprocessInvocationMode
{
    Direct,
    Shell,
}

/// <summary>Specifies whether subprocess streams contain bytes or decoded text.</summary>
public enum LythonSubprocessContentMode
{
    Binary,
    Text,
}

/// <summary>Specifies the UTF-8 variant used for text subprocess streams.</summary>
public enum LythonSubprocessTextEncoding
{
    Utf8,
    Utf8WithSignature,
}

/// <summary>Specifies how invalid subprocess text is decoded.</summary>
public enum LythonSubprocessTextErrorMode
{
    Strict,
    Ignore,
    Replace,
    BackslashReplace,
}

/// <summary>Represents the maximum combined buffered subprocess output in bytes.</summary>
public readonly record struct LythonSubprocessOutputLimit
{
    /// <summary>Creates a validated output limit.</summary>
    public LythonSubprocessOutputLimit(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Subprocess output limits cannot be negative.");
        }

        Bytes = bytes;
    }

    /// <summary>Gets the limit in bytes.</summary>
    public long Bytes { get; }
}

/// <summary>Describes a subprocess operation delegated to <see cref="ILythonSubprocessRunner"/>.</summary>
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
