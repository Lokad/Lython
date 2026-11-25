namespace Lokad.Lython.Frontend;

internal static class ComprehensionSyntaxFacts
{
    public static ScopeDirectiveFacts PrepareBindings(ComprehensionExpressionSyntax expression)
    {
        if (expression.PreparedScopeFacts is { } prepared) return prepared;

        var names = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var clause in expression.Clauses) CollectNames(clause.Target);
        if (expression is GeneratorExpressionSyntax) Add(".0");
        return expression.PreparedScopeFacts = new ScopeDirectiveFacts(
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
