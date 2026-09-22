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
            if (statement is MatchStatementSyntax matchGuardOwner &&
                IsMatchCaseGuard(matchGuardOwner, expression))
            {
                // Case guards run after their own pattern binds, so they are
                // analyzed per case below with the pattern names in scope.
                continue;
            }

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
                if (annotated.Expression is not null && annotated.Target is NameAssignmentTargetSyntax name)
                {
                    maybeAssigned.Add(name.Name);
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
                    foreach (var boundName in UnpackingTargetNames(target))
                    {
                        maybeAssigned.Add(boundName);
                    }
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
                        ScopeDirectiveFactsCollector.CollectPatternBindings(matchCase.Pattern, caseAssigned);
                        if (matchCase.Guard is not null)
                        {
                            AnalyzeExpression(matchCase.Guard, context, localNames, caseAssigned);
                        }

                        AnalyzeStatements(matchCase.Body, context, localNames, caseAssigned);
                        unionAssigned.UnionWith(caseAssigned);
                    }
                    maybeAssigned.UnionWith(unionAssigned);
                    break;
                }

            case DeleteStatementSyntax deleteStatement:
                foreach (var deletedName in DeleteTargetNames(deleteStatement.Target))
                {
                    maybeAssigned.Remove(deletedName);
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
                    foreach (var exceptClause in tryStatement.ExceptClauses)
                    {
                        var exceptAssigned = Clone(maybeAssigned);
                        if (exceptClause.ExceptionVariableName is not null)
                        {
                            exceptAssigned.Add(exceptClause.ExceptionVariableName);
                        }

                        AnalyzeStatements(exceptClause.Body, context, localNames, exceptAssigned);
                        if (exceptClause.ExceptionVariableName is not null &&
                            !maybeAssigned.Contains(exceptClause.ExceptionVariableName))
                        {
                            // The handler variable is deleted when the handler
                            // exits like CPython, so it must not leak outward.
                            exceptAssigned.Remove(exceptClause.ExceptionVariableName);
                        }

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
                        foreach (var nestedName in UnpackingTargetNames(nested))
                        {
                            maybeAssigned.Add(nestedName);
                        }
                    }
                    break;
            }
        }

        static IEnumerable<string> UnpackingTargetNames(UnpackingTargetSyntax target)
        {
            if (target is UnpackingNameTargetSyntax name)
            {
                yield return name.Name;
                yield break;
            }

            if (target is UnpackingNestedTargetSyntax nested)
            {
                foreach (var nestedItem in nested.Items)
                {
                    foreach (var nestedName in UnpackingTargetNames(nestedItem))
                    {
                        yield return nestedName;
                    }
                }
            }
        }

        static IEnumerable<string> DeleteTargetNames(ExpressionSyntax target)
        {
            while (target is ParenthesizedExpressionSyntax parenthesized)
            {
                target = parenthesized.Inner;
            }

            if (target is IdentifierExpressionSyntax identifier)
            {
                yield return identifier.Name;
                yield break;
            }

            var items = target switch
            {
                TupleLiteralExpressionSyntax tuple => tuple.Items,
                ListLiteralExpressionSyntax list => list.Items,
                _ => null,
            };

            if (items is null)
            {
                yield break;
            }

            foreach (var item in items)
            {
                if (item.IsUnpacking)
                {
                    continue;
                }

                foreach (var nestedName in DeleteTargetNames(item.Expression))
                {
                    yield return nestedName;
                }
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
                return;

            case ListComprehensionExpressionSyntax listComprehension:
                var listAssigned = AnalyzeComprehensionClauses(listComprehension.Clauses, context, localNames, maybeAssigned);
                AnalyzeExpression(listComprehension.ItemExpression, context, localNames, listAssigned);
                return;

            case GeneratorExpressionSyntax generator:
                var generatorAssigned = AnalyzeComprehensionClauses(generator.Clauses, context, localNames, maybeAssigned);
                AnalyzeExpression(generator.ItemExpression, context, localNames, generatorAssigned);
                return;

            case SetComprehensionExpressionSyntax setComprehension:
                var setAssigned = AnalyzeComprehensionClauses(setComprehension.Clauses, context, localNames, maybeAssigned);
                AnalyzeExpression(setComprehension.ItemExpression, context, localNames, setAssigned);
                return;

            case DictComprehensionExpressionSyntax dictComprehension:
                var dictAssigned = AnalyzeComprehensionClauses(dictComprehension.Clauses, context, localNames, maybeAssigned);
                AnalyzeExpression(dictComprehension.KeyExpression, context, localNames, dictAssigned);
                AnalyzeExpression(dictComprehension.ValueExpression, context, localNames, dictAssigned);
                return;

            case AssignmentExpressionSyntax assignment:
                AnalyzeExpression(assignment.Expression, context, localNames, maybeAssigned);
                maybeAssigned.Add(assignment.Name);
                return;

            case LambdaExpressionSyntax lambda:
                var lambdaLocalNames = ScopeDirectiveFactsCollector.CollectLambdaLocalNames(lambda);
                var lambdaAssigned = new HashSet<string>(
                    lambda.Parameters.Select(static parameter => parameter.Name),
                    StringComparer.Ordinal);
                AnalyzeExpression(lambda.Body, context, lambdaLocalNames, lambdaAssigned);
                return;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            AnalyzeExpression(child, context, localNames, maybeAssigned);
        }
    }

    // The first iterable evaluates in the enclosing scope, while later
    // iterables, conditions and the result expressions observe the targets
    // bound by their enclosing clauses. The returned set stays local to the
    // comprehension: targets never leak into the outer assignment facts.
    private static HashSet<string> AnalyzeComprehensionClauses(
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

        return comprehensionAssigned;
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
                span);
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

    private static bool IsMatchCaseGuard(MatchStatementSyntax matchStatement, ExpressionSyntax expression)
    {
        foreach (var matchCase in matchStatement.Cases)
        {
            if (matchCase.Guard is not null && ReferenceEquals(matchCase.Guard, expression))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddLoopTarget(LoopTargetSyntax target, HashSet<string> maybeAssigned)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                maybeAssigned.Add(nameTarget.Name);
                break;
            case LoopStarredTargetSyntax starredTarget:
                maybeAssigned.Add(starredTarget.Name);
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items) AddLoopTarget(item, maybeAssigned);
                break;
        }
    }

    private static HashSet<string> Clone(HashSet<string> names)
        => new(names, StringComparer.Ordinal);
}
