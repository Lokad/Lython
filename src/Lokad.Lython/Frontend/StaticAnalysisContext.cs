namespace Lokad.Lython.Frontend;

internal sealed class StaticAnalysisContext : IDisposable
{
    private readonly List<LythonDiagnostic> _diagnostics = new();
    private readonly CompiledRegexPrograms _regexPrograms;
    private readonly bool _ownsRegexPrograms;
    private Runtime.MemoryGovernor.TemporaryMemoryReservation? _regexGroupFactsStorage;
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
    internal Dictionary<(string Pattern, LythonRegexOptions Options), (AbstractRegexPatternSummary Summary, byte[] Utf8)> RegexPatterns { get; } = new();
    internal Runtime.MemoryGovernor? MemoryGovernor { get; }
    internal CancellationToken CancellationToken { get; }
    internal ILythonMemoryBudget RegexMemoryOwner => _regexPrograms.StorageOwner;

    internal ILythonMemoryReservation ReserveRegexStorage(long bytes, LythonSourceSpan span)
        => _regexPrograms.Reserve(bytes, span);

    internal IReadOnlyDictionary<Runtime.Text.PyString, int> ReadRegexGroupFacts(ILythonRegexPattern program,
        int captureSlotCount, LythonSourceSpan span)
        => Runtime.LythonRuntime.RegexCompiler.CopyAnalysisNamedGroups(program, captureSlotCount, MemoryGovernor,
            _regexGroupFactsStorage ??= MemoryGovernor?.ReserveTemporary(0, span), CancellationToken, span);

    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics;

    internal List<LythonDiagnostic> DiagnosticList => _diagnostics;
    internal void RetainRegexProgram(byte[] encoded, LythonRegexOptions options, ILythonRegexPattern program,
        int captureSlotCount, LythonSourceSpan span)
        => _regexPrograms.Add(encoded, options, program, captureSlotCount, CancellationToken, span);

    internal StringLiteralExpressionSyntax? GetCompiledRegexLiteral(ExpressionSyntax? expression, string text)
        => !_ownsRegexPrograms && expression is StringLiteralExpressionSyntax literal && ReferenceEquals(literal.Value, text)
            ? literal : null;

    internal void PrepareRegexLiteral(ExpressionSyntax? expression, string pattern, byte[] encoded)
    {
        // Only the caller-owned program table survives analysis. The script
        // or imported module keeps its key bytes funded through execution.
        if (encoded.Length > Runtime.Text.PyString.InlineUtf8Capacity && GetCompiledRegexLiteral(expression, pattern) is { } literal)
            literal.PreparedRegexUtf8 ??= encoded;
    }

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
        try { _regexGroupFactsStorage?.Dispose(); }
        finally { if (_ownsRegexPrograms) _regexPrograms.Dispose(); }
    }

    public void AddError(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));
    }
}
