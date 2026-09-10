namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private sealed class AssignmentStatementAnalyzer : IStatementAnalyzer
    {
        public static readonly AssignmentStatementAnalyzer Instance = new();

        public bool TryAnalyze(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax assignment:
                    AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                    return true;

                case ChainedAssignmentStatementSyntax chained:
                    AnalyzeExpression(chained.Expression, diagnostics, bindings);
                    foreach (var target in chained.Targets)
                    {
                        AnalyzeAssignmentTarget(target, diagnostics, bindings);
                    }
                    return true;

                case AnnotatedAssignmentStatementSyntax annotated:
                    AnalyzeExpressionIfPresent(annotated.Expression, diagnostics, bindings);
                    return true;

                case SubscriptAssignmentStatementSyntax subscript:
                    AnalyzeExpression(subscript.Target, diagnostics, bindings);
                    AnalyzeExpression(subscript.Index, diagnostics, bindings);
                    AnalyzeExpression(subscript.Expression, diagnostics, bindings);
                    return true;

                case SliceAssignmentStatementSyntax slice:
                    AnalyzeExpression(slice.Target, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                    AnalyzeExpression(slice.Expression, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeSliceAssignment(slice, diagnostics, bindings);
                    return true;

                case MemberAssignmentStatementSyntax member:
                    AnalyzeExpression(member.Target, diagnostics, bindings);
                    AnalyzeExpression(member.Expression, diagnostics, bindings);
                    return true;

                case AugmentedAssignmentStatementSyntax augmented:
                    AnalyzeAssignmentTarget(augmented.Target, diagnostics, bindings);
                    AnalyzeExpression(augmented.Expression, diagnostics, bindings);
                    return true;

                case UnpackingAssignmentStatementSyntax unpacking:
                    AnalyzeExpression(unpacking.Expression, diagnostics, bindings);
                    StaticDestructuringDiagnostics.AnalyzeUnpackingTargets(
                        unpacking.Targets,
                        unpacking.Expression,
                        unpacking.Span,
                        diagnostics,
                        bindings);
                    return true;

                default:
                    return false;
            }
        }
    }

    private sealed class ControlFlowStatementAnalyzer : IStatementAnalyzer
    {
        public static readonly ControlFlowStatementAnalyzer Instance = new();

        public bool TryAnalyze(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (statement)
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
                    return true;

                case IfStatementSyntax ifStatement:
                    AnalyzeIf(ifStatement);
                    return true;

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
                    return true;

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
                    return true;

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
                    return true;

                case TryStatementSyntax tryStatement:
                    AnalyzeTry(tryStatement);
                    return true;

                default:
                    return false;
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
                var tryBindings = bindings.Clone();
                AbstractState? exceptBindings = null;
                if (tryStatement.ExceptClauses.Count == 0)
                {
                    AnalyzeStatements(tryStatement.TryBody, diagnostics, tryBindings);
                }
                else
                {
                    // A runtime contract failure in the protected body may be the exception
                    // that the Python program intends to catch. Keep its successful-path facts,
                    // but do not turn the catchable failure into a compilation failure.
                    AnalyzeStatements(tryStatement.TryBody, [], tryBindings);

                    foreach (var exceptClause in tryStatement.ExceptClauses)
                    {
                        var clauseBindings = bindings.Clone();
                        AnalyzeStatements(exceptClause.Body, diagnostics, clauseBindings);
                        exceptBindings = exceptBindings is null
                            ? clauseBindings
                            : AbstractState.Merge(exceptBindings, clauseBindings);
                    }
                }

                if (tryStatement.ElseBody is not null)
                {
                    // Python enters else only when the protected body completed successfully.
                    AnalyzeStatements(tryStatement.ElseBody, diagnostics, tryBindings);
                }

                var continuationBindings = exceptBindings is null
                    ? tryBindings
                    : AbstractState.Merge(tryBindings, exceptBindings);

                if (tryStatement.FinallyBody is not null)
                {
                    AnalyzeStatements(tryStatement.FinallyBody, diagnostics, continuationBindings);
                }

                bindings.ReplaceWith(continuationBindings);
            }
        }
    }

    private sealed class DefinitionStatementAnalyzer : IStatementAnalyzer
    {
        public static readonly DefinitionStatementAnalyzer Instance = new();

        public bool TryAnalyze(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (statement)
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
                        return true;
                    }

                    var functionBindings = bindings.Clone();
                    foreach (var parameter in functionDefinition.Parameters)
                    {
                        functionBindings.Remove(parameter.Name);
                    }
                    AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings);
                    return true;

                case ClassDefinitionStatementSyntax classDefinition:
                    AnalyzeExpressions(classDefinition.Decorators, diagnostics, bindings);
                    AnalyzeExpressions(classDefinition.Bases, diagnostics, bindings);
                    foreach (var keywordArgument in classDefinition.KeywordArguments)
                    {
                        AnalyzeExpression(keywordArgument.Value, diagnostics, bindings);
                    }

                    AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone());
                    return true;

                default:
                    return false;
            }
        }
    }

    private sealed class SimpleStatementAnalyzer : IStatementAnalyzer
    {
        public static readonly SimpleStatementAnalyzer Instance = new();

        public bool TryAnalyze(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (statement)
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

            // This is the terminal analyzer: statements with no expression work are valid no-ops here.
            return true;
        }
    }
}
