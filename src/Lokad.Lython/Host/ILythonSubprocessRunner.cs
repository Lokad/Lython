namespace Lokad.Lython;

/// <summary>Optional host capability that executes fully described, contained subprocess requests.</summary>
public interface ILythonSubprocessRunner
{
    /// <summary>Gets whether this runner accepts requests with <see cref="LythonSubprocessInvocationMode.Shell"/>.</summary>
    /// <remarks>
    /// The default denies shell invocation because a real shell is broader ambient authority
    /// than direct argument-vector execution.
    /// </remarks>
    bool AllowsShellInvocation => false;

    /// <summary>
    /// Executes one request exactly once and returns its exit status and routed byte output.
    /// </summary>
    /// <remarks>
    /// Implementations must honor cancellation, enforce the request timeout and output bound,
    /// and must not use ambient Lython process state that is absent from <paramref name="request"/>.
    /// </remarks>
    ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken cancellationToken);
}
