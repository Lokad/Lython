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

/// <summary>Describes a subprocess operation delegated to <see cref="ILythonSubprocessRunner"/>.</summary>
/// <remarks>
/// Runtime-created requests use contained host paths only. When guest code omits
/// <c>cwd</c> or <c>env</c>, Lython supplies the run's contained host cwd and a copy
/// of the run environment instead of relying on process ambient defaults.
/// StandardInputUtf8 carries raw bytes in binary mode, or encoded UTF-8/UTF-8-sig
/// bytes in text mode. Timeout is the guest's requested duration in seconds,
/// converted to TimeSpan; the host is responsible for enforcing that operation timeout.
/// </remarks>
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
    TimeSpan? Timeout)
{
    /// <summary>
    /// Gets the borrowed active memory budget for cooperating host commands.
    /// The dispatcher may create child scopes and must await all command users
    /// and cleanup before disposing them or completing the request.
    /// </summary>
    public ILythonMemoryBudget? MemoryBudget { get; init; }

}
