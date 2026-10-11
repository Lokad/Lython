namespace Lokad.Lython.Frontend;

internal static class ComprehensionSyntaxFacts
{
    public static bool TryGetParts(ExpressionSyntax expression, out IReadOnlyList<ExpressionSyntax> items,
        out IReadOnlyList<ComprehensionClauseSyntax> clauses, out bool generator)
    {
        generator = false;
        switch (expression)
        {
            case ListComprehensionExpressionSyntax list: items = [list.ItemExpression]; clauses = list.Clauses; return true;
            case SetComprehensionExpressionSyntax set: items = [set.ItemExpression]; clauses = set.Clauses; return true;
            case DictComprehensionExpressionSyntax dict: items = [dict.KeyExpression, dict.ValueExpression]; clauses = dict.Clauses; return true;
            case GeneratorExpressionSyntax gen: items = [gen.ItemExpression]; clauses = gen.Clauses; generator = true; return true;
            default: items = []; clauses = []; return false;
        }
    }

    public static ScopeDirectiveFacts Bindings(IEnumerable<LoopTargetSyntax> targets, bool generator = false)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets) CollectNames(target, names);
        if (generator) names.Add(".0");
        return new ScopeDirectiveFacts(ScopeDirectiveFacts.Empty.GlobalNames, ScopeDirectiveFacts.Empty.NonlocalNames, names);
    }

    private static void CollectNames(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name: names.Add(name.Name); break;
            case LoopStarredTargetSyntax starred: names.Add(starred.Name); break;
            case LoopStoreTargetSyntax store:
                foreach (var name in AssignmentTargetFacts.Names(store.Target)) names.Add(name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var child in tuple.Items) CollectNames(child, names);
                break;
        }
    }

    public static bool BodyReferencesClass(IReadOnlyList<ExpressionSyntax> items, IReadOnlyList<ComprehensionClauseSyntax> clauses)
    {
        if (items.Any(ClassCellSyntaxFacts.ReferencesFromExpression)) return true;
        for (var i = 0; i < clauses.Count; i++)
        {
            var clause = clauses[i];
            if (i != 0 && ClassCellSyntaxFacts.ReferencesFromExpression(clause.Iterable)) return true;
            if (clause.Condition is { } condition && ClassCellSyntaxFacts.ReferencesFromExpression(condition)) return true;
            if (AssignmentTargetFacts.Reads(clause.Target).Any(ClassCellSyntaxFacts.ReferencesFromExpression)) return true;
        }
        return false;
    }
}
