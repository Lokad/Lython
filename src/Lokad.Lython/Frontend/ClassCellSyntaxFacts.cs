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

    internal static bool ReferencesFromFunction(IReadOnlyList<StatementSyntax> body, bool postponedAnnotations = false)
    {
        var pending = new Stack<StatementSyntax>(body);
        while (pending.Count > 0)
        {
            var statement = pending.Pop();
            if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Nonlocal } directive &&
                directive.Names.Contains("__class__", StringComparer.Ordinal)) return true;
            // Bare deleted names are stores, not lexical class/super reads.
            var expressions = statement is DeleteStatementSyntax deleted
                ? ReadDeletedTarget(deleted.Target)
                : ImplicitSuperSyntaxFacts.ReadExpressions(statement, postponedAnnotations);
            foreach (var expression in expressions)
                if (ReferencesFromExpression(expression)) return true;
            if (statement is FunctionDefinitionStatementSyntax function)
            {
                var bindings = ScopeDirectiveFactsCollector.ForFunctionBindings(function);
                if (!bindings.IsGlobal("__class__") && !bindings.LocalNames.Contains("__class__"))
                    foreach (var child in function.Body) pending.Push(child);
                continue;
            }
            if (statement is ClassDefinitionStatementSyntax nested)
            {
                if (SuiteReferencesEnclosingClass(nested.Body, postponedAnnotations)) return true;
                continue;
            }
            foreach (var child in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                foreach (var statementChild in child) pending.Push(statementChild);
        }
        return false;
    }

    private static IEnumerable<ExpressionSyntax> ReadDeletedTarget(ExpressionSyntax target)
    {
        if (target is IdentifierExpressionSyntax) yield break;
        if (target is ParenthesizedExpressionSyntax parenthesized)
        {
            foreach (var read in ReadDeletedTarget(parenthesized.Inner)) yield return read;
        }
        else if (target is TupleLiteralExpressionSyntax tuple)
        {
            foreach (var item in tuple.Items)
                foreach (var read in ReadDeletedTarget(item.Expression)) yield return read;
        }
        else if (target is ListLiteralExpressionSyntax list)
        {
            foreach (var item in list.Items)
                foreach (var read in ReadDeletedTarget(item.Expression)) yield return read;
        }
        else foreach (var read in ExpressionSyntaxTraversal.EnumerateChildren(target)) yield return read;
    }

    internal static bool ReferencesFromExpression(ExpressionSyntax expression)
    {
        var pending = new Stack<(ExpressionSyntax Node, bool ClassShadowed)>();
        pending.Push((expression, false));
        while (pending.Count > 0)
        {
            var (node, classShadowed) = pending.Pop();
            if (node is IdentifierExpressionSyntax { Name: "super" } ||
                !classShadowed && node is IdentifierExpressionSyntax { Name: "__class__" }) return true;
            if (node is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is { } value) pending.Push((value, classShadowed));
                if (classShadowed || ScopeDirectiveFactsCollector.CollectLambdaLocalNames(lambda).Contains("__class__")) continue;
            }
            if (ComprehensionSyntaxFacts.TryGetParts(node, out var items, out var clauses, out var generator))
            {
                pending.Push((clauses[0].Iterable, classShadowed));
                var shadowed = classShadowed || ComprehensionSyntaxFacts.Bindings(clauses.Select(clause => clause.Target)).LocalNames.Contains("__class__");
                if (generator && shadowed) continue;
                foreach (var item in items) pending.Push((item, shadowed));
                for (var i = 0; i < clauses.Count; i++)
                {
                    if (i != 0) pending.Push((clauses[i].Iterable, shadowed));
                    if (clauses[i].Condition is { } condition) pending.Push((condition, shadowed));
                    foreach (var read in AssignmentTargetFacts.Reads(clauses[i].Target)) pending.Push((read, shadowed));
                }
            }
            else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(node)) pending.Push((child, classShadowed));
        }
        return false;
    }

    private static bool ReferencesClass(ExpressionSyntax expression) => ReferencesFromExpression(expression);

    private static bool SuiteReferencesEnclosingClass(IReadOnlyList<StatementSyntax> body, bool postponedAnnotations = false)
    {
        var bindings = ScopeDirectiveFactsCollector.ForFunction([], body, includeClassCellRequirement: false);
        if (bindings.IsNonlocal("__class__")) return true;
        if (bindings.IsGlobal("__class__") || bindings.LocalNames.Contains("__class__")) return false;
        var pending = new Stack<StatementSyntax>(body);
        while (pending.Count != 0)
        {
            var statement = pending.Pop();
            foreach (var expression in ImplicitSuperSyntaxFacts.ReadExpressions(statement, postponedAnnotations))
                if (ReadsEnclosingClass(expression)) return true;
            if (statement is FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax) continue;
            foreach (var child in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                foreach (var nested in child) pending.Push(nested);
        }
        return false;
    }

    private static bool ReadsEnclosingClass(ExpressionSyntax expression)
    {
        var pending = new Stack<ExpressionSyntax>();
        pending.Push(expression);
        while (pending.Count != 0)
        {
            var node = pending.Pop();
            if (node is IdentifierExpressionSyntax { Name: "__class__" }) return true;
            // A class body's super reference does not request a cell. Functions
            // created there capture the new class's cell, not its parent's.
            if (node is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is { } value) pending.Push(value);
            }
            else if (ComprehensionSyntaxFacts.TryGetParts(node, out var items, out var clauses, out var generator))
            {
                pending.Push(clauses[0].Iterable);
                if (!generator && !ComprehensionSyntaxFacts.Bindings(clauses.Select(clause => clause.Target)).LocalNames.Contains("__class__"))
                {
                    foreach (var item in items) pending.Push(item);
                    for (var i = 0; i < clauses.Count; i++)
                    {
                        if (i != 0) pending.Push(clauses[i].Iterable);
                        if (clauses[i].Condition is { } condition) pending.Push(condition);
                        foreach (var read in AssignmentTargetFacts.Reads(clauses[i].Target)) pending.Push(read);
                    }
                }
            }
            else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(node)) pending.Push(child);
        }
        return false;
    }
}
