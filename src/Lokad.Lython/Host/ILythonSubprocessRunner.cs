namespace Lokad.Lython;

/// <summary>Optional host capability that executes fully described, contained subprocess requests.</summary>
public interface ILythonSubprocessRunner
{
    /// <summary>
    /// Executes one request exactly once and returns its exit status and routed byte output.
    /// </summary>
    /// <remarks>
    /// Implementations must honor cancellation, enforce the request timeout and output bound,
    /// and must not use ambient Lython process state that is absent from <paramref name="request"/>.
    /// </remarks>
    ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken cancellationToken);
}
