namespace Lokad.Lython;

public sealed record LythonRuntimeFailure(
    string ExceptionType,
    string Message,
    LythonSourceSpan? Span,
    IReadOnlyList<LythonStackFrame> StackTrace,
    string? SourcePath = null);
