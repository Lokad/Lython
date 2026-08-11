namespace Lokad.Lython.Frontend;

internal sealed class StaticAnalysisContext
{
    private readonly List<LythonDiagnostic> _diagnostics = new();

    public StaticAnalysisContext(ScriptSyntax script)
    {
        Script = script;
    }

    public ScriptSyntax Script { get; }

    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics;

    internal List<LythonDiagnostic> DiagnosticList => _diagnostics;

    public void AddError(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));
    }
}
