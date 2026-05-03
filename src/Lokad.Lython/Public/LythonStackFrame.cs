namespace Lokad.Lython;

public sealed record LythonStackFrame(
    string FunctionName,
    LythonSourceSpan? Span,
    string? SourcePath = null);
