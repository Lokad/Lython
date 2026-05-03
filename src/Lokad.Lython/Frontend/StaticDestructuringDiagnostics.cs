namespace Lokad.Lython.Frontend;

internal static class StaticDestructuringDiagnostics
{
    public static void AnalyzeUnpackingTargets(
        IReadOnlyList<UnpackingTargetSyntax> targets,
        ExpressionSyntax valueExpression,
        LythonSourceSpan span,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!StaticAbstractFacts.TryGetKnownSequenceArity(valueExpression, bindings, out var count))
        {
            if (StaticAbstractFacts.IsDefinitelyKnownNonIterable(valueExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", valueExpression.Span);
            }

            return;
        }

        var starredIndex = -1;
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].IsStarred)
            {
                starredIndex = i;
                break;
            }
        }

        if (starredIndex < 0)
        {
            if (count != targets.Count)
            {
                AddDiagnostic(diagnostics, "LA3030", "unpacking assignment has the wrong number of values", span);
            }

            return;
        }

        if (count < targets.Count - 1)
        {
            AddDiagnostic(diagnostics, "LA3030", "unpacking assignment has the wrong number of values", span);
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
