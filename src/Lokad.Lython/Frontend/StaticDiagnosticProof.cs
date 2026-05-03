namespace Lokad.Lython.Frontend;

internal sealed record StaticDiagnosticProof(
    string Category,
    string Subject,
    string Reason,
    AbstractValue? Evidence = null);

internal static class StaticDiagnosticSink
{
    public static void AddError(
        List<LythonDiagnostic> diagnostics,
        string code,
        string message,
        LythonSourceSpan span,
        StaticDiagnosticProof? proof = null)
    {
        _ = proof;
        diagnostics.Add(new LythonDiagnostic(code, message, LythonDiagnosticSeverity.Error, span));
    }

    public static void AddError(
        StaticAnalysisContext context,
        string code,
        string message,
        LythonSourceSpan span,
        StaticDiagnosticProof? proof = null)
    {
        _ = proof;
        context.AddError(code, message, span);
    }
}
