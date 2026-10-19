namespace Lokad.Lython.Frontend;

internal sealed class StaticAnalysisContext
{
    private readonly List<LythonDiagnostic> _diagnostics = new();

    public StaticAnalysisContext(ScriptSyntax script, ILythonRegexProvider? regexProvider = null, Runtime.MemoryGovernor? governor = null, CancellationToken cancellationToken = default)
    {
        Script = script;
        RegexProvider = regexProvider;
        MemoryGovernor = governor;
        CancellationToken = cancellationToken;
    }

    public ScriptSyntax Script { get; }
    internal ILythonRegexProvider? RegexProvider { get; }
    internal Dictionary<(string Pattern, int Flags), ILythonRegexPattern> RegexPatterns { get; } = new();
    internal Runtime.MemoryGovernor? MemoryGovernor { get; }
    internal CancellationToken CancellationToken { get; }

    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics;

    internal List<LythonDiagnostic> DiagnosticList => _diagnostics;

    public void AddError(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));
    }
}
