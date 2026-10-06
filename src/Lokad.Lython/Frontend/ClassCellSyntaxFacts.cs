namespace Lokad.Lython.Frontend;

internal static class ClassCellSyntaxFacts
{
    internal static readonly ScopeDirectiveFacts Scope = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(["__class__"], StringComparer.Ordinal));

    public static bool RequiresCell(ClassDefinitionStatementSyntax definition)
    {
        var pending = new Stack<StatementSyntax>(definition.Body);
        while (pending.Count > 0)
        {
            var statement = pending.Pop();
            if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Nonlocal } directive &&
                directive.Names.Contains("__class__", StringComparer.Ordinal)) return true;
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                if (ReferencesClass(expression)) return true;
            // A nested class suite may capture this class's cell, while its
            // methods establish a separate cell for their own defining class.
            if (statement is ClassDefinitionStatementSyntax nested)
            {
                if (SuiteReferencesEnclosingClass(nested.Body)) return true;
                continue;
            }
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                foreach (var child in body) pending.Push(child);
        }
        return false;
    }

    private static bool ReferencesClass(ExpressionSyntax expression)
    {
        var pending = new Stack<ExpressionSyntax>();
        pending.Push(expression);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is IdentifierExpressionSyntax { Name: "__class__" or "super" }) return true;
            foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(node)) pending.Push(child);
        }
        return false;
    }

    private static bool SuiteReferencesEnclosingClass(IReadOnlyList<StatementSyntax> body)
    {
        foreach (var statement in body)
        {
            if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Nonlocal } directive &&
                directive.Names.Contains("__class__", StringComparer.Ordinal)) return true;
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                if (ReferencesClass(expression)) return true;
            if (statement is FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax) continue;
            foreach (var child in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                if (SuiteReferencesEnclosingClass(child)) return true;
        }
        return false;
    }
}
