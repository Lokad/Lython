namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeStatements(context.Script.Statements, context.DiagnosticList, new AbstractState());
    }

    private static void AnalyzeStatements(IReadOnlyList<StatementSyntax> statements, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, diagnostics, bindings);
            StaticBindingEngine.UpdateBindings(statement, bindings);
        }
    }

    private static void AnalyzeStatement(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax or
                 ChainedAssignmentStatementSyntax or
                 AnnotatedAssignmentStatementSyntax or
                 SubscriptAssignmentStatementSyntax or
                 SliceAssignmentStatementSyntax or
                 MemberAssignmentStatementSyntax or
                 AugmentedAssignmentStatementSyntax or
                 UnpackingAssignmentStatementSyntax:
                AnalyzeAssignment(statement);
                return;

            case WithStatementSyntax or
                 IfStatementSyntax or
                 ForStatementSyntax or
                 WhileStatementSyntax or
                 MatchStatementSyntax or
                 TryStatementSyntax:
                AnalyzeControlFlow(statement);
                return;

            case FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax:
                AnalyzeDefinition(statement);
                return;

            default:
                AnalyzeSimpleStatement(statement);
                return;
        }

        void AnalyzeAssignment(StatementSyntax assignmentStatement)
        {
            switch (assignmentStatement)
            {
                case AssignmentStatementSyntax assignment:
                    AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                    break;

                case ChainedAssignmentStatementSyntax chained:
                    AnalyzeExpression(chained.Expression, diagnostics, bindings);
                    foreach (var target in chained.Targets)
                    {
                        AnalyzeAssignmentTarget(target, diagnostics, bindings);
                    }
                    break;

                case AnnotatedAssignmentStatementSyntax annotated:
                    AnalyzeExpressionIfPresent(annotated.Expression, diagnostics, bindings);
                    break;

                case SubscriptAssignmentStatementSyntax subscript:
                    AnalyzeExpression(subscript.Target, diagnostics, bindings);
                    AnalyzeExpression(subscript.Index, diagnostics, bindings);
                    AnalyzeExpression(subscript.Expression, diagnostics, bindings);
                    break;

                case SliceAssignmentStatementSyntax slice:
                    AnalyzeExpression(slice.Target, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                    AnalyzeExpression(slice.Expression, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeSliceAssignment(slice, diagnostics, bindings);
                    break;

                case MemberAssignmentStatementSyntax member:
                    AnalyzeExpression(member.Target, diagnostics, bindings);
                    AnalyzeExpression(member.Expression, diagnostics, bindings);
                    break;

                case AugmentedAssignmentStatementSyntax augmented:
                    AnalyzeAssignmentTarget(augmented.Target, diagnostics, bindings);
                    AnalyzeExpression(augmented.Expression, diagnostics, bindings);
                    break;

                case UnpackingAssignmentStatementSyntax unpacking:
                    AnalyzeExpression(unpacking.Expression, diagnostics, bindings);
                    StaticDestructuringDiagnostics.AnalyzeUnpackingTargets(
                        unpacking.Targets,
                        unpacking.Expression,
                        unpacking.Span,
                        diagnostics,
                        bindings);
                    break;
            }
        }

        void AnalyzeControlFlow(StatementSyntax controlFlowStatement)
        {
            switch (controlFlowStatement)
            {
                case WithStatementSyntax withStatement:
                    AnalyzeExpression(withStatement.ContextExpression, diagnostics, bindings);
                    var withBindings = bindings.Clone();
                    if (withStatement.VariableName is not null &&
                        StaticAbstractValueResolver.TryResolve(withStatement.ContextExpression, bindings, out var contextValue) &&
                        contextValue.Kind == AbstractValueKind.TextFileHandle)
                    {
                        withBindings.Set(withStatement.VariableName, contextValue);
                    }

                    AnalyzeStatements(withStatement.Body, diagnostics, withBindings);
                    bindings.ReplaceWith(withBindings);
                    if (withStatement.VariableName is not null)
                    {
                        bindings.Remove(withStatement.VariableName);
                    }
                    break;

                case IfStatementSyntax ifStatement:
                    AnalyzeIf(ifStatement);
                    break;

                case ForStatementSyntax forStatement:
                    AnalyzeExpression(forStatement.Iterable, diagnostics, bindings);
                    StaticIterationDiagnostics.AnalyzeLoopTarget(forStatement.Target, forStatement.Iterable, forStatement.Span, diagnostics, bindings);
                    var forBodyBindings = bindings.Clone();
                    StaticBindingEngine.BindLoopTargetFromIterable(forStatement.Target, forStatement.Iterable, forBodyBindings);
                    AnalyzeStatements(forStatement.Body, diagnostics, forBodyBindings);
                    var forMergedBindings = AbstractState.Merge(bindings, forBodyBindings);
                    if (forStatement.ElseStatements is not null)
                    {
                        var forElseBindings = bindings.Clone();
                        AnalyzeStatements(forStatement.ElseStatements, diagnostics, forElseBindings);
                        forMergedBindings = AbstractState.Merge(forMergedBindings, forElseBindings);
                    }

                    bindings.ReplaceWith(forMergedBindings);
                    break;

                case WhileStatementSyntax whileStatement:
                    AnalyzeExpression(whileStatement.Condition, diagnostics, bindings);
                    var whileBodyBindings = bindings.Clone();
                    if (!TryResolveConditionTruth(whileStatement.Condition, bindings, out var whileTruth) || whileTruth)
                    {
                        StaticConditionRefinements.Apply(whileStatement.Condition, assumedTruth: true, whileBodyBindings);
                        AnalyzeStatements(whileStatement.Body, diagnostics, whileBodyBindings);
                    }
                    var whileMergedBindings = AbstractState.Merge(bindings, whileBodyBindings);
                    if (whileStatement.ElseStatements is not null)
                    {
                        var whileElseBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(whileStatement.Condition, assumedTruth: false, whileElseBindings);
                        AnalyzeStatements(whileStatement.ElseStatements, diagnostics, whileElseBindings);
                        whileMergedBindings = AbstractState.Merge(whileMergedBindings, whileElseBindings);
                    }

                    bindings.ReplaceWith(whileMergedBindings);
                    break;

                case MatchStatementSyntax matchStatement:
                    AnalyzeExpression(matchStatement.Subject, diagnostics, bindings);
                    var mergedMatchBindings = bindings.Clone();
                    var hasMatchCase = false;
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        var caseBindings = bindings.Clone();
                        if (matchCase.Guard is not null)
                        {
                            AnalyzeExpression(matchCase.Guard, diagnostics, bindings);
                            StaticConditionRefinements.Apply(matchCase.Guard, assumedTruth: true, caseBindings);
                        }

                        AnalyzeStatements(matchCase.Body, diagnostics, caseBindings);
                        mergedMatchBindings = hasMatchCase
                            ? AbstractState.Merge(mergedMatchBindings, caseBindings)
                            : caseBindings;
                        hasMatchCase = true;
                    }

                    if (hasMatchCase)
                    {
                        bindings.ReplaceWith(AbstractState.Merge(bindings, mergedMatchBindings));
                    }
                    break;

                case TryStatementSyntax tryStatement:
                    AnalyzeTry(tryStatement);
                    break;
            }

            void AnalyzeIf(IfStatementSyntax ifStatement)
            {
                AnalyzeExpression(ifStatement.Condition, diagnostics, bindings);
                if (TryResolveConditionTruth(ifStatement.Condition, bindings, out var conditionTruth))
                {
                    if (conditionTruth)
                    {
                        var reachableBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, reachableBindings);
                        AnalyzeStatements(ifStatement.ThenStatements, diagnostics, reachableBindings);
                        bindings.ReplaceWith(reachableBindings);
                    }
                    else if (ifStatement.ElseStatements is not null)
                    {
                        var reachableBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, reachableBindings);
                        AnalyzeStatements(ifStatement.ElseStatements, diagnostics, reachableBindings);
                        bindings.ReplaceWith(reachableBindings);
                    }

                    return;
                }

                var thenBindings = bindings.Clone();
                StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, thenBindings);
                AnalyzeStatements(ifStatement.ThenStatements, diagnostics, thenBindings);
                var elseBindings = bindings.Clone();
                StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
                if (ifStatement.ElseStatements is not null)
                {
                    AnalyzeStatements(ifStatement.ElseStatements, diagnostics, elseBindings);
                }

                var thenExits = StatementsAlwaysExit(ifStatement.ThenStatements);
                var elseExits = ifStatement.ElseStatements is not null && StatementsAlwaysExit(ifStatement.ElseStatements);
                if (thenExits && !elseExits)
                {
                    bindings.ReplaceWith(elseBindings);
                }
                else if (!thenExits && elseExits)
                {
                    bindings.ReplaceWith(thenBindings);
                }
                else
                {
                    bindings.MergeFrom(thenBindings, elseBindings);
                }
            }

            void AnalyzeTry(TryStatementSyntax tryStatement)
            {
                if (tryStatement.ExceptBody is null)
                {
                    AnalyzeStatements(tryStatement.TryBody, diagnostics, bindings.Clone());
                }
                else
                {
                    AnalyzeStatements(tryStatement.ExceptBody, diagnostics, bindings.Clone());
                }

                if (tryStatement.ElseBody is not null)
                {
                    AnalyzeStatements(tryStatement.ElseBody, diagnostics, bindings.Clone());
                }

                if (tryStatement.FinallyBody is not null)
                {
                    AnalyzeStatements(tryStatement.FinallyBody, diagnostics, bindings.Clone());
                }
            }
        }

        void AnalyzeDefinition(StatementSyntax definitionStatement)
        {
            switch (definitionStatement)
            {
                case FunctionDefinitionStatementSyntax functionDefinition:
                    foreach (var decorator in functionDefinition.Decorators)
                    {
                        AnalyzeExpression(decorator, diagnostics, bindings);
                    }

                    foreach (var parameter in functionDefinition.Parameters)
                    {
                        AnalyzeExpressionIfPresent(parameter.DefaultValue, diagnostics, bindings);
                    }

                    if (ScopeDirectiveFactsCollector.ContainsScopeDirective(functionDefinition.Body))
                    {
                        return;
                    }

                    var functionBindings = bindings.Clone();
                    foreach (var parameter in functionDefinition.Parameters)
                    {
                        functionBindings.Remove(parameter.Name);
                    }
                    AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings);
                    break;

                case ClassDefinitionStatementSyntax classDefinition:
                    AnalyzeExpressions(classDefinition.Decorators, diagnostics, bindings);
                    AnalyzeExpressions(classDefinition.Bases, diagnostics, bindings);
                    foreach (var keywordArgument in classDefinition.KeywordArguments)
                    {
                        AnalyzeExpression(keywordArgument.Value, diagnostics, bindings);
                    }

                    AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone());
                    break;
            }
        }

        void AnalyzeSimpleStatement(StatementSyntax simpleStatement)
        {
            switch (simpleStatement)
            {
                case ExpressionStatementSyntax expressionStatement:
                    AnalyzeExpression(expressionStatement.Expression, diagnostics, bindings);
                    break;

                case AssertStatementSyntax assertStatement:
                    AnalyzeExpression(assertStatement.Condition, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(assertStatement.Message, diagnostics, bindings);
                    StaticConditionRefinements.Apply(assertStatement.Condition, assumedTruth: true, bindings);
                    break;

                case DeleteStatementSyntax deleteStatement:
                    AnalyzeExpression(deleteStatement.Target, diagnostics, bindings);
                    break;

                case ReturnStatementSyntax { Expression: not null } returnStatement:
                    AnalyzeExpression(returnStatement.Expression, diagnostics, bindings);
                    break;

                case RaiseStatementSyntax raiseStatement:
                    AnalyzeExpression(raiseStatement.Expression, diagnostics, bindings);
                    break;
            }
        }
    }

    private static void AnalyzeAssignmentTarget(AssignmentTargetSyntax target, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        switch (target)
        {
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                foreach (var nestedTarget in unpacking.Targets)
                {
                    _ = nestedTarget;
                }
                break;

            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeExpression(subscript.Target, diagnostics, bindings);
                AnalyzeExpression(subscript.Index, diagnostics, bindings);
                break;

            case SliceAssignmentTargetSyntax slice:
                AnalyzeExpression(slice.Target, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                break;

            case MemberAssignmentTargetSyntax member:
                AnalyzeExpression(member.Target, diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzeExpressionIfPresent(ExpressionSyntax? expression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (expression is not null)
        {
            AnalyzeExpression(expression, diagnostics, bindings);
        }
    }

}
