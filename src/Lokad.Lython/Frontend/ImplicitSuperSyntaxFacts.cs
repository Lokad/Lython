namespace Lokad.Lython.Frontend;

// Python 3.13's direct super().member form reads the class cell and first
// positional local before calling super. This is a lexical compilation choice,
// independent of the value that the name resolves to during execution.
internal static class ImplicitSuperSyntaxFacts
{
    private sealed record Scope(Scope? Parent, ScopeDirectiveFacts Facts,
        bool Function = false, bool Class = false, bool Positional = false)
    {
        public bool CanPreload => Function && Positional &&
            !Facts.IsGlobal("__class__") && !Facts.LocalNames.Contains("__class__") &&
            HasEnclosingClassBinding() && SuperIsImplicitGlobal();

        public bool HasEnclosingClassBinding()
        {
            for (var scope = Parent; scope is not null; scope = scope.Parent)
            {
                if (scope.Class) return true;
                if (scope.Facts.IsGlobal("__class__")) return false;
                if (scope.Facts.LocalNames.Contains("__class__")) return true;
            }
            return false;
        }

        private bool SuperIsImplicitGlobal()
        {
            for (var scope = this; scope is not null; scope = scope.Parent)
            {
                if (scope.Class) continue;
                if (scope.Facts.IsGlobal("super") || scope.Facts.IsNonlocal("super") ||
                    scope.Facts.LocalNames.Contains("super")) return false;
            }
            return true;
        }
    }

