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
                StaticAbstractFacts.TryGetNonIterablePrimitiveTypeName(valueExpression, bindings, out var primitiveTypeName);
                AddDiagnostic(diagnostics, "LA3031", primitiveTypeName is null ? "Object is not iterable." : $"cannot unpack non-iterable {primitiveTypeName} object", valueExpression.Span);
            }

            return;
        }

        var layout = UnpackingLayout.FromTargets(targets);
        if (!layout.AcceptsValueCount(count))
        {
            AddDiagnostic(diagnostics, "LA3030", layout.DescribeArityMismatch(count), span);
        }
    }

}
