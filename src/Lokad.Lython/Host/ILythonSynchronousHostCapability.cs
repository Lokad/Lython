namespace Lokad.Lython;

/// <summary>
/// Opt-in contract for host capability objects that can be called by synchronous Lython execution.
/// </summary>
/// <remarks>
/// When <see cref="CompletesSynchronously"/> is <see langword="true"/>, every
/// <see cref="ValueTask"/> returned by the capability must already be complete.
/// Lython checks the advertised capability before invoking an operation. If the
/// host breaks its promise, Lython joins the started operation before reporting
/// the contract failure and releasing borrowed storage. A host that never settles
/// or honors cancellation can therefore delay synchronous failure cleanup.
/// </remarks>
public interface ILythonSynchronousHostCapability
{
    /// <summary>
    /// Gets whether every operation exposed by this capability completes before returning.
    /// </summary>
    bool CompletesSynchronously { get; }
}
