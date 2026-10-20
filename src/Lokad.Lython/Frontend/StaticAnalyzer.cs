namespace Lokad.Lython.Frontend;

internal static class StaticAnalyzer
{
    private static readonly Action<StaticAnalysisContext>[] Passes =
    [
        StaticScopeDirectiveDiagnostics.Analyze,
        StaticAbstractInterpreter.Analyze,
        StaticRegexDiagnostics.Analyze,
        StaticNameBindingDiagnostics.Analyze,
    ];

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script, ILythonRegexProvider? regexProvider = null, Runtime.MemoryGovernor? governor = null, CancellationToken cancellationToken = default)
    {

        return Analyze(new StaticAnalysisContext(script, regexProvider, governor, cancellationToken));
    }

    internal static IReadOnlyList<LythonDiagnostic> Analyze(StaticAnalysisContext context)
    {
        foreach (var pass in Passes)
        {
            pass(context);
        }

        return context.Diagnostics;
    }

}
