namespace Lokad.Lython;

/// <summary>Identifies the origin of an unsuccessful execution without parsing exception text.</summary>
public enum LythonFailureReason
{
    /// <summary>A Python-visible program error, including an explicitly raised exception.</summary>
    GuestException,
    /// <summary>Compilation rejected the source.</summary>
    Compilation,
    /// <summary>The host did not supply a required capability.</summary>
    HostCapabilityUnavailable,
    /// <summary>A supplied host operation failed or violated its synchronous contract.</summary>
    HostOperationFailed,
    /// <summary>The invocation's built-in module policy denied access.</summary>
    ModulePolicyDenied,
    /// <summary>A cancellation checkpoint or host operation observed cancellation.</summary>
    Cancelled,
    /// <summary>The configured interpreter-checkpoint allowance was exhausted.</summary>
    ExecutionStepLimit,
    /// <summary>The configured Python call-depth limit was reached.</summary>
    RecursionDepthLimit,
    /// <summary>The managed stack admission check or fixed interpreter nesting bound rejected entry.</summary>
    InterpreterStackLimit,
    /// <summary>The configured host-call allowance was exhausted.</summary>
    HostCallLimit,
    /// <summary>The configured item count for a collection was exceeded.</summary>
    CollectionSizeLimit,
    /// <summary>The configured Python string length was exceeded.</summary>
    StringLengthLimit,
    /// <summary>The configured host-read byte count was exceeded.</summary>
    HostReadLimit,
    /// <summary>The configured captured standard-output byte count was exceeded.</summary>
    StandardOutputLimit,
    /// <summary>The configured captured standard-error byte count was exceeded.</summary>
    StandardErrorLimit,
    /// <summary>The conservative live execution-memory budget was exceeded.</summary>
    ExecutionMemoryLimit,
    /// <summary>The separate public-projection memory budget was exceeded.</summary>
    ProjectionMemoryLimit,
    /// <summary>An internal byte-buffer capacity bound was exceeded.</summary>
    ByteBufferCapacityLimit,
    /// <summary>The byte-oriented Popen input buffer exceeded its configured string allowance.</summary>
    SubprocessInputLimit,
    /// <summary>A host-mediated subprocess exceeded its request's captured-output byte allowance.</summary>
    SubprocessOutputLimit,
    /// <summary>Cumulative Popen pipeline output exceeded its configured byte-oriented string allowance.</summary>
    SubprocessPipelineLimit,
    /// <summary>A value was cyclic or unsupported for public projection.</summary>
    ProjectionRejected,
    /// <summary>Public projection reached its fixed nesting bound or failed managed stack admission.</summary>
    ProjectionDepthLimit,
}

/// <summary>Contains fixed-size attribution captured where an execution was rejected.</summary>
/// <param name="Reason">The originating check, independent of the Python exception's name or message.</param>
/// <param name="Limit">The configured or fixed bound, or null when no numeric bound is available.</param>
/// <param name="Observed">The quantity observed by the check, when known.</param>
/// <param name="Requested">The requested operation quantity, when known.</param>
/// <remarks>
/// Quantities use the reason's units: checkpoints, calls, nesting depth,
/// collection items, Python string characters, or bytes. Requested execution
/// memory is the reservation size; observed memory is accepted live accounting,
/// not CLR heap size. Unavailable quantities, including values outside Int64,
/// remain null. This describes the
/// terminal failure, not a history of caught denials. Guest code cannot create
/// or modify attribution, even when it changes a caught exception's args.
/// </remarks>
public readonly record struct LythonFailureAttribution(
    LythonFailureReason Reason,
    long? Limit = null,
    long? Observed = null,
    long? Requested = null);
