namespace Lokad.Lython.Frontend;

internal static class StaticAbstractInterpreter
{
    public static void Analyze(StaticAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
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
                if (annotated.Expression is not null)
                {
                    AnalyzeExpression(annotated.Expression, diagnostics, bindings);
                }
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
                StaticDestructuringDiagnostics.AnalyzeUnpackingTargets(unpacking.Targets, unpacking.Expression, unpacking.Span, diagnostics, bindings);
                break;

            case ExpressionStatementSyntax expressionStatement:
                AnalyzeExpression(expressionStatement.Expression, diagnostics, bindings);
                break;

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

                    break;
                }

                var thenBindings = bindings.Clone();
                StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, thenBindings);
                AnalyzeStatements(ifStatement.ThenStatements, diagnostics, thenBindings);
                AbstractState elseBindings;
                if (ifStatement.ElseStatements is not null)
                {
                    elseBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
                    AnalyzeStatements(ifStatement.ElseStatements, diagnostics, elseBindings);
                }
                else
                {
                    elseBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
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
                AnalyzeStatements(whileStatement.Body, diagnostics, whileBodyBindings);
                var whileMergedBindings = AbstractState.Merge(bindings, whileBodyBindings);
                if (whileStatement.ElseStatements is not null)
                {
                    var whileElseBindings = bindings.Clone();
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
                    if (matchCase.Guard is not null)
                    {
                        AnalyzeExpression(matchCase.Guard, diagnostics, bindings);
                    }

                    var caseBindings = bindings.Clone();
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

            case AssertStatementSyntax assertStatement:
                AnalyzeExpression(assertStatement.Condition, diagnostics, bindings);
                if (assertStatement.Message is not null)
                {
                    AnalyzeExpression(assertStatement.Message, diagnostics, bindings);
                }
                StaticConditionRefinements.Apply(assertStatement.Condition, assumedTruth: true, bindings);
                break;

            case DeleteStatementSyntax deleteStatement:
                AnalyzeExpression(deleteStatement.Target, diagnostics, bindings);
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators)
                {
                    AnalyzeExpression(decorator, diagnostics, bindings);
                }

                foreach (var parameter in functionDefinition.Parameters)
                {
                    if (parameter.DefaultValue is not null)
                    {
                        AnalyzeExpression(parameter.DefaultValue, diagnostics, bindings);
                    }
                }

                if (ScopeDirectiveFactsCollector.ContainsScopeDirective(functionDefinition.Body))
                {
                    break;
                }

                var functionBindings = bindings.Clone();
                foreach (var parameter in functionDefinition.Parameters)
                {
                    functionBindings.Remove(parameter.Name);
                }
                AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators)
                {
                    AnalyzeExpression(decorator, diagnostics, bindings);
                }

                foreach (var @base in classDefinition.Bases)
                {
                    AnalyzeExpression(@base, diagnostics, bindings);
                }

                foreach (var keywordArgument in classDefinition.KeywordArguments)
                {
                    AnalyzeExpression(keywordArgument.Value, diagnostics, bindings);
                }

                AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone());
                break;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null:
                AnalyzeExpression(returnStatement.Expression, diagnostics, bindings);
                break;

            case RaiseStatementSyntax raiseStatement:
                AnalyzeExpression(raiseStatement.Expression, diagnostics, bindings);
                break;

            case TryStatementSyntax tryStatement:
                if (tryStatement.ExceptBody is null)
                {
                    AnalyzeStatements(tryStatement.TryBody, diagnostics, bindings.Clone());
                }

                if (tryStatement.ExceptBody is not null)
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
                break;
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

    private static void AnalyzeExpression(ExpressionSyntax expression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
                foreach (var part in formatted.Parts)
                {
                    if (part is FormattedStringExpressionPartSyntax expressionPart)
                    {
                        AnalyzeExpression(expressionPart.Expression, diagnostics, bindings);
                    }
                }
                break;

            case ListLiteralExpressionSyntax list:
                AnalyzeExpressions(list.Items, diagnostics, bindings);
                break;

            case ListComprehensionExpressionSyntax listComprehension:
            {
                var comprehensionBindings = AnalyzeComprehensionClauses(listComprehension.Clauses, diagnostics, bindings);
                AnalyzeExpression(listComprehension.ItemExpression, diagnostics, comprehensionBindings);
                break;
            }

            case GeneratorExpressionSyntax generator:
            {
                var comprehensionBindings = AnalyzeComprehensionClauses(generator.Clauses, diagnostics, bindings);
                AnalyzeExpression(generator.ItemExpression, diagnostics, comprehensionBindings);
                break;
            }

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    AnalyzeExpression(item.Key, diagnostics, bindings);
                    AnalyzeExpression(item.Value, diagnostics, bindings);
                }
                break;

            case SetLiteralExpressionSyntax set:
                AnalyzeExpressions(set.Items, diagnostics, bindings);
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
            {
                var comprehensionBindings = AnalyzeComprehensionClauses(dictComprehension.Clauses, diagnostics, bindings);
                AnalyzeExpression(dictComprehension.KeyExpression, diagnostics, comprehensionBindings);
                AnalyzeExpression(dictComprehension.ValueExpression, diagnostics, comprehensionBindings);
                break;
            }

            case TupleLiteralExpressionSyntax tuple:
                AnalyzeExpressions(tuple.Items, diagnostics, bindings);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                AnalyzeExpression(parenthesized.Inner, diagnostics, bindings);
                break;

            case MemberExpressionSyntax member:
                AnalyzeExpression(member.Target, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeMemberAccess(member, diagnostics, bindings);
                break;

            case CallExpressionSyntax call:
                AnalyzeExpression(call.Target, diagnostics, bindings);
                AnalyzeCall(call, diagnostics, bindings);
                foreach (var argument in call.Arguments)
                {
                    AnalyzeExpression(argument.Expression, diagnostics, bindings);
                }
                break;

            case SubscriptExpressionSyntax subscript:
                AnalyzeExpression(subscript.Target, diagnostics, bindings);
                AnalyzeExpression(subscript.Index, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeSubscriptAccess(subscript, diagnostics, bindings);
                break;

            case SliceExpressionSyntax slice:
                AnalyzeExpression(slice.Target, diagnostics, bindings);
                if (slice.Start is not null) AnalyzeExpression(slice.Start, diagnostics, bindings);
                if (slice.End is not null) AnalyzeExpression(slice.End, diagnostics, bindings);
                if (slice.Step is not null) AnalyzeExpression(slice.Step, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeSliceAccess(slice, diagnostics, bindings);
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeExpression(binary.Left, diagnostics, bindings);
                AnalyzeExpression(binary.Right, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeBinaryOperation(binary, diagnostics, bindings);
                break;

            case ChainedComparisonExpressionSyntax chained:
                AnalyzeExpressions(chained.Operands, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeChainedComparisonOperations(chained, diagnostics, bindings);
                break;

            case UnaryExpressionSyntax unary:
                AnalyzeExpression(unary.Operand, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeUnaryOperation(unary, diagnostics, bindings);
                break;

            case ConditionalExpressionSyntax conditional:
                AnalyzeExpression(conditional.Condition, diagnostics, bindings);
                AnalyzeExpression(conditional.Consequent, diagnostics, bindings);
                AnalyzeExpression(conditional.Alternative, diagnostics, bindings);
                break;

            case AssignmentExpressionSyntax assignment:
                AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                break;

            case LambdaExpressionSyntax lambda:
            {
                var lambdaBindings = bindings.Clone();
                StaticBindingEngine.BindFunctionParametersUnknown(lambda.Parameters, lambdaBindings);
                AnalyzeExpression(lambda.Body, diagnostics, lambdaBindings);
                break;
            }
        }
    }

    private static void AnalyzeExpressions(IReadOnlyList<ExpressionSyntax> expressions, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        foreach (var expression in expressions)
        {
            AnalyzeExpression(expression, diagnostics, bindings);
        }
    }

    private static AbstractState AnalyzeComprehensionClauses(IReadOnlyList<ComprehensionClauseSyntax> clauses, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var comprehensionBindings = bindings.Clone();
        foreach (var clause in clauses)
        {
            AnalyzeExpression(clause.Iterable, diagnostics, comprehensionBindings);
            StaticIterationDiagnostics.AnalyzeLoopTarget(clause.Target, clause.Iterable, clause.Span, diagnostics, comprehensionBindings);
            StaticBindingEngine.BindLoopTargetFromIterable(clause.Target, clause.Iterable, comprehensionBindings);
            if (clause.Condition is not null)
            {
                AnalyzeExpression(clause.Condition, diagnostics, comprehensionBindings);
            }
        }

        return comprehensionBindings;
    }

    private static void AnalyzeCall(CallExpressionSyntax call, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (StaticAbstractValueResolver.TryResolve(call.Target, bindings, out var targetValue) &&
            StaticAbstractFacts.IsDefinitelyNonCallable(targetValue))
        {
            AddDiagnostic(diagnostics, "LA3107", "Object is not callable.", call.Target.Span);
            return;
        }

        if (!TryGetConcreteArguments(call, bindings, out var arguments))
        {
            return;
        }

        if (AnalyzeUserDefinedCallShape(call, arguments, diagnostics, bindings))
        {
            return;
        }

        if (StaticContractEngine.AnalyzeKnownCallContract(call, arguments, diagnostics, bindings))
        {
            return;
        }

        if (StaticContractEngine.AnalyzeCallableContract(call, arguments, diagnostics, bindings))
        {
            return;
        }

        if (StaticContractEngine.TryAnalyzeCall(call, arguments, diagnostics, bindings))
        {
            return;
        }
    }

    private static bool TryGetConcreteArguments(CallExpressionSyntax call, AbstractState bindings, out ConcreteCallArguments arguments)
        => StaticCallArguments.TryGetConcreteArguments(call, bindings, out arguments);

    private static bool AnalyzeUserDefinedCallShape(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is IdentifierExpressionSyntax identifier &&
            bindings.TryGet(identifier.Name, out var targetValue))
        {
            if (targetValue.Kind == AbstractValueKind.Function &&
                StaticBindingEngine.TryGetFunctionCallShapeFailure(
                    (AbstractFunctionSummary)targetValue.Value,
                    arguments,
                    out var functionReason,
                    out var functionOffendingExpression))
            {
                AddDiagnostic(
                    diagnostics,
                    "LA3148",
                    $"Function '{identifier.Name}' call does not match its parameter list: {functionReason}.",
                    functionOffendingExpression?.Span ?? call.Span);
                return true;
            }

            if (targetValue.Kind == AbstractValueKind.UserClass &&
                StaticBindingEngine.TryGetDataclassConstructorShapeFailure(
                    (AbstractClassSummary)targetValue.Value,
                    arguments,
                    out var constructorReason,
                    out var constructorOffendingExpression))
            {
                AddDiagnostic(
                    diagnostics,
                    "LA3149",
                    $"Constructor for '{identifier.Name}' does not match its dataclass fields: {constructorReason}.",
                    constructorOffendingExpression?.Span ?? call.Span);
                return true;
            }
        }

        if (call.Target is MemberExpressionSyntax { Target: var receiverExpression, MemberName: var methodName } &&
            StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver) &&
            receiver.Kind == AbstractValueKind.UserInstance &&
            StaticBindingEngine.TryGetUserInstanceMethodCallShapeFailure(
                receiver,
                methodName,
                arguments,
                out var methodReason,
                out var methodOffendingExpression))
        {
            AddDiagnostic(
                diagnostics,
                "LA3150",
                $"Method '{methodName}' call does not match its parameter list: {methodReason}.",
                methodOffendingExpression?.Span ?? call.Span);
            return true;
        }

        return false;
    }

    private static bool TryResolveConditionTruth(ExpressionSyntax condition, AbstractState bindings, out bool truth)
    {
        if (StaticAbstractValueResolver.TryResolve(condition, bindings, out var value))
        {
            switch (value.Kind)
            {
                case AbstractValueKind.Boolean:
                    truth = (bool)value.Value;
                    return true;
                case AbstractValueKind.None:
                    truth = false;
                    return true;
            }
        }

        if (condition is BinaryExpressionSyntax { Operator: var op, Left: var left, Right: var right } &&
            op is BinaryOperatorSyntax.Is or BinaryOperatorSyntax.IsNot &&
            TryResolveNoneComparison(left, right, bindings, out var isNone))
        {
            truth = op == BinaryOperatorSyntax.Is ? isNone : !isNone;
            return true;
        }

        truth = false;
        return false;
    }

    private static bool TryResolveNoneComparison(
        ExpressionSyntax left,
        ExpressionSyntax right,
        AbstractState bindings,
        out bool isNone)
    {
        if (right is NoneLiteralExpressionSyntax &&
            StaticAbstractValueResolver.TryResolve(left, bindings, out var leftValue))
        {
            if (leftValue.Kind == AbstractValueKind.None)
            {
                isNone = true;
                return true;
            }

            if (StaticAbstractFacts.IsDefinitelyNonNone(leftValue))
            {
                isNone = false;
                return true;
            }
        }

        if (left is NoneLiteralExpressionSyntax &&
            StaticAbstractValueResolver.TryResolve(right, bindings, out var rightValue))
        {
            if (rightValue.Kind == AbstractValueKind.None)
            {
                isNone = true;
                return true;
            }

            if (StaticAbstractFacts.IsDefinitelyNonNone(rightValue))
            {
                isNone = false;
                return true;
            }
        }

        isNone = false;
        return false;
    }

    private static bool StatementsAlwaysExit(IReadOnlyList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ReturnStatementSyntax:
                case RaiseStatementSyntax:
                    return true;

                case IfStatementSyntax { ElseStatements: not null } ifStatement
                    when StatementsAlwaysExit(ifStatement.ThenStatements) &&
                         StatementsAlwaysExit(ifStatement.ElseStatements):
                    return true;
            }
        }

        return false;
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }

}
