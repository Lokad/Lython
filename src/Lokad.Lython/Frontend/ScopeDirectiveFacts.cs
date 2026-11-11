namespace Lokad.Lython.Frontend;

internal sealed record ScopeDirectiveFacts(
    IReadOnlySet<string> GlobalNames,
    IReadOnlySet<string> NonlocalNames,
    IReadOnlySet<string> LocalNames,
    IReadOnlyList<string> LocalBindingOrder)
{
    // A free __class__ cell is required only by lexical reads in this function
    // or an enclosed scope. A neighboring method does not supply that demand.
    public bool RequiresClassCell { get; init; }

    // A class's own implicit cell is distinct from an enclosing cell captured
    // by its suite. Prepare this lexical requirement before execution too.
    public bool CreatesClassCell { get; init; }

    // Lexical setup includes dead branches and excludes nested definition bodies.
    public bool ContainsVariableAnnotations { get; init; }

    // Filled by scope validation, which already resolves every nonlocal target.
    // Such cells can change through callbacks; local flow cannot prove them unbound.
    public HashSet<string>? NestedNonlocalNames { get; set; }

    public static ScopeDirectiveFacts Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal), []);

    public bool IsGlobal(string name) => GlobalNames.Contains(name);

    public bool IsNonlocal(string name) => NonlocalNames.Contains(name);
}

internal static class ScopeDirectiveFactsCollector
{
    public static ScopeDirectiveFacts ForModule(ScriptSyntax script)
        => script.PreparedScopeFacts ??= ForFunction([], script.Statements, includeClassCellRequirement: false);

    public static bool ContainsScopeDirective(IReadOnlyList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            if (statement is ScopeDirectiveStatementSyntax)
            {
                return true;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                if (ContainsScopeDirective(body)) return true;
            }
        }

