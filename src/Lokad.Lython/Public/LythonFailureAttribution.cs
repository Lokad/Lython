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
    /// <summary>The configured Python call-depth limit was reached.</summary>
    RecursionDepthLimit,
    /// <summary>The managed stack admission check or fixed interpreter nesting bound rejected entry.</summary>
    InterpreterStackLimit,
    /// <summary>An internal byte-buffer capacity bound was exceeded.</summary>
    ByteBufferCapacityLimit,
    /// <summary>A value was cyclic or unsupported for public projection.</summary>
    ProjectionRejected,
    /// <summary>Public projection reached its fixed nesting bound or failed managed stack admission.</summary>
    ProjectionDepthLimit,
    /// <summary>The shared host allowance refused a conservative owned-storage estimate.</summary>
    SharedMemoryAllowance,
}

/// <summary>Contains fixed-size attribution captured where an execution was rejected.</summary>
/// <param name="Reason">The originating check, independent of the Python exception's name or message.</param>
/// <param name="Limit">The configured or fixed bound, or null when no numeric bound is available.</param>
/// <param name="Observed">The quantity observed by the check, when known.</param>
/// <param name="Requested">The requested operation quantity, when known.</param>
/// <remarks>
/// Quantities use the reason's units: nesting depth or bytes. Requested execution
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
