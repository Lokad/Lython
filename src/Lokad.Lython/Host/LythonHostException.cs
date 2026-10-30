namespace Lokad.Lython;

/// <summary>Identifies a contained host failure without inspecting exception messages.</summary>
public enum LythonHostFailureKind
{
    NotFound,
    PermissionDenied,
    IsDirectory,
    NotDirectory,
    AlreadyExists,
    UnsupportedOperation,
    BrokenPipe,
    Transport,
    MemoryAdmission,
}

/// <summary>Reports a host failure with a diagnostic safe to expose to guest Python.</summary>
/// <remarks>
/// Use MemoryAdmission immediately after a refused reservation, carrying its first
/// requested estimate. Lython records that terminal outcome; the host needs no
/// separate sticky error. Finish outstanding users and cleanup before throwing.
/// Cancellation uses OperationCanceledException separately. Unknown CLR exceptions
/// retain Lython's generic, redacted host-error behavior.
/// </remarks>
public sealed class LythonHostException : Exception
{
    /// <summary>Creates a structured failure with safe contained diagnostic text.</summary>
    public LythonHostException(LythonHostFailureKind kind, string message,
        Exception? innerException = null, long? requestedBytes = null)
        : base(message, innerException)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (requestedBytes is < 0) throw new ArgumentOutOfRangeException(nameof(requestedBytes));
        Kind = kind;
        RequestedBytes = requestedBytes;
    }

    /// <summary>Gets the host's failure category.</summary>
    public LythonHostFailureKind Kind { get; }

    /// <summary>Gets the first refused estimate, when known, for MemoryAdmission.</summary>
    public long? RequestedBytes { get; }
}