        return false;
    }

    public static ScopeDirectiveFacts ForFunction(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyList<StatementSyntax> body,
        bool includeClassCellRequirement = true)
        => CollectFunctionBindings(parameters.Select(parameter => parameter.Name), body, includeClassCellRequirement);

    private static ScopeDirectiveFacts CollectFunctionBindings(IEnumerable<string> parameterNames,
        IReadOnlyList<StatementSyntax> body, bool includeClassCellRequirement = true)
    {
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        var nonlocalNames = new HashSet<string>(StringComparer.Ordinal);

        var bindings = new List<string>();
        foreach (var name in parameterNames)
        {
            bindings.Add(name);
        }

        var containsAnnotations = CollectScopeBindings(body, bindings, globalNames, nonlocalNames);
        var localNames = new HashSet<string>(bindings, StringComparer.Ordinal);
        localNames.ExceptWith(globalNames);
        localNames.ExceptWith(nonlocalNames);

        return new ScopeDirectiveFacts(globalNames, nonlocalNames, localNames,
            bindings.Where(localNames.Contains).Distinct(StringComparer.Ordinal).ToArray())
        {
            ContainsVariableAnnotations = containsAnnotations,
            RequiresClassCell = includeClassCellRequirement && !globalNames.Contains("__class__") && !localNames.Contains("__class__") &&
                ClassCellSyntaxFacts.ReferencesFromFunction(body)
        };
    }

    public static ScopeDirectiveFacts ForClass(ClassDefinitionStatementSyntax definition)
        => definition.PreparedScopeFacts ??= ForFunction([], definition.Body) with
        { CreatesClassCell = ClassCellSyntaxFacts.RequiresCell(definition) };

    public static ScopeDirectiveFacts ForFunction(FunctionDefinitionStatementSyntax functionDefinition)
    {
        return functionDefinition.PreparedScopeFacts ??= CollectFunctionBindings(
            functionDefinition.Parameters.Select(parameter => parameter.Name),
            functionDefinition.Body);
    }

    internal static ScopeDirectiveFacts ForFunctionBindings(FunctionDefinitionStatementSyntax definition)
        => definition.PreparedScopeFacts ?? CollectFunctionBindings(definition.Parameters.Select(p => p.Name),
            definition.Body, includeClassCellRequirement: false);

    public static HashSet<string> CollectLambdaLocalNames(LambdaExpressionSyntax lambda)
    {
        var localNames = new HashSet<string>(
            lambda.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        CollectExpressionBindings(lambda.Body, localNames);
        return localNames;
    }

    public static ScopeDirectiveFacts ForLambda(IReadOnlyList<LoweredFunctionParameter> parameters, ExpressionSyntax body)
        => CollectLambdaBindings(parameters.Select(parameter => parameter.Name), body);

    private static ScopeDirectiveFacts CollectLambdaBindings(IEnumerable<string> parameterNames, ExpressionSyntax body)
    {
        var bindings = parameterNames.ToList();
        CollectExpressionBindings(body, bindings);
        var locals = new HashSet<string>(bindings, StringComparer.Ordinal);
        return new ScopeDirectiveFacts(EmptyNames, EmptyNames, locals,
            bindings.Distinct(StringComparer.Ordinal).ToArray())
        {
            RequiresClassCell = !locals.Contains("__class__") && ClassCellSyntaxFacts.ReferencesFromExpression(body)
        };
    }

    public static ScopeDirectiveFacts ForLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.PreparedScopeFacts ??= CollectLambdaBindings(
            lambda.Parameters.Select(parameter => parameter.Name), lambda.Body);
    }

    private static readonly IReadOnlySet<string> EmptyNames = new HashSet<string>(StringComparer.Ordinal);

    private static bool CollectScopeBindings(IReadOnlyList<StatementSyntax> statements, ICollection<string> names,
        HashSet<string> globalNames, HashSet<string> nonlocalNames)
    {
        var containsAnnotations = false;
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ScopeDirectiveStatementSyntax directive:
                    (directive.Kind == ScopeDirectiveKind.Global ? globalNames : nonlocalNames).UnionWith(directive.Names);
                    break;
                case ImportStatementSyntax importStatement:
                    foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                    {
                        names.Add(memberName);
                    }
                    break;

                case TypeAliasStatementSyntax alias:
                    names.Add(alias.Name);
                    break;
                case AssignmentStatementSyntax assignment:
                    names.Add(assignment.Name);
                    break;

                case ChainedAssignmentStatementSyntax chained:
                    foreach (var target in chained.Targets) CollectAssignmentTargetBindings(target, names);
                    break;

                case AnnotatedAssignmentStatementSyntax annotated:
                    containsAnnotations = true;
                    if (annotated.Target is NameAssignmentTargetSyntax name)
                    {
                        names.Add(name.Name);
                    }

                    break;

                case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                    names.Add(nameTarget.Name);
                    break;

                case UnpackingAssignmentStatementSyntax unpacking:
                    foreach (var target in unpacking.Targets) CollectUnpackingTargetNames(target, names);
                    break;

                case ForStatementSyntax forStatement:
                    CollectLoopTargetBindings(forStatement.Target, names);
                    break;

                case WithStatementSyntax withStatement:
                    foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(withStatement))
                        CollectExpressionBindings(expression, names);
                    foreach (var boundName in withStatement.BoundNames) names.Add(boundName);
                    containsAnnotations |= CollectScopeBindings(withStatement.Body, names, globalNames, nonlocalNames);
                    continue;

                case TryStatementSyntax tryStatement:
                    containsAnnotations |= CollectScopeBindings(tryStatement.TryBody, names, globalNames, nonlocalNames);
                    foreach (var exceptClause in tryStatement.ExceptClauses)
                    {
                        if (exceptClause.ExceptionTypeExpression is not null) CollectExpressionBindings(exceptClause.ExceptionTypeExpression, names);
                        if (exceptClause.ExceptionVariableName is not null) names.Add(exceptClause.ExceptionVariableName);
                        containsAnnotations |= CollectScopeBindings(exceptClause.Body, names, globalNames, nonlocalNames);
                    }
                    if (tryStatement.ElseBody is not null)
                        containsAnnotations |= CollectScopeBindings(tryStatement.ElseBody, names, globalNames, nonlocalNames);
                    if (tryStatement.FinallyBody is not null)
                        containsAnnotations |= CollectScopeBindings(tryStatement.FinallyBody, names, globalNames, nonlocalNames);
                    continue;

                case MatchStatementSyntax matchStatement:
                    CollectExpressionBindings(matchStatement.Subject, names);
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        CollectPatternBindings(matchCase.Pattern, names);
                        if (matchCase.Guard is not null) CollectExpressionBindings(matchCase.Guard, names);
                        containsAnnotations |= CollectScopeBindings(matchCase.Body, names, globalNames, nonlocalNames);
                    }
                    continue;

                case DeleteStatementSyntax { Target: IdentifierExpressionSyntax deleted }:
                    names.Add(deleted.Name);
                    break;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    names.Add(functionDefinition.Name);
                    foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(functionDefinition))
                        CollectExpressionBindings(expression, names);
                    continue;

                case ClassDefinitionStatementSyntax classDefinition:
                    names.Add(classDefinition.Name);
                    foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(classDefinition))
                        CollectExpressionBindings(expression, names);
                    continue;

            }

            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                CollectExpressionBindings(expression, names);

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                // Keep collecting bindings after finding an annotation.
                containsAnnotations |= CollectScopeBindings(body, names, globalNames, nonlocalNames);
            }
        }

        return containsAnnotations;

        static void CollectAssignmentTargetBindings(AssignmentTargetSyntax target, ICollection<string> names)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax name:
                    names.Add(name.Name);
                    break;
                case UnpackingAssignmentTargetGroupSyntax unpacking:
                    foreach (var nestedTarget in unpacking.Targets) CollectUnpackingTargetNames(nestedTarget, names);
                    break;
            }
        }

        static void CollectUnpackingTargetNames(UnpackingTargetSyntax target, ICollection<string> names)
        {
            if (target is UnpackingNameTargetSyntax name)
            {
                names.Add(name.Name);
                return;
            }

            if (target is UnpackingNestedTargetSyntax nested)
            {
                foreach (var nestedItem in nested.Items)
                {
                    CollectUnpackingTargetNames(nestedItem, names);
                }
            }
        }
    }

    private static void CollectLoopTargetBindings(LoopTargetSyntax target, ICollection<string> names)
    {
        switch (target)
        {
            case LoopStoreTargetSyntax store:
                foreach (var binding in AssignmentTargetFacts.Names(store.Target)) names.Add(binding);
                break;
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopStarredTargetSyntax starred:
                names.Add(starred.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectLoopTargetBindings(item, names);
                break;
        }
    }

    internal static void CollectPatternBindings(PatternSyntax pattern, ICollection<string> names)
    {
        switch (pattern)
        {
            case MatchCapturePatternSyntax capture:
                names.Add(capture.Name);
                break;
            case MatchSequencePatternSyntax sequence:
                foreach (var item in sequence.Items) CollectPatternBindings(item, names);
                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items) CollectPatternBindings(item.Pattern, names);
                if (mapping.RestName is not null) names.Add(mapping.RestName);
                break;
            case MatchClassPatternSyntax classPattern:
                foreach (var item in classPattern.PositionalPatterns) CollectPatternBindings(item, names);
                foreach (var item in classPattern.KeywordPatterns) CollectPatternBindings(item.Pattern, names);
                break;
            case MatchStarPatternSyntax star when star.Name is not null:
                names.Add(star.Name);
                break;
            case MatchAsPatternSyntax asPattern:
                CollectPatternBindings(asPattern.Pattern, names);
                names.Add(asPattern.Name);
                break;
            case MatchOrPatternSyntax orPattern:
                foreach (var item in orPattern.Patterns) CollectPatternBindings(item, names);
                break;
        }
    }

    internal static void CollectExpressionBindings(ExpressionSyntax expression, ICollection<string> names)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            foreach (var parameter in lambda.Parameters)
                if (parameter.DefaultValue is not null) CollectExpressionBindings(parameter.DefaultValue, names);
            return;
        }

        if (expression is AssignmentExpressionSyntax assignment)
        {
            names.Add(assignment.Name);
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            CollectExpressionBindings(child, names);
        }
    }
}
