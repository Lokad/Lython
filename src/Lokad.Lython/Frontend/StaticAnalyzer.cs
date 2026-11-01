namespace Lokad.Lython.Frontend;

internal static class StaticAnalyzer
{
    private static readonly Action<StaticAnalysisContext>[] Passes =
    [
        StaticScopeDirectiveDiagnostics.Analyze,
        StaticAbstractInterpreter.Analyze,
        StaticNameBindingDiagnostics.Analyze,
    ];

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script, ILythonRegexProvider? regexProvider = null, Runtime.MemoryGovernor? governor = null, CancellationToken cancellationToken = default)
    {
        using var context = new StaticAnalysisContext(script, regexProvider, governor, cancellationToken);
        return Analyze(context);
    }

    internal static IReadOnlyList<LythonDiagnostic> Analyze(StaticAnalysisContext context)
    {
        foreach (var pass in Passes)
        {
            context.MemoryGovernor?.ThrowIfAdmissionFailed();
            PythonTokenization.CheckCancellation(context.CancellationToken, 0);
            pass(context);
        }

        context.MemoryGovernor?.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(context.CancellationToken, 0);
        return context.Diagnostics;
    }

}
