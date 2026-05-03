namespace Lokad.Lython;

public sealed record LythonDiagnostic(
    string Code,
    string Message,
    LythonDiagnosticSeverity Severity,
    LythonSourceSpan? Span);
