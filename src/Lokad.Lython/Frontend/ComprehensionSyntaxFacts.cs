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

    public static ScopeDirectiveFacts PrepareBindings(ExpressionSyntax expression,
        IReadOnlyList<ComprehensionClauseSyntax> clauses, bool generator)
    {
        if (expression.PreparedComprehensionScopeFacts is { } prepared) return prepared;

        var names = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var clause in clauses) CollectNames(clause.Target);
        if (generator) Add(".0");
        return expression.PreparedComprehensionScopeFacts = new ScopeDirectiveFacts(
            ScopeDirectiveFacts.Empty.GlobalNames, ScopeDirectiveFacts.Empty.NonlocalNames,
            names, order.ToArray());

        void Add(string name)
        {
            if (names.Add(name)) order.Add(name);
        }

        void CollectNames(LoopTargetSyntax target)
        {
            switch (target)
            {
                case LoopNameTargetSyntax name: Add(name.Name); break;
                case LoopStarredTargetSyntax starred: Add(starred.Name); break;
                case LoopStoreTargetSyntax store:
                    foreach (var name in AssignmentTargetFacts.Names(store.Target)) Add(name);
                    break;
                case LoopTupleTargetSyntax tuple:
                    foreach (var child in tuple.Items) CollectNames(child);
                    break;
            }
        }
    }
}
