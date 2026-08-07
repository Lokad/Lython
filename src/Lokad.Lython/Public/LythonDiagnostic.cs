namespace Lokad.Lython;

/// <summary>Describes a frontend or host-capability issue found before execution.</summary>
/// <param name="Code">The stable Lython diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic message.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Span">The source location, or <see langword="null"/> when no precise location is available.</param>
public sealed record LythonDiagnostic(
    string Code,
    string Message,
    LythonDiagnosticSeverity Severity,
    LythonSourceSpan? Span);
