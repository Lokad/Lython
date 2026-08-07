namespace Lokad.Lython;

/// <summary>Contains the Python exception projected from an unsuccessful execution.</summary>
/// <param name="ExceptionType">The Python exception type name.</param>
/// <param name="Message">The exception message.</param>
/// <param name="Span">The most specific source span associated with the failure.</param>
/// <param name="StackTrace">The Python stack, from innermost to outermost frame.</param>
/// <param name="SourcePath">The source path supplied by the host when known.</param>
public sealed record LythonRuntimeFailure(
    string ExceptionType,
    string Message,
    LythonSourceSpan? Span,
    IReadOnlyList<LythonStackFrame> StackTrace,
    string? SourcePath)
{
    /// <summary>Creates failure details without source-path metadata.</summary>
    public LythonRuntimeFailure(
        string exceptionType,
        string message,
        LythonSourceSpan? span,
        IReadOnlyList<LythonStackFrame> stackTrace)
        : this(exceptionType, message, span, stackTrace, null)
    {
    }
}
