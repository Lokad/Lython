namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private static void AnalyzeExpression(
        ExpressionSyntax expression,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
            {
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    AnalyzeExpression(nestedExpression, diagnostics, bindings);
                }

                return;
            }

            case ListLiteralExpressionSyntax list:
            {
                AnalyzeCollectionDisplayItems(list.Items, diagnostics, bindings);
                return;
            }

            case ListComprehensionExpressionSyntax listComprehension:
            {
                AnalyzeComprehension(listComprehension.ItemExpression, listComprehension.Clauses, diagnostics, bindings);
                return;
            }

            case GeneratorExpressionSyntax generator:
            {
                AnalyzeComprehension(generator.ItemExpression, generator.Clauses, diagnostics, bindings, deferred: true);
                return;
            }

            case DictLiteralExpressionSyntax dict:
            {
                foreach (var item in dict.Items)
                {
                    AnalyzeExpression(item.Key, diagnostics, bindings);
                    if (!item.IsUnpacking)
                    {
                        AnalyzeExpression(item.Value, diagnostics, bindings);
                    }
                }

                return;
            }

            case SetLiteralExpressionSyntax set:
            {
                AnalyzeCollectionDisplayItems(set.Items, diagnostics, bindings);
                return;
            }

            case SetComprehensionExpressionSyntax setComprehension:
            {
                AnalyzeComprehension(setComprehension.ItemExpression, setComprehension.Clauses, diagnostics, bindings);
                return;
            }

            case DictComprehensionExpressionSyntax dictComprehension:
            {
                var comprehensionBindings = AnalyzeComprehensionClauses(dictComprehension.Clauses, diagnostics, bindings, out var reachable);
                if (reachable)
                {
                    AnalyzeExpression(dictComprehension.KeyExpression, diagnostics, comprehensionBindings);
                    AnalyzeExpression(dictComprehension.ValueExpression, diagnostics, comprehensionBindings);
                }

                bindings.ApplyContainerInvalidation(comprehensionBindings);
                return;
            }

            case TupleLiteralExpressionSyntax tuple:
            {
                AnalyzeCollectionDisplayItems(tuple.Items, diagnostics, bindings);
                return;
            }

            case ParenthesizedExpressionSyntax parenthesized:
            {
                AnalyzeExpression(parenthesized.Inner, diagnostics, bindings);
                return;
            }

            case MemberExpressionSyntax member:
            {
                AnalyzeExpression(member.Target, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeMemberAccess(member, diagnostics, bindings);
                return;
            }

            case CallExpressionSyntax call:
            {
                var diagnosticsBeforeCall = diagnostics.Count;
                AnalyzeExpression(call.Target, diagnostics, bindings);
                AnalyzeCall(call, diagnostics, bindings);
                foreach (var argument in call.Arguments)
                {
                    AnalyzeExpression(argument.Expression, diagnostics, bindings);
                }

                if (diagnostics.Count == diagnosticsBeforeCall && StaticBindingEngine.CallMayMutateContainerFacts(call, bindings))
                {
                    // Preserve a scalar return (e.g. dict.pop) evaluated
                    // before mutation. Container returns may alias the changed
                    // input, so their exact shape facts cannot be retained.
                    var resolved = StaticAbstractValueResolver.TryResolve(call, bindings, out var returnValue) && !AbstractState.ContainsMutableCollectionFacts(returnValue, bindings.AnalysisContext);
                    bindings.InvalidateCollectionFacts();
                    if (resolved)
                    {
                        bindings.SetCachedAbstractValue(call, AbstractValueResolution.Resolved(returnValue));
                    }
                }

                return;
            }

            case SubscriptExpressionSyntax subscript:
            {
                AnalyzeExpression(subscript.Target, diagnostics, bindings);
                AnalyzeExpression(subscript.Index, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeSubscriptAccess(subscript, diagnostics, bindings);
                return;
            }

            case SliceExpressionSyntax slice:
            {
                AnalyzeExpression(slice.Target, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeSliceAccess(slice, diagnostics, bindings);
                return;
            }

            case SliceValueExpressionSyntax slice:
            {
                AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                return;
            }

            case BinaryExpressionSyntax binary:
            {
                AnalyzeExpression(binary.Left, diagnostics, bindings);
                if (binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                {
                    var continueTruth = binary.Operator == BinaryOperatorSyntax.And;
                    if (!TryResolveConditionTruth(binary.Left, bindings, out var leftTruth) || leftTruth == continueTruth)
                    {
                        var rightBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(binary.Left, continueTruth, rightBindings);
                        AnalyzeExpression(binary.Right, diagnostics, rightBindings);
                        if (TryResolveConditionTruth(binary.Left, bindings, out _))
                        {
                            bindings.TakeBranchResult(rightBindings);
                        }
                        else
                        {
                            bindings.MergeFrom(bindings.Clone(), rightBindings);
                        }
                    }
                }
                else
                {
                    AnalyzeExpression(binary.Right, diagnostics, bindings);
                }

                StaticStructuralDiagnostics.AnalyzeBinaryOperation(binary, diagnostics, bindings);
                return;
            }

            case ChainedComparisonExpressionSyntax chained:
            {
                AnalyzeExpressions(chained.Operands, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeChainedComparisonOperations(chained, diagnostics, bindings);
                return;
            }

            case UnaryExpressionSyntax unary:
            {
                AnalyzeExpression(unary.Operand, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeUnaryOperation(unary, diagnostics, bindings);
                return;
            }

            case ConditionalExpressionSyntax conditional:
            {
                AnalyzeExpression(conditional.Condition, diagnostics, bindings);
                if (TryResolveConditionTruth(conditional.Condition, bindings, out var conditionalTruth))
                {
                    StaticConditionRefinements.Apply(conditional.Condition, conditionalTruth, bindings);
                    AnalyzeExpression(conditionalTruth ? conditional.Consequent : conditional.Alternative, diagnostics, bindings);
                }
                else
                {
                    var consequentBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(conditional.Condition, assumedTruth: true, consequentBindings);
                    AnalyzeExpression(conditional.Consequent, diagnostics, consequentBindings);
                    var alternativeBindings = bindings.Clone();
                    StaticConditionRefinements.Apply(conditional.Condition, assumedTruth: false, alternativeBindings);
                    AnalyzeExpression(conditional.Alternative, diagnostics, alternativeBindings);
                    bindings.MergeFrom(consequentBindings, alternativeBindings);
                }

                return;
            }

            case AssignmentExpressionSyntax assignment:
            {
                AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                return;
            }

            case LambdaExpressionSyntax lambda:
            {
                foreach (var parameter in lambda.Parameters)
                {
                    AnalyzeExpressionIfPresent(parameter.DefaultValue, diagnostics, bindings);
                }

                var lambdaBindings = bindings.CloneForDeferredBody();
                StaticBindingEngine.BindFunctionParametersUnknown(lambda.Parameters, lambdaBindings);
                AnalyzeExpression(lambda.Body, diagnostics, lambdaBindings);
                return;
            }
        }
    }

    private static void AnalyzeComprehension(
        ExpressionSyntax item,
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        bool deferred = false)
    {
        var comprehensionBindings = AnalyzeComprehensionClauses(clauses, diagnostics, bindings, out var reachable);
        if (reachable)
        {
            AnalyzeExpression(item, diagnostics, comprehensionBindings);
        }

        if (!deferred)
        {
            bindings.ApplyContainerInvalidation(comprehensionBindings);
        }
    }

    private static void AnalyzeCollectionDisplayItems(
        IReadOnlyList<CollectionDisplayItemSyntax> items,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var item in items)
        {
            AnalyzeExpression(item.Expression, diagnostics, bindings);
        }
    }

    private static void AnalyzeCall(
        CallExpressionSyntax call,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (StaticAbstractValueResolver.TryResolve(call.Target, bindings, out var targetValue) && StaticAbstractFacts.IsDefinitelyNonCallable(targetValue))
        {
            AddDiagnostic(diagnostics, "LA3107", "Object is not callable.", call.Target.Span);
            return;
        }

        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments))
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

        _ = StaticContractEngine.TryAnalyzeCall(call, arguments, diagnostics, bindings);
    }

    private static bool AnalyzeUserDefinedCallShape(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is IdentifierExpressionSyntax identifier && bindings.TryGet(identifier.Name, out var targetValue))
        {
            if (targetValue.Kind == AbstractValueKind.Function && StaticBindingEngine.TryGetFunctionCallShapeFailure(targetValue.RequireFunctionSummary(), arguments, out var functionReason, out var functionOffendingExpression))
            {
                AddDiagnostic(diagnostics, "LA3148", $"Function '{identifier.Name}' call does not match its parameter list: {functionReason}.", functionOffendingExpression?.Span ?? call.Span);
                return true;
            }

            if (targetValue.Kind == AbstractValueKind.UserClass && StaticBindingEngine.TryGetDataclassConstructorShapeFailure(targetValue.RequireClassSummary(), arguments, out var constructorReason, out var constructorOffendingExpression))
            {
                AddDiagnostic(diagnostics, "LA3149", $"Constructor for '{identifier.Name}' does not match its dataclass fields: {constructorReason}.", constructorOffendingExpression?.Span ?? call.Span);
                return true;
            }
        }

        if (call.Target is MemberExpressionSyntax { Target: var receiverExpression, MemberName: var methodName } && StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver) && receiver.Kind == AbstractValueKind.UserInstance && StaticBindingEngine.TryGetUserInstanceMethodCallShapeFailure(receiver, methodName, arguments, out var methodReason, out var methodOffendingExpression))
        {
            AddDiagnostic(diagnostics, "LA3150", $"Method '{methodName}' call does not match its parameter list: {methodReason}.", methodOffendingExpression?.Span ?? call.Span);
            return true;
        }

        return false;
    }
}
