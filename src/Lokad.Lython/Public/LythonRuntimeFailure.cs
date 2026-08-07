namespace Lokad.Lython;

public sealed record LythonRuntimeFailure(
    string ExceptionType,
    string Message,
    LythonSourceSpan? Span,
    IReadOnlyList<LythonStackFrame> StackTrace,
    string? SourcePath)
{
    public LythonRuntimeFailure(
        string exceptionType,
        string message,
        LythonSourceSpan? span,
        IReadOnlyList<LythonStackFrame> stackTrace)
        : this(exceptionType, message, span, stackTrace, null)
    {
    }
}
