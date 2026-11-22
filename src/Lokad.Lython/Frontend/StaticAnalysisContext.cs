namespace Lokad.Lython.Frontend;

internal sealed class StaticAnalysisContext : IDisposable
{
    private readonly List<LythonDiagnostic> _diagnostics = new();
    private readonly CompiledRegexPrograms _regexPrograms;
    private readonly bool _ownsRegexPrograms;
    private readonly Dictionary<StatementSyntax, StatementFacts>? _statementFacts;

    public StaticAnalysisContext(ScriptSyntax script, ILythonRegexProvider? regexProvider = null,
        Runtime.MemoryGovernor? governor = null, CancellationToken cancellationToken = default,
        CompiledRegexPrograms? regexPrograms = null,
        Dictionary<StatementSyntax, StatementFacts>? statementFacts = null)
    {
        Script = script;
        RegexProvider = regexProvider;
        MemoryGovernor = governor;
        CancellationToken = cancellationToken;
        _regexPrograms = regexPrograms ?? new(governor);
        _ownsRegexPrograms = regexPrograms is null;
        _statementFacts = statementFacts;
    }

    public ScriptSyntax Script { get; }
    internal ILythonRegexProvider? RegexProvider { get; }
    internal Dictionary<(string Pattern, LythonRegexOptions Options), AbstractRegexPatternSummary> RegexPatterns { get; } = new();
    internal Runtime.MemoryGovernor? MemoryGovernor { get; }
    internal CancellationToken CancellationToken { get; }
    internal ILythonMemoryBudget RegexMemoryOwner => _regexPrograms.StorageOwner;

    internal void ReserveRegexStorage(long bytes, LythonSourceSpan span)
        => _regexPrograms.Reserve(bytes, span);

    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics;

    internal List<LythonDiagnostic> DiagnosticList => _diagnostics;
    internal void RetainRegexProgram(byte[] encoded, LythonRegexOptions options, ILythonRegexPattern program,
        int captureSlotCount, LythonSourceSpan span)
        => _regexPrograms.Add(encoded, options, program, captureSlotCount, CancellationToken, span);

    internal List<CallExpressionSyntax>? GetPotentialMutations(StatementSyntax statement)
        => _statementFacts?.GetValueOrDefault(statement).PotentialMutations;

    internal void CheckExecution()
    {
        MemoryGovernor?.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(CancellationToken, 0);
    }

    public void Dispose()
    {
        RegexPatterns.Clear();
        if (_ownsRegexPrograms) _regexPrograms.Dispose();
    }

    public void AddError(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));
    }
}
