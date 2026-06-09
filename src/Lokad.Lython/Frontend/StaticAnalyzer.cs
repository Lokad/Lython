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

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var context = new StaticAnalysisContext(script);
        foreach (var pass in Passes)
        {
            pass(context);
        }

        return context.Diagnostics;
    }

    public static IReadOnlyList<LythonDiagnostic> AnalyzeHostRequirements(ScriptSyntax script, ILythonHost host)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(host);

        var context = new StaticAnalysisContext(script);
        StaticHostRequirementDiagnostics.Analyze(context, host);
        return context.Diagnostics;
    }
}
