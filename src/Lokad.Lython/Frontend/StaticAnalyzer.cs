namespace Lokad.Lython.Frontend;

internal static class StaticAnalyzer
{
    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script, ILythonRegexProvider? regexProvider = null, Runtime.MemoryGovernor? governor = null, CancellationToken cancellationToken = default)
    {
        using var context = new StaticAnalysisContext(script, regexProvider, governor, cancellationToken);
        return Analyze(context);
    }

    internal static IReadOnlyList<LythonDiagnostic> Analyze(StaticAnalysisContext context, bool hasScopeDirectives = true)
    {
        if (hasScopeDirectives)
        {
            CheckAdmissionAndCancellation(context);
            StaticScopeDirectiveDiagnostics.Analyze(context);
        }
        CheckAdmissionAndCancellation(context);
        StaticAbstractInterpreter.Analyze(context);
        CheckAdmissionAndCancellation(context);
        StaticNameBindingDiagnostics.Analyze(context);
        CheckAdmissionAndCancellation(context);
        return context.Diagnostics;
    }

    private static void CheckAdmissionAndCancellation(StaticAnalysisContext context)
    {
        context.MemoryGovernor?.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(context.CancellationToken, 0);
    }
}
