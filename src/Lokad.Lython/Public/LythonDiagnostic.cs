namespace Lokad.Lython;

/// <summary>Describes a frontend or host-capability issue found before execution.</summary>
/// <param name="Code">The stable Lython diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic message.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Span">The source location, or <see langword="null"/> when no precise location is available.</param>
public sealed record LythonDiagnostic
{
    private string _code = "LA0000";

    public LythonDiagnostic(
        string Code,
        string Message,
        LythonDiagnosticSeverity Severity,
        LythonSourceSpan? Span)
    {
        this.Code = Code;
        this.Message = Message;
        this.Severity = Severity;
        this.Span = Span;
    }

    public string Code
    {
        get => _code;
        init
        {
            _ = Frontend.LythonDiagnosticCode.Parse(value);
            _code = value;
        }
    }

    public string Message { get; init; }

    public LythonDiagnosticSeverity Severity { get; init; }

    public LythonSourceSpan? Span { get; init; }

    public void Deconstruct(
        out string Code,
        out string Message,
        out LythonDiagnosticSeverity Severity,
        out LythonSourceSpan? Span)
    {
        Code = this.Code;
        Message = this.Message;
        Severity = this.Severity;
        Span = this.Span;
    }
}
