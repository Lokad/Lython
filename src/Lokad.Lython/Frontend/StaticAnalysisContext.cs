namespace Lokad.Lython.Frontend;

internal sealed class StaticAnalysisContext : IDisposable
{
    private readonly List<LythonDiagnostic> _diagnostics = new();
    private ILythonMemoryScope? _regexOwner;
    private List<ILythonRegexPattern>? _regexHandles;

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
    internal ILythonMemoryBudget RegexMemoryOwner => _regexOwner ??=
        (MemoryGovernor?.Budget ?? new LythonMemoryBudget()).CreateScope();

    internal void ReserveRegexStorage(long bytes)
    {
        if (!RegexMemoryOwner.TryReserve(bytes, out _))
            throw MemoryGovernor?.RefuseAdmission("shared memory budget refused regex analysis storage", bytes, null)
                ?? Runtime.RuntimeErrors.Attributed("MemoryError", "shared memory budget refused regex analysis storage", null,
                    LythonFailureReason.SharedMemoryAllowance, requested: bytes);
    }

    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics;

    internal List<LythonDiagnostic> DiagnosticList => _diagnostics;
    internal void OwnRegexPattern(ILythonRegexPattern pattern) => (_regexHandles ??= new()).Add(pattern);

    public void Dispose()
    {
        // Cache aliases can overwrite one another; every acquired handle still
        // gets closed once. These are disposable resources, not guest roots.
        try
        {
            if (_regexHandles is { } handles)
                foreach (var pattern in handles) pattern.Dispose();
            _regexHandles = null;
            RegexPatterns.Clear();
        }
        finally { _regexOwner?.Dispose(); _regexOwner = null; }
    }

    public void AddError(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(code.Value, message, LythonDiagnosticSeverity.Error, span));
    }
}
