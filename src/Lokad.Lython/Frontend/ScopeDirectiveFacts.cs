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
            switch (statement)
            {
                case ScopeDirectiveStatementSyntax:
                    return true;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    if (ContainsScopeDirective(functionDefinition.Body))
                    {
                        return true;
                    }
                    break;

                case ClassDefinitionStatementSyntax classDefinition:
                    if (ContainsScopeDirective(classDefinition.Body))
                    {
                        return true;
                    }
                    break;

                case IfStatementSyntax ifStatement:
                    if (ContainsScopeDirective(ifStatement.ThenStatements) ||
                        ifStatement.ElseStatements is not null && ContainsScopeDirective(ifStatement.ElseStatements))
                    {
                        return true;
                    }
                    break;

                case ForStatementSyntax forStatement:
                    if (ContainsScopeDirective(forStatement.Body) ||
                        forStatement.ElseStatements is not null && ContainsScopeDirective(forStatement.ElseStatements))
                    {
                        return true;
                    }
                    break;

                case WhileStatementSyntax whileStatement:
                    if (ContainsScopeDirective(whileStatement.Body) ||
                        whileStatement.ElseStatements is not null && ContainsScopeDirective(whileStatement.ElseStatements))
                    {
                        return true;
                    }
                    break;

                case WithStatementSyntax withStatement:
                    if (ContainsScopeDirective(withStatement.Body))
                    {
                        return true;
                    }
                    break;

                case TryStatementSyntax tryStatement:
                    if (ContainsScopeDirective(tryStatement.TryBody) ||
                        tryStatement.ExceptBody is not null && ContainsScopeDirective(tryStatement.ExceptBody) ||
                        tryStatement.ElseBody is not null && ContainsScopeDirective(tryStatement.ElseBody) ||
                        tryStatement.FinallyBody is not null && ContainsScopeDirective(tryStatement.FinallyBody))
                    {
                        return true;
                    }
                    break;

                case MatchStatementSyntax matchStatement:
                    if (matchStatement.Cases.Any(matchCase => ContainsScopeDirective(matchCase.Body)))
                    {
                        return true;
                    }
                    break;
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
            switch (statement)
            {
                case ScopeDirectiveStatementSyntax directive:
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
                    break;

                case IfStatementSyntax ifStatement:
                    CollectDirectives(ifStatement.ThenStatements, globalNames, nonlocalNames);
                    if (ifStatement.ElseStatements is not null) CollectDirectives(ifStatement.ElseStatements, globalNames, nonlocalNames);
                    break;

                case ForStatementSyntax forStatement:
                    CollectDirectives(forStatement.Body, globalNames, nonlocalNames);
                    if (forStatement.ElseStatements is not null) CollectDirectives(forStatement.ElseStatements, globalNames, nonlocalNames);
                    break;

                case WhileStatementSyntax whileStatement:
                    CollectDirectives(whileStatement.Body, globalNames, nonlocalNames);
                    if (whileStatement.ElseStatements is not null) CollectDirectives(whileStatement.ElseStatements, globalNames, nonlocalNames);
                    break;

                case WithStatementSyntax withStatement:
                    CollectDirectives(withStatement.Body, globalNames, nonlocalNames);
                    break;

                case TryStatementSyntax tryStatement:
                    CollectDirectives(tryStatement.TryBody, globalNames, nonlocalNames);
                    if (tryStatement.ExceptBody is not null) CollectDirectives(tryStatement.ExceptBody, globalNames, nonlocalNames);
                    if (tryStatement.ElseBody is not null) CollectDirectives(tryStatement.ElseBody, globalNames, nonlocalNames);
                    if (tryStatement.FinallyBody is not null) CollectDirectives(tryStatement.FinallyBody, globalNames, nonlocalNames);
                    break;

                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        CollectDirectives(matchCase.Body, globalNames, nonlocalNames);
                    }
                    break;
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
                        foreach (var member in importStatement.ImportedMembers)
                        {
                            names.Add(member.BindingName);
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
                    CollectLocalBindings(forStatement.Body, names);
                    if (forStatement.ElseStatements is not null) CollectLocalBindings(forStatement.ElseStatements, names);
                    break;

                case WithStatementSyntax withStatement:
                    if (withStatement.VariableName is not null) names.Add(withStatement.VariableName);
                    CollectLocalBindings(withStatement.Body, names);
                    break;

                case TryStatementSyntax tryStatement:
                    if (tryStatement.ExceptionVariableName is not null) names.Add(tryStatement.ExceptionVariableName);
                    CollectLocalBindings(tryStatement.TryBody, names);
                    if (tryStatement.ExceptBody is not null) CollectLocalBindings(tryStatement.ExceptBody, names);
                    if (tryStatement.ElseBody is not null) CollectLocalBindings(tryStatement.ElseBody, names);
                    if (tryStatement.FinallyBody is not null) CollectLocalBindings(tryStatement.FinallyBody, names);
                    break;

                case IfStatementSyntax ifStatement:
                    CollectLocalBindings(ifStatement.ThenStatements, names);
                    if (ifStatement.ElseStatements is not null) CollectLocalBindings(ifStatement.ElseStatements, names);
                    break;

                case WhileStatementSyntax whileStatement:
                    CollectLocalBindings(whileStatement.Body, names);
                    if (whileStatement.ElseStatements is not null) CollectLocalBindings(whileStatement.ElseStatements, names);
                    break;

                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        CollectPatternBindings(matchCase.Pattern, names);
                        CollectLocalBindings(matchCase.Body, names);
                    }
                    break;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    names.Add(functionDefinition.Name);
                    break;

                case ClassDefinitionStatementSyntax classDefinition:
                    names.Add(classDefinition.Name);
                    break;

                case ExpressionStatementSyntax expressionStatement:
                    CollectExpressionBindings(expressionStatement.Expression, names);
                    break;
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
        switch (expression)
        {
            case AssignmentExpressionSyntax assignment:
                names.Add(assignment.Name);
                CollectExpressionBindings(assignment.Expression, names);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                CollectExpressionBindings(parenthesized.Inner, names);
                break;
            case ConditionalExpressionSyntax conditional:
                CollectExpressionBindings(conditional.Condition, names);
                CollectExpressionBindings(conditional.Consequent, names);
                CollectExpressionBindings(conditional.Alternative, names);
                break;
            case BinaryExpressionSyntax binary:
                CollectExpressionBindings(binary.Left, names);
                CollectExpressionBindings(binary.Right, names);
                break;
            case ChainedComparisonExpressionSyntax chained:
                foreach (var operand in chained.Operands) CollectExpressionBindings(operand, names);
                break;
            case UnaryExpressionSyntax unary:
                CollectExpressionBindings(unary.Operand, names);
                break;
            case CallExpressionSyntax call:
                CollectExpressionBindings(call.Target, names);
                foreach (var argument in call.Arguments) CollectExpressionBindings(argument.Expression, names);
                break;
            case MemberExpressionSyntax member:
                CollectExpressionBindings(member.Target, names);
                break;
            case SubscriptExpressionSyntax subscript:
                CollectExpressionBindings(subscript.Target, names);
                CollectExpressionBindings(subscript.Index, names);
                break;
            case SliceExpressionSyntax slice:
                CollectExpressionBindings(slice.Target, names);
                if (slice.Start is not null) CollectExpressionBindings(slice.Start, names);
                if (slice.End is not null) CollectExpressionBindings(slice.End, names);
                if (slice.Step is not null) CollectExpressionBindings(slice.Step, names);
                break;
            case ListLiteralExpressionSyntax list:
                foreach (var item in list.Items) CollectExpressionBindings(item, names);
                break;
            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items) CollectExpressionBindings(item, names);
                break;
            case SetLiteralExpressionSyntax set:
                foreach (var item in set.Items) CollectExpressionBindings(item, names);
                break;
            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    CollectExpressionBindings(item.Key, names);
                    CollectExpressionBindings(item.Value, names);
                }
                break;
        }
    }
}
