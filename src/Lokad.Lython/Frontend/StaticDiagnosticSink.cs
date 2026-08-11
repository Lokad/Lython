namespace Lokad.Lython.Frontend;

internal static class StaticDiagnosticSink
{
    public static void AddError(List<LythonDiagnostic> diagnostics, LythonDiagnosticCode code, string message, LythonSourceSpan span)
        => diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));

    public static void AddError(StaticAnalysisContext context, LythonDiagnosticCode code, string message, LythonSourceSpan span)
        => context.AddError(code, message, span);
}
