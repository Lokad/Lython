namespace Lokad.Lython.Frontend;

internal sealed record ScopeDirectiveFacts(
    IReadOnlySet<string> GlobalNames,
    IReadOnlySet<string> NonlocalNames,
    IReadOnlySet<string> LocalNames)
{
    public static ScopeDirectiveFacts Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    public bool IsGlobal(string name) => GlobalNames.Contains(name);

    public bool IsNonlocal(string name) => NonlocalNames.Contains(name);
}

internal static class ScopeDirectiveFactsCollector
{
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
        IReadOnlyList<StatementSyntax> body)
    {
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        var nonlocalNames = new HashSet<string>(StringComparer.Ordinal);
        CollectDirectives(body, globalNames, nonlocalNames);

        var localNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            localNames.Add(parameter.Name);
        }

        CollectLocalBindings(body, localNames);
        localNames.ExceptWith(globalNames);
        localNames.ExceptWith(nonlocalNames);

        return new ScopeDirectiveFacts(globalNames, nonlocalNames, localNames);
    }

    public static ScopeDirectiveFacts ForFunction(FunctionDefinitionStatementSyntax functionDefinition)
        => ForFunction(
            functionDefinition.Parameters.Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                Annotation: null,
                DefaultValue: null)).ToArray(),
            functionDefinition.Body);

    public static ScopeDirectiveFacts ForLoweredStatements(
        IReadOnlyList<LoweredFunctionParameter>? parameters,
        IReadOnlyList<LoweredStatement> statements)
        => ForFunction(parameters ?? [], statements.Select(GetSyntax).ToArray());

    private static StatementSyntax GetSyntax(LoweredStatement statement)
        => statement switch
        {
            LoweredImportStatement lowered => lowered.Syntax,
            LoweredScopeDirectiveStatement lowered => lowered.Syntax,
            LoweredFunctionDefinitionStatement lowered => lowered.Syntax,
            LoweredClassDefinitionStatement lowered => lowered.Syntax,
            LoweredAssignmentStatement lowered => lowered.Syntax,
            LoweredExpressionStatement lowered => lowered.Syntax,
            LoweredIfStatement lowered => lowered.Syntax,
            LoweredForStatement lowered => lowered.Syntax,
            LoweredWhileStatement lowered => lowered.Syntax,
            LoweredMatchStatement lowered => lowered.Syntax,
            LoweredWithStatement lowered => lowered.Syntax,
            LoweredTryStatement lowered => lowered.Syntax,
            LoweredPassStatement lowered => lowered.Syntax,
            LoweredBreakStatement lowered => lowered.Syntax,
            LoweredContinueStatement lowered => lowered.Syntax,
            LoweredAssertStatement lowered => lowered.Syntax,
            LoweredDeleteStatement lowered => lowered.Syntax,
            LoweredReturnStatement lowered => lowered.Syntax,
            LoweredRaiseStatement lowered => lowered.Syntax,
            LoweredOtherStatement lowered => lowered.Syntax,
            _ => throw new InvalidOperationException($"Unknown lowered statement kind: {statement.GetType().Name}")
        };

    private static void CollectDirectives(
        IReadOnlyList<StatementSyntax> statements,
        HashSet<string> globalNames,
        HashSet<string> nonlocalNames)
    {
        foreach (var statement in statements)
        {
            if (statement is ScopeDirectiveStatementSyntax directive)
            {
                foreach (var name in directive.Names)
                {
                    if (directive.Kind == ScopeDirectiveKind.Global)
                    {
                        globalNames.Add(name);
                    }
                    else
                    {
                        nonlocalNames.Add(name);
                    }
                }
            }

            if (statement is FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax)
            {
                continue;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                CollectDirectives(body, globalNames, nonlocalNames);
            }
        }
    }

    private static void CollectLocalBindings(IReadOnlyList<StatementSyntax> statements, HashSet<string> names)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ImportStatementSyntax importStatement:
                    names.Add(importStatement.BindingName);
                    if (importStatement.ImportedMembers is not null)
                    {
                        foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                        {
                            names.Add(memberName);
                        }
                    }
                    break;

                case AssignmentStatementSyntax assignment:
                    names.Add(assignment.Name);
                    break;

                case ChainedAssignmentStatementSyntax chained:
                    foreach (var target in chained.Targets) CollectAssignmentTargetBindings(target, names);
                    break;

                case AnnotatedAssignmentStatementSyntax annotated:
                    names.Add(annotated.Name);
                    break;

                case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                    names.Add(nameTarget.Name);
                    break;

                case UnpackingAssignmentStatementSyntax unpacking:
                    foreach (var target in unpacking.Targets) names.Add(target.Name);
                    break;

                case ForStatementSyntax forStatement:
                    CollectLoopTargetBindings(forStatement.Target, names);
                    break;

                case WithStatementSyntax withStatement:
                    if (withStatement.VariableName is not null) names.Add(withStatement.VariableName);
                    break;

                case TryStatementSyntax tryStatement:
                    if (tryStatement.ExceptionVariableName is not null) names.Add(tryStatement.ExceptionVariableName);
                    break;

                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        CollectPatternBindings(matchCase.Pattern, names);
                    }
                    break;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    names.Add(functionDefinition.Name);
                    continue;

                case ClassDefinitionStatementSyntax classDefinition:
                    names.Add(classDefinition.Name);
                    continue;

                case ExpressionStatementSyntax expressionStatement:
                    CollectExpressionBindings(expressionStatement.Expression, names);
                    break;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                CollectLocalBindings(body, names);
            }
        }
    }

    private static void CollectAssignmentTargetBindings(AssignmentTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                names.Add(name.Name);
                break;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                foreach (var nestedTarget in unpacking.Targets) names.Add(nestedTarget.Name);
                break;
        }
    }

    private static void CollectLoopTargetBindings(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectLoopTargetBindings(item, names);
                break;
        }
    }

    private static void CollectPatternBindings(PatternSyntax pattern, HashSet<string> names)
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

    private static void CollectExpressionBindings(ExpressionSyntax expression, HashSet<string> names)
    {
        if (expression is LambdaExpressionSyntax)
        {
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
