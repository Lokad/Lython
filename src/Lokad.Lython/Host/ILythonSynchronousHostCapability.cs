namespace Lokad.Lython;

/// <summary>
/// Opt-in contract for host capability objects that can be called by synchronous Lython execution.
/// </summary>
/// <remarks>
/// When <see cref="CompletesSynchronously"/> is <see langword="true"/>, every
/// <see cref="ValueTask"/> returned by the capability must already be complete.
/// Lython checks this contract before invoking a host operation, so a synchronous
/// run never starts an operation that may continue after the run has failed.
/// </remarks>
public interface ILythonSynchronousHostCapability
{
    /// <summary>
    /// Gets whether every operation exposed by this capability completes before returning.
    /// </summary>
    bool CompletesSynchronously { get; }
}
