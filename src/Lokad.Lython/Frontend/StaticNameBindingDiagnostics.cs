namespace Lokad.Lython.Frontend;

internal static class StaticNameBindingDiagnostics
{
    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeNestedFunctionDefinitions(context.Script.Statements, context);
    }

    private static void AnalyzeNestedFunctionDefinitions(IReadOnlyList<StatementSyntax> statements, StaticAnalysisContext context)
    {
        foreach (var statement in statements)
        {
            if (statement is FunctionDefinitionStatementSyntax functionDefinition)
            {
                AnalyzeFunctionDefinition(functionDefinition, context);
                continue;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                AnalyzeNestedFunctionDefinitions(body, context);
            }
        }
    }

    private static void AnalyzeFunctionDefinition(FunctionDefinitionStatementSyntax functionDefinition, StaticAnalysisContext context)
    {
        foreach (var parameter in functionDefinition.Parameters)
        {
            if (parameter.Annotation is not null)
            {
                AnalyzeExpressionForNestedFunctions(parameter.Annotation, context);
            }

            if (parameter.DefaultValue is not null)
            {
                AnalyzeExpressionForNestedFunctions(parameter.DefaultValue, context);
            }
        }

        foreach (var decorator in functionDefinition.Decorators)
        {
            AnalyzeExpressionForNestedFunctions(decorator, context);
        }

        var scopeFacts = ScopeDirectiveFactsCollector.ForFunction(functionDefinition);
        var localNames = new HashSet<string>(scopeFacts.LocalNames, StringComparer.Ordinal);

        var maybeAssigned = new HashSet<string>(
            functionDefinition.Parameters
                .Select(static parameter => parameter.Name)
                .Where(name => !scopeFacts.GlobalNames.Contains(name) && !scopeFacts.NonlocalNames.Contains(name)),
            StringComparer.Ordinal);
        AnalyzeStatements(functionDefinition.Body, context, localNames, maybeAssigned);
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
        {
            AnalyzeExpression(expression, context, localNames, maybeAssigned);
        }

        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                maybeAssigned.Add(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                    {
                        maybeAssigned.Add(memberName);
                    }
                }
                break;

            case AssignmentStatementSyntax assignment:
                maybeAssigned.Add(assignment.Name);
                break;

            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets)
                {
                    AddAssignmentTarget(target, maybeAssigned);
                }
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                if (annotated.Expression is not null)
                {
                    maybeAssigned.Add(annotated.Name);
                }
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                if (augmented.Target is NameAssignmentTargetSyntax augmentedName)
                {
                    AnalyzeLocalRead(augmentedName.Name, augmentedName.Span, context, localNames, maybeAssigned);
                    maybeAssigned.Add(augmentedName.Name);
                }
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets)
                {
                    maybeAssigned.Add(target.Name);
                }
                break;

            case WithStatementSyntax withStatement:
                if (withStatement.VariableName is not null)
                {
                    maybeAssigned.Add(withStatement.VariableName);
                }
                AnalyzeStatements(withStatement.Body, context, localNames, maybeAssigned);
                break;

            case IfStatementSyntax ifStatement:
                {
                    // This is deliberately a may-assignment analysis: unioning branch
                    // results avoids claiming a name is certainly unbound after a branch.
                    var thenAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(ifStatement.ThenStatements, context, localNames, thenAssigned);
                    var elseAssigned = Clone(maybeAssigned);
                    if (ifStatement.ElseStatements is not null)
                    {
                        AnalyzeStatements(ifStatement.ElseStatements, context, localNames, elseAssigned);
                    }
                    maybeAssigned.UnionWith(thenAssigned);
                    maybeAssigned.UnionWith(elseAssigned);
                    break;
                }

            case ForStatementSyntax forStatement:
                {
                    var bodyAssigned = Clone(maybeAssigned);
                    AddLoopTarget(forStatement.Target, bodyAssigned);
                    AnalyzeStatements(forStatement.Body, context, localNames, bodyAssigned);
                    maybeAssigned.UnionWith(bodyAssigned);
                    if (forStatement.ElseStatements is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(forStatement.ElseStatements, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    break;
                }

            case WhileStatementSyntax whileStatement:
                {
                    var bodyAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(whileStatement.Body, context, localNames, bodyAssigned);
                    maybeAssigned.UnionWith(bodyAssigned);
                    if (whileStatement.ElseStatements is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(whileStatement.ElseStatements, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    break;
                }

            case MatchStatementSyntax matchStatement:
                {
                    var unionAssigned = Clone(maybeAssigned);
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        var caseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(matchCase.Body, context, localNames, caseAssigned);
                        unionAssigned.UnionWith(caseAssigned);
                    }
                    maybeAssigned.UnionWith(unionAssigned);
                    break;
                }

            case DeleteStatementSyntax deleteStatement:
                if (deleteStatement.Target is IdentifierExpressionSyntax identifier)
                {
                    maybeAssigned.Remove(identifier.Name);
                }
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                maybeAssigned.Add(functionDefinition.Name);
                AnalyzeFunctionDefinition(functionDefinition, context);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                maybeAssigned.Add(classDefinition.Name);
                AnalyzeNestedFunctionDefinitions(classDefinition.Body, context);
                break;

            case TryStatementSyntax tryStatement:
                {
                    var tryAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(tryStatement.TryBody, context, localNames, tryAssigned);
                    maybeAssigned.UnionWith(tryAssigned);
                    if (tryStatement.ExceptBody is not null)
                    {
                        var exceptAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(tryStatement.ExceptBody, context, localNames, exceptAssigned);
                        maybeAssigned.UnionWith(exceptAssigned);
                    }
                    if (tryStatement.ElseBody is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(tryStatement.ElseBody, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    if (tryStatement.FinallyBody is not null)
                    {
                        AnalyzeStatements(tryStatement.FinallyBody, context, localNames, maybeAssigned);
                    }
                    break;
                }
        }

        static void AddAssignmentTarget(AssignmentTargetSyntax target, HashSet<string> maybeAssigned)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax nameTarget:
                    maybeAssigned.Add(nameTarget.Name);
                    break;
                case UnpackingAssignmentTargetGroupSyntax unpacking:
                    foreach (var nested in unpacking.Targets)
                    {
                        maybeAssigned.Add(nested.Name);
                    }
                    break;
            }
        }
    }

    private static void AnalyzeExpression(
        ExpressionSyntax expression,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                AnalyzeLocalRead(identifier.Name, identifier.Span, context, localNames, maybeAssigned);
                break;

            case FormattedStringExpressionSyntax formatted:
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    AnalyzeExpression(nestedExpression, context, localNames, maybeAssigned);
                }
                break;

            case ListLiteralExpressionSyntax list:
                AnalyzeExpressions(list.Items, context, localNames, maybeAssigned);
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                AnalyzeExpression(listComprehension.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(listComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case GeneratorExpressionSyntax generator:
                AnalyzeExpression(generator.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(generator.Clauses, context, localNames, maybeAssigned);
                break;

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    AnalyzeExpression(item.Key, context, localNames, maybeAssigned);
                    if (!item.IsUnpacking)
                    {
                        AnalyzeExpression(item.Value, context, localNames, maybeAssigned);
                    }
                }
                break;

            case SetLiteralExpressionSyntax set:
                AnalyzeExpressions(set.Items, context, localNames, maybeAssigned);
                break;

            case SetComprehensionExpressionSyntax setComprehension:
                AnalyzeExpression(setComprehension.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(setComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                AnalyzeExpression(dictComprehension.KeyExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeExpression(dictComprehension.ValueExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(dictComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case TupleLiteralExpressionSyntax tuple:
                AnalyzeExpressions(tuple.Items, context, localNames, maybeAssigned);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                AnalyzeExpression(parenthesized.Inner, context, localNames, maybeAssigned);
                break;

            case MemberExpressionSyntax member:
                AnalyzeExpression(member.Target, context, localNames, maybeAssigned);
                break;

            case CallExpressionSyntax call:
                AnalyzeExpression(call.Target, context, localNames, maybeAssigned);
                foreach (var argument in call.Arguments)
                {
                    AnalyzeExpression(argument.Expression, context, localNames, maybeAssigned);
                }
                break;

            case SubscriptExpressionSyntax subscript:
                AnalyzeExpression(subscript.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(subscript.Index, context, localNames, maybeAssigned);
                break;

            case SliceExpressionSyntax slice:
                AnalyzeExpression(slice.Target, context, localNames, maybeAssigned);
                if (slice.Start is not null) AnalyzeExpression(slice.Start, context, localNames, maybeAssigned);
                if (slice.End is not null) AnalyzeExpression(slice.End, context, localNames, maybeAssigned);
                if (slice.Step is not null) AnalyzeExpression(slice.Step, context, localNames, maybeAssigned);
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeExpression(binary.Left, context, localNames, maybeAssigned);
                AnalyzeExpression(binary.Right, context, localNames, maybeAssigned);
                break;

            case ChainedComparisonExpressionSyntax chained:
                AnalyzeExpressions(chained.Operands, context, localNames, maybeAssigned);
                break;

            case UnaryExpressionSyntax unary:
                AnalyzeExpression(unary.Operand, context, localNames, maybeAssigned);
                break;

            case ConditionalExpressionSyntax conditional:
                AnalyzeExpression(conditional.Condition, context, localNames, maybeAssigned);
                AnalyzeExpression(conditional.Consequent, context, localNames, maybeAssigned);
                AnalyzeExpression(conditional.Alternative, context, localNames, maybeAssigned);
                break;

            case AssignmentExpressionSyntax assignment:
                AnalyzeExpression(assignment.Expression, context, localNames, maybeAssigned);
                maybeAssigned.Add(assignment.Name);
                break;

            case LambdaExpressionSyntax lambda:
                {
                    var lambdaLocalNames = ScopeDirectiveFactsCollector.CollectLambdaLocalNames(lambda);
                    var lambdaAssigned = new HashSet<string>(
                        lambda.Parameters.Select(static parameter => parameter.Name),
                        StringComparer.Ordinal);
                    AnalyzeExpression(lambda.Body, context, lambdaLocalNames, lambdaAssigned);
                    break;
                }
        }
    }

    private static void AnalyzeExpressions(
        IReadOnlyList<ExpressionSyntax> expressions,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var expression in expressions)
        {
            AnalyzeExpression(expression, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeExpressions(
        IReadOnlyList<CollectionDisplayItemSyntax> items,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var item in items)
        {
            AnalyzeExpression(item.Expression, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeComprehensionClauses(
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        var comprehensionAssigned = Clone(maybeAssigned);
        foreach (var clause in clauses)
        {
            AnalyzeExpression(clause.Iterable, context, localNames, comprehensionAssigned);
            AddLoopTarget(clause.Target, comprehensionAssigned);
            if (clause.Condition is not null)
            {
                AnalyzeExpression(clause.Condition, context, localNames, comprehensionAssigned);
            }
        }
    }

    private static void AnalyzeLocalRead(
        string name,
        LythonSourceSpan span,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        if (localNames.Contains(name) && !maybeAssigned.Contains(name))
        {
            StaticDiagnosticSink.AddError(
                context,
                "LA3146",
                $"Local variable '{name}' is read before it is assigned.",
                span,
                new StaticDiagnosticProof("binding", name, "function-local name is read before any assignment path"));
        }
    }

    private static void AnalyzeExpressionForNestedFunctions(ExpressionSyntax expression, StaticAnalysisContext context)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            var localNames = ScopeDirectiveFactsCollector.CollectLambdaLocalNames(lambda);
            var maybeAssigned = new HashSet<string>(
                lambda.Parameters.Select(static parameter => parameter.Name),
                StringComparer.Ordinal);
            AnalyzeExpression(lambda.Body, context, localNames, maybeAssigned);
        }
    }

    private static void AddLoopTarget(LoopTargetSyntax target, HashSet<string> maybeAssigned)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                maybeAssigned.Add(nameTarget.Name);
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items) AddLoopTarget(item, maybeAssigned);
                break;
        }
    }

    private static HashSet<string> Clone(HashSet<string> names)
        => new(names, StringComparer.Ordinal);
}
