namespace Lokad.Lython.Frontend;

internal static class StaticIterationDiagnostics
{
    public static void AnalyzeLoopTarget(
        LoopTargetSyntax target,
        ExpressionSyntax iterableExpression,
        LythonSourceSpan span,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (StaticAbstractFacts.IsDefinitelyKnownNonIterable(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", iterableExpression.Span);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownTextFileHandle(iterableExpression, bindings, out var textFileMode) &&
            textFileMode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
        {
            AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", iterableExpression.Span);
            return;
        }

        if (target is not LoopTupleTargetSyntax tupleTarget)
        {
            return;
        }

        switch (iterableExpression)
        {
            case ListLiteralExpressionSyntax { Items: var listItems, HasUnpacking: false }:
                AnalyzeTupleLoopItems(tupleTarget, listItems, span, diagnostics, bindings);
                break;
            case TupleLiteralExpressionSyntax { Items: var tupleItems, HasUnpacking: false }:
                AnalyzeTupleLoopItems(tupleTarget, tupleItems, span, diagnostics, bindings);
                break;
            case SetLiteralExpressionSyntax { Items: var setItems, HasUnpacking: false }:
                AnalyzeTupleLoopItems(tupleTarget, setItems, span, diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzeTupleLoopItems(
        LoopTupleTargetSyntax target,
        IReadOnlyList<CollectionDisplayItemSyntax> items,
        LythonSourceSpan span,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var item in items)
        {
            if (!StaticAbstractFacts.TryGetKnownSequenceArity(item.Expression, bindings, out var count))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownNonIterable(item.Expression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", item.Span);
                    return;
                }

                continue;
            }

            if (count != target.Items.Count)
            {
                AddDiagnostic(diagnostics, "LA3030", "unpacking assignment has the wrong number of values", span);
                return;
            }
        }
    }

}