    public static void Prepare(ScriptSyntax script)
    {
        var postponed = script.Statements.OfType<ImportStatementSyntax>().Any(import =>
            import.ModuleName == "__future__" && import.ImportedMembers?.Any(member => member.Name == "annotations") == true);
        var moduleUsesSuper = ModuleUsesSuper(script.Statements, postponed);
        var root = new Scope(null, ScopeDirectiveFacts.Empty);
        var pending = new Stack<(object Syntax, Scope Scope)>();
        PushStatements(script.Statements, root);
        while (pending.Count != 0)
        {
            var (syntax, scope) = pending.Pop();
            if (syntax is StatementSyntax statement)
            {
                foreach (var expression in ReadExpressions(statement, postponed)) pending.Push((expression, scope));
                if (statement is FunctionDefinitionStatementSyntax function)
                {
                    var enclosing = TypeParameterScope(scope, function.TypeParameters);
                    var facts = ScopeDirectiveFactsCollector.ForFunctionBindings(function);
                    var functionScope = new Scope(enclosing, facts,
                        Function: true, Positional: function.Parameters.Any(p => p.Kind is FunctionParameterKind.Positional or FunctionParameterKind.PositionalOnly));
                    function.PreparedClassCellRequirement = !facts.IsGlobal("__class__") && !facts.LocalNames.Contains("__class__") &&
                        functionScope.HasEnclosingClassBinding() && ClassCellSyntaxFacts.ReferencesFromFunction(function.Body, postponed);
                    PushStatements(function.Body, functionScope);
                }
                else if (statement is ClassDefinitionStatementSyntax definition)
                    PushStatements(definition.Body, new Scope(TypeParameterScope(scope, definition.TypeParameters),
                        ScopeDirectiveFactsCollector.ForClass(definition), Class: true));
                else foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement)) PushStatements(body, scope);
                continue;
            }
            var node = (ExpressionSyntax)syntax;
            if (node is MemberExpressionSyntax member && member.MemberName != "__class__" &&
                !moduleUsesSuper && scope.CanPreload &&
                Unwrap(member.Target) is CallExpressionSyntax { Arguments.Count: 0 } call &&
                Unwrap(call.Target) is IdentifierExpressionSyntax { Name: "super" })
                call.PreloadImplicitSuperArguments = true;
            if (node is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is { } value) pending.Push((value, scope));
                var parameters = lambda.Parameters.Select(p => new LoweredFunctionParameter(p.Name, p.Kind, null, null)).ToArray();
                var facts = ScopeDirectiveFactsCollector.ForLambda(parameters, lambda.Body);
                var lambdaScope = new Scope(scope, facts, Function: true,
                    Positional: parameters.Any(p => p.Kind is FunctionParameterKind.Positional or FunctionParameterKind.PositionalOnly));
                lambda.PreparedClassCellRequirement = facts.RequiresClassCell && lambdaScope.HasEnclosingClassBinding();
                pending.Push((lambda.Body, lambdaScope));
            }
            else if (ComprehensionSyntaxFacts.TryGetParts(node, out var items, out var clauses, out var generator))
            {
                pending.Push((clauses[0].Iterable, scope));
                var facts = ComprehensionSyntaxFacts.Bindings(clauses.Select(clause => clause.Target), generator);
                var bodyScope = new Scope(scope, facts, Function: generator || scope.Function, Positional: generator || scope.Positional);
                if (generator)
                    facts = facts with { RequiresClassCell = !facts.LocalNames.Contains("__class__") && bodyScope.HasEnclosingClassBinding() &&
                        ComprehensionSyntaxFacts.BodyReferencesClass(items, clauses) };
                node.PreparedComprehensionScopeFacts = facts;
                bodyScope = bodyScope with { Facts = facts };
                foreach (var item in items) pending.Push((item, bodyScope));
                for (var i = 0; i < clauses.Count; i++)
                {
                    var clause = clauses[i];
                    if (i != 0) pending.Push((clause.Iterable, bodyScope));
                    if (clause.Condition is { } condition) pending.Push((condition, bodyScope));
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) pending.Push((read, bodyScope));
                }
            }
            else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(node)) pending.Push((child, scope));
        }

        void PushStatements(IReadOnlyList<StatementSyntax> statements, Scope scope)
        {
            foreach (var statement in statements) pending.Push((statement, scope));
        }
    }

    private static Scope TypeParameterScope(Scope parent, IReadOnlyList<TypeParameterSyntax>? parameters)
        => parameters is not { Count: > 0 } ? parent : new Scope(parent,
            new ScopeDirectiveFacts(ScopeDirectiveFacts.Empty.GlobalNames, ScopeDirectiveFacts.Empty.NonlocalNames,
                new HashSet<string>(parameters.Select(p => p.Name), StringComparer.Ordinal)));

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses) expression = parentheses.Inner;
        return expression;
    }

    internal static IEnumerable<ExpressionSyntax> ReadExpressions(StatementSyntax statement, bool postponedAnnotations)
    {
        if (statement is DeleteStatementSyntax deleted)
        {
            foreach (var read in DeletedTargetReads(deleted.Target)) yield return read;
            yield break;
        }
        if (postponedAnnotations && statement is FunctionDefinitionStatementSyntax function)
        {
            foreach (var decorator in function.Decorators) yield return decorator;
            foreach (var parameter in function.Parameters)
                if (parameter.DefaultValue is { } value) yield return value;
            yield break;
        }
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
            if (!(postponedAnnotations && statement is AnnotatedAssignmentStatementSyntax annotation &&
                ReferenceEquals(expression, annotation.Annotation))) yield return expression;
    }

    private static IEnumerable<ExpressionSyntax> DeletedTargetReads(ExpressionSyntax target)
    {
        target = Unwrap(target);
        if (target is IdentifierExpressionSyntax) yield break;
        if (target is TupleLiteralExpressionSyntax tuple)
        {
            foreach (var item in tuple.Items)
                foreach (var read in DeletedTargetReads(item.Expression)) yield return read;
        }
        else if (target is ListLiteralExpressionSyntax list)
        {
            foreach (var item in list.Items)
                foreach (var read in DeletedTargetReads(item.Expression)) yield return read;
        }
        else foreach (var read in ExpressionSyntaxTraversal.EnumerateChildren(target)) yield return read;
    }

    private static bool ModuleUsesSuper(IReadOnlyList<StatementSyntax> statements, bool postponedAnnotations)
    {
        var facts = ScopeDirectiveFactsCollector.ForFunction([], statements, includeClassCellRequirement: false);
        if (facts.LocalNames.Contains("super") || facts.IsGlobal("super")) return true;
        var pending = new Stack<(object Syntax, bool Module)>();
        foreach (var statement in statements) pending.Push((statement, true));
        while (pending.Count != 0)
        {
            var (syntax, module) = pending.Pop();
            if (syntax is StatementSyntax statement)
            {
                // A global declaration in any child block also marks the
                // module's symbol table, including declarations in siblings.
                if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Global } directive &&
                    directive.Names.Contains("super", StringComparer.Ordinal)) return true;
                if (module) foreach (var expression in ReadExpressions(statement, postponedAnnotations)) pending.Push((expression, true));
                foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                    foreach (var child in body) pending.Push((child, module && statement is not (FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax)));
            }
            else if (module)
            {
                var expression = (ExpressionSyntax)syntax;
                if (expression is IdentifierExpressionSyntax { Name: "super" }) return true;
                if (expression is LambdaExpressionSyntax lambda)
                {
                    foreach (var parameter in lambda.Parameters)
                        if (parameter.DefaultValue is { } value) pending.Push((value, true));
                }
                else if (ComprehensionSyntaxFacts.TryGetParts(expression, out var items, out var clauses, out var generator))
                {
                    pending.Push((clauses[0].Iterable, true));
                    if (!generator)
                    {
                        if (ComprehensionSyntaxFacts.Bindings(clauses.Select(clause => clause.Target)).LocalNames.Contains("super")) return true;
                        foreach (var item in items) pending.Push((item, true));
                        for (var i = 0; i < clauses.Count; i++)
                        {
                            if (i != 0) pending.Push((clauses[i].Iterable, true));
                            if (clauses[i].Condition is { } condition) pending.Push((condition, true));
                            foreach (var read in AssignmentTargetFacts.Reads(clauses[i].Target)) pending.Push((read, true));
                        }
                    }
                }
                else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression)) pending.Push((child, true));
            }
        }
        return false;
    }
}
