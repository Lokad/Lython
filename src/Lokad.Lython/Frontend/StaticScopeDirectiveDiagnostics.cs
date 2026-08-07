namespace Lokad.Lython.Frontend;

internal static class StaticScopeDirectiveDiagnostics
{
    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeStatements(context.Script.Statements, context, [], inClassBody: false);
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        IReadOnlyList<ScopeDirectiveFacts> enclosingFunctions,
        bool inClassBody)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ScopeDirectiveStatementSyntax directive:
                    if (directive.Kind == ScopeDirectiveKind.Nonlocal && enclosingFunctions.Count == 0)
                    {
                        context.AddError("LA3201", "`nonlocal` is only valid inside a nested function.", directive.Span);
                    }

                    if (inClassBody)
                    {
                        context.AddError("LA3202", "Scope directives inside class bodies are not supported by Lython.", directive.Span);
                    }
                    break;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    AnalyzeFunction(functionDefinition, context, enclosingFunctions);
                    break;

                case ClassDefinitionStatementSyntax classDefinition:
                    AnalyzeStatements(classDefinition.Body, context, enclosingFunctions, inClassBody: true);
                    break;

                case IfStatementSyntax ifStatement:
                    AnalyzeStatements(ifStatement.ThenStatements, context, enclosingFunctions, inClassBody);
                    if (ifStatement.ElseStatements is not null) AnalyzeStatements(ifStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case ForStatementSyntax forStatement:
                    AnalyzeStatements(forStatement.Body, context, enclosingFunctions, inClassBody);
                    if (forStatement.ElseStatements is not null) AnalyzeStatements(forStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case WhileStatementSyntax whileStatement:
                    AnalyzeStatements(whileStatement.Body, context, enclosingFunctions, inClassBody);
                    if (whileStatement.ElseStatements is not null) AnalyzeStatements(whileStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case WithStatementSyntax withStatement:
                    AnalyzeStatements(withStatement.Body, context, enclosingFunctions, inClassBody);
                    break;

                case TryStatementSyntax tryStatement:
                    AnalyzeStatements(tryStatement.TryBody, context, enclosingFunctions, inClassBody);
                    if (tryStatement.ExceptBody is not null) AnalyzeStatements(tryStatement.ExceptBody, context, enclosingFunctions, inClassBody);
                    if (tryStatement.ElseBody is not null) AnalyzeStatements(tryStatement.ElseBody, context, enclosingFunctions, inClassBody);
                    if (tryStatement.FinallyBody is not null) AnalyzeStatements(tryStatement.FinallyBody, context, enclosingFunctions, inClassBody);
                    break;

                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        AnalyzeStatements(matchCase.Body, context, enclosingFunctions, inClassBody);
                    }
                    break;
            }
        }
    }

    private static void AnalyzeFunction(
        FunctionDefinitionStatementSyntax functionDefinition,
        StaticAnalysisContext context,
        IReadOnlyList<ScopeDirectiveFacts> enclosingFunctions)
    {
        var facts = ScopeDirectiveFactsCollector.ForFunction(functionDefinition);
        foreach (var name in facts.GlobalNames.Intersect(facts.NonlocalNames, StringComparer.Ordinal))
        {
            context.AddError("LA3203", $"Name '{name}' cannot be declared both global and nonlocal.", functionDefinition.Span);
        }

        var parameterNames = new HashSet<string>(
            functionDefinition.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        foreach (var name in facts.GlobalNames.Concat(facts.NonlocalNames))
        {
            if (parameterNames.Contains(name))
            {
                context.AddError("LA3204", $"Parameter '{name}' cannot also be declared global or nonlocal.", functionDefinition.Span);
            }
        }

        foreach (var name in facts.NonlocalNames)
        {
            if (!enclosingFunctions.Any(scope => scope.LocalNames.Contains(name)))
            {
                context.AddError("LA3205", $"No enclosing function binding exists for nonlocal name '{name}'.", functionDefinition.Span);
            }
        }

        AnalyzeUseBeforeDirective(functionDefinition, facts, context);

        var nested = enclosingFunctions.Concat([facts]).ToArray();
        AnalyzeStatements(functionDefinition.Body, context, nested, inClassBody: false);
    }

    private static void AnalyzeUseBeforeDirective(
        FunctionDefinitionStatementSyntax functionDefinition,
        ScopeDirectiveFacts facts,
        StaticAnalysisContext context)
    {
        var directiveNames = new HashSet<string>(facts.GlobalNames.Concat(facts.NonlocalNames), StringComparer.Ordinal);
        if (directiveNames.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(
            functionDefinition.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        AnalyzeUseBeforeDirective(functionDefinition.Body, directiveNames, seen, context);
    }

    private static void AnalyzeUseBeforeDirective(
        IReadOnlyList<StatementSyntax> statements,
        HashSet<string> directiveNames,
        HashSet<string> seen,
        StaticAnalysisContext context)
    {
        foreach (var statement in statements)
        {
            if (statement is ScopeDirectiveStatementSyntax directive)
            {
                foreach (var name in directive.Names)
                {
                    if (directiveNames.Contains(name) && seen.Contains(name))
                    {
                        context.AddError("LA3206", $"Name '{name}' is used or assigned before its scope directive.", directive.Span);
                    }
                }
                continue;
            }

            CollectSeenNames(statement, seen);
        }
    }

    private static void CollectSeenNames(StatementSyntax statement, HashSet<string> names)
    {
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                names.Add(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement)) names.Add(memberName);
                }
                break;
            case AssignmentStatementSyntax assignment:
                names.Add(assignment.Name);
                CollectSeenNames(assignment.Expression, names);
                break;
            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets) CollectSeenNames(target, names);
                CollectSeenNames(chained.Expression, names);
                break;
            case AnnotatedAssignmentStatementSyntax annotated:
                names.Add(annotated.Name);
                CollectSeenNames(annotated.Annotation, names);
                if (annotated.Expression is not null) CollectSeenNames(annotated.Expression, names);
                break;
            case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                names.Add(nameTarget.Name);
                break;
            case AugmentedAssignmentStatementSyntax augmented:
                CollectSeenNames(augmented.Target, names);
                CollectSeenNames(augmented.Expression, names);
                break;
            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets) names.Add(target.Name);
                CollectSeenNames(unpacking.Expression, names);
                break;
            case SubscriptAssignmentStatementSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                CollectSeenNames(subscript.Expression, names);
                break;
            case SliceAssignmentStatementSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                CollectSeenNames(slice.Expression, names);
                break;
            case MemberAssignmentStatementSyntax member:
                CollectSeenNames(member.Target, names);
                CollectSeenNames(member.Expression, names);
                break;
            case FunctionDefinitionStatementSyntax functionDefinition:
                names.Add(functionDefinition.Name);
                break;
            case ClassDefinitionStatementSyntax classDefinition:
                names.Add(classDefinition.Name);
                break;
            case ForStatementSyntax forStatement:
                CollectSeenNames(forStatement.Target, names);
                CollectSeenNames(forStatement.Iterable, names);
                AnalyzeNestedSeen(forStatement.Body, names);
                if (forStatement.ElseStatements is not null) AnalyzeNestedSeen(forStatement.ElseStatements, names);
                break;
            case WithStatementSyntax withStatement:
                if (withStatement.VariableName is not null) names.Add(withStatement.VariableName);
                CollectSeenNames(withStatement.ContextExpression, names);
                AnalyzeNestedSeen(withStatement.Body, names);
                break;
            case IfStatementSyntax ifStatement:
                CollectSeenNames(ifStatement.Condition, names);
                AnalyzeNestedSeen(ifStatement.ThenStatements, names);
                if (ifStatement.ElseStatements is not null) AnalyzeNestedSeen(ifStatement.ElseStatements, names);
                break;
            case WhileStatementSyntax whileStatement:
                CollectSeenNames(whileStatement.Condition, names);
                AnalyzeNestedSeen(whileStatement.Body, names);
                if (whileStatement.ElseStatements is not null) AnalyzeNestedSeen(whileStatement.ElseStatements, names);
                break;
            case ExpressionStatementSyntax expressionStatement:
                CollectSeenNames(expressionStatement.Expression, names);
                break;
            case MatchStatementSyntax matchStatement:
                CollectSeenNames(matchStatement.Subject, names);
                foreach (var matchCase in matchStatement.Cases)
                {
                    CollectPatternNames(matchCase.Pattern, names);
                    if (matchCase.Guard is not null) CollectSeenNames(matchCase.Guard, names);
                    AnalyzeNestedSeen(matchCase.Body, names);
                }
                break;
            case AssertStatementSyntax assertStatement:
                CollectSeenNames(assertStatement.Condition, names);
                if (assertStatement.Message is not null) CollectSeenNames(assertStatement.Message, names);
                break;
            case DeleteStatementSyntax deleteStatement:
                CollectSeenNames(deleteStatement.Target, names);
                break;
            case ReturnStatementSyntax { Expression: { } expression }:
                CollectSeenNames(expression, names);
                break;
            case RaiseStatementSyntax raiseStatement:
                CollectSeenNames(raiseStatement.Expression, names);
                break;
            case TryStatementSyntax tryStatement:
                AnalyzeNestedSeen(tryStatement.TryBody, names);
                if (tryStatement.ExceptionVariableName is not null) names.Add(tryStatement.ExceptionVariableName);
                if (tryStatement.ExceptBody is not null) AnalyzeNestedSeen(tryStatement.ExceptBody, names);
                if (tryStatement.ElseBody is not null) AnalyzeNestedSeen(tryStatement.ElseBody, names);
                if (tryStatement.FinallyBody is not null) AnalyzeNestedSeen(tryStatement.FinallyBody, names);
                break;
        }
    }

    private static void AnalyzeNestedSeen(IReadOnlyList<StatementSyntax> statements, HashSet<string> names)
    {
        foreach (var statement in statements)
        {
            if (statement is not FunctionDefinitionStatementSyntax and not ClassDefinitionStatementSyntax)
            {
                CollectSeenNames(statement, names);
            }
        }
    }

    private static void CollectSeenNames(AssignmentTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                names.Add(name.Name);
                break;
            case UnpackingAssignmentTargetGroupSyntax group:
                foreach (var nested in group.Targets) names.Add(nested.Name);
                break;
            case SubscriptAssignmentTargetSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                break;
            case SliceAssignmentTargetSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                break;
            case MemberAssignmentTargetSyntax member:
                CollectSeenNames(member.Target, names);
                break;
        }
    }

    private static void CollectSeenNames(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectSeenNames(item, names);
                break;
        }
    }

    private static void CollectSeenNames(ExpressionSyntax expression, HashSet<string> names)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                names.Add(identifier.Name);
                break;
            case AssignmentExpressionSyntax assignment:
                names.Add(assignment.Name);
                CollectSeenNames(assignment.Expression, names);
                break;
            case FormattedStringExpressionSyntax formatted:
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    CollectSeenNames(nestedExpression, names);
                }
                break;
            case ListLiteralExpressionSyntax list:
                foreach (var item in list.Items) CollectSeenNames(item, names);
                break;
            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items) CollectSeenNames(item, names);
                break;
            case SetLiteralExpressionSyntax set:
                foreach (var item in set.Items) CollectSeenNames(item, names);
                break;
            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    CollectSeenNames(item.Key, names);
                    if (!item.IsUnpacking)
                    {
                        CollectSeenNames(item.Value, names);
                    }
                }
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                CollectSeenNames(parenthesized.Inner, names);
                break;
            case BinaryExpressionSyntax binary:
                CollectSeenNames(binary.Left, names);
                CollectSeenNames(binary.Right, names);
                break;
            case UnaryExpressionSyntax unary:
                CollectSeenNames(unary.Operand, names);
                break;
            case CallExpressionSyntax call:
                CollectSeenNames(call.Target, names);
                foreach (var argument in call.Arguments) CollectSeenNames(argument.Expression, names);
                break;
            case MemberExpressionSyntax member:
                CollectSeenNames(member.Target, names);
                break;
            case SubscriptExpressionSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                break;
            case SliceExpressionSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                break;
            case ChainedComparisonExpressionSyntax chained:
                foreach (var operand in chained.Operands) CollectSeenNames(operand, names);
                break;
            case ConditionalExpressionSyntax conditional:
                CollectSeenNames(conditional.Condition, names);
                CollectSeenNames(conditional.Consequent, names);
                CollectSeenNames(conditional.Alternative, names);
                break;
            case ListComprehensionExpressionSyntax listComprehension:
                CollectSeenNames(listComprehension.ItemExpression, names);
                CollectComprehensionNames(listComprehension.Clauses, names);
                break;
            case GeneratorExpressionSyntax generator:
                CollectSeenNames(generator.ItemExpression, names);
                CollectComprehensionNames(generator.Clauses, names);
                break;
            case SetComprehensionExpressionSyntax setComprehension:
                CollectSeenNames(setComprehension.ItemExpression, names);
                CollectComprehensionNames(setComprehension.Clauses, names);
                break;
            case DictComprehensionExpressionSyntax dictComprehension:
                CollectSeenNames(dictComprehension.KeyExpression, names);
                CollectSeenNames(dictComprehension.ValueExpression, names);
                CollectComprehensionNames(dictComprehension.Clauses, names);
                break;
        }
    }

    private static void CollectComprehensionNames(IReadOnlyList<ComprehensionClauseSyntax> clauses, HashSet<string> names)
    {
        foreach (var clause in clauses)
        {
            CollectSeenNames(clause.Target, names);
            CollectSeenNames(clause.Iterable, names);
            if (clause.Condition is not null) CollectSeenNames(clause.Condition, names);
        }
    }

    private static void CollectPatternNames(PatternSyntax pattern, HashSet<string> names)
    {
        switch (pattern)
        {
            case MatchCapturePatternSyntax capture:
                names.Add(capture.Name);
                break;
            case MatchSequencePatternSyntax sequence:
                foreach (var item in sequence.Items) CollectPatternNames(item, names);
                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items) CollectPatternNames(item.Pattern, names);
                if (mapping.RestName is not null) names.Add(mapping.RestName);
                break;
            case MatchClassPatternSyntax classPattern:
                foreach (var item in classPattern.PositionalPatterns) CollectPatternNames(item, names);
                foreach (var item in classPattern.KeywordPatterns) CollectPatternNames(item.Pattern, names);
                break;
            case MatchStarPatternSyntax { Name: { } name }:
                names.Add(name);
                break;
            case MatchAsPatternSyntax asPattern:
                CollectPatternNames(asPattern.Pattern, names);
                names.Add(asPattern.Name);
                break;
            case MatchOrPatternSyntax orPattern:
                foreach (var item in orPattern.Patterns) CollectPatternNames(item, names);
                break;
        }
    }
}
