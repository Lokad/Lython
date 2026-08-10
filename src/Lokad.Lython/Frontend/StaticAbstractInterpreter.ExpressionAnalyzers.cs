namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private sealed class CollectionExpressionAnalyzer : IExpressionAnalyzer
    {
        public static readonly CollectionExpressionAnalyzer Instance = new();

        public bool TryAnalyze(ExpressionSyntax expression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (expression)
            {
                case FormattedStringExpressionSyntax formatted:
                    foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                    {
                        AnalyzeExpression(nestedExpression, diagnostics, bindings);
                    }
                    return true;

                case ListLiteralExpressionSyntax list:
                    AnalyzeCollectionDisplayItems(list.Items, diagnostics, bindings);
                    return true;

                case ListComprehensionExpressionSyntax listComprehension:
                    AnalyzeComprehension(
                        listComprehension.ItemExpression,
                        listComprehension.Clauses,
                        diagnostics,
                        bindings);
                    return true;

                case GeneratorExpressionSyntax generator:
                    AnalyzeComprehension(generator.ItemExpression, generator.Clauses, diagnostics, bindings);
                    return true;

                case DictLiteralExpressionSyntax dict:
                    foreach (var item in dict.Items)
                    {
                        AnalyzeExpression(item.Key, diagnostics, bindings);
                        if (!item.IsUnpacking)
                        {
                            AnalyzeExpression(item.Value, diagnostics, bindings);
                        }
                    }
                    return true;

                case SetLiteralExpressionSyntax set:
                    AnalyzeCollectionDisplayItems(set.Items, diagnostics, bindings);
                    return true;

                case SetComprehensionExpressionSyntax setComprehension:
                    AnalyzeComprehension(
                        setComprehension.ItemExpression,
                        setComprehension.Clauses,
                        diagnostics,
                        bindings);
                    return true;

                case DictComprehensionExpressionSyntax dictComprehension:
                    var comprehensionBindings = AnalyzeComprehensionClauses(
                        dictComprehension.Clauses,
                        diagnostics,
                        bindings,
                        out var reachable);
                    if (reachable)
                    {
                        AnalyzeExpression(dictComprehension.KeyExpression, diagnostics, comprehensionBindings);
                        AnalyzeExpression(dictComprehension.ValueExpression, diagnostics, comprehensionBindings);
                    }
                    return true;

                case TupleLiteralExpressionSyntax tuple:
                    AnalyzeCollectionDisplayItems(tuple.Items, diagnostics, bindings);
                    return true;

                case ParenthesizedExpressionSyntax parenthesized:
                    AnalyzeExpression(parenthesized.Inner, diagnostics, bindings);
                    return true;

                default:
                    return false;
            }

            static void AnalyzeComprehension(
                ExpressionSyntax item,
                IReadOnlyList<ComprehensionClauseSyntax> clauses,
                List<LythonDiagnostic> diagnostics,
                AbstractState bindings)
            {
                var comprehensionBindings = AnalyzeComprehensionClauses(clauses, diagnostics, bindings, out var reachable);
                if (reachable)
                {
                    AnalyzeExpression(item, diagnostics, comprehensionBindings);
                }
            }

            static void AnalyzeCollectionDisplayItems(
                IReadOnlyList<CollectionDisplayItemSyntax> items,
                List<LythonDiagnostic> diagnostics,
                AbstractState bindings)
            {
                foreach (var item in items)
                {
                    AnalyzeExpression(item.Expression, diagnostics, bindings);
                }
            }
        }
    }

    private sealed class AccessCallExpressionAnalyzer : IExpressionAnalyzer
    {
        public static readonly AccessCallExpressionAnalyzer Instance = new();

        public bool TryAnalyze(ExpressionSyntax expression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (expression)
            {
                case MemberExpressionSyntax member:
                    AnalyzeExpression(member.Target, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeMemberAccess(member, diagnostics, bindings);
                    return true;

                case CallExpressionSyntax call:
                    AnalyzeExpression(call.Target, diagnostics, bindings);
                    AnalyzeCall(call);
                    foreach (var argument in call.Arguments)
                    {
                        AnalyzeExpression(argument.Expression, diagnostics, bindings);
                    }
                    return true;

                case SubscriptExpressionSyntax subscript:
                    AnalyzeExpression(subscript.Target, diagnostics, bindings);
                    AnalyzeExpression(subscript.Index, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeSubscriptAccess(subscript, diagnostics, bindings);
                    return true;

                case SliceExpressionSyntax slice:
                    AnalyzeExpression(slice.Target, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeSliceAccess(slice, diagnostics, bindings);
                    return true;

                default:
                    return false;
            }

            void AnalyzeCall(CallExpressionSyntax call)
            {
                if (StaticAbstractValueResolver.TryResolve(call.Target, bindings, out var targetValue) &&
                    StaticAbstractFacts.IsDefinitelyNonCallable(targetValue))
                {
                    AddDiagnostic(diagnostics, "LA3107", "Object is not callable.", call.Target.Span);
                    return;
                }

                if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments))
                {
                    return;
                }

                if (AnalyzeUserDefinedCallShape(call, arguments))
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

            bool AnalyzeUserDefinedCallShape(CallExpressionSyntax call, ConcreteCallArguments arguments)
            {
                if (call.Target is IdentifierExpressionSyntax identifier &&
                    bindings.TryGet(identifier.Name, out var targetValue))
                {
                    if (targetValue.Kind == AbstractValueKind.Function &&
                        StaticBindingEngine.TryGetFunctionCallShapeFailure(
                            targetValue.RequirePayload<AbstractFunctionSummary>(),
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
                            targetValue.RequirePayload<AbstractClassSummary>(),
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
        }
    }

    private sealed class OperatorFlowExpressionAnalyzer : IExpressionAnalyzer
    {
        public static readonly OperatorFlowExpressionAnalyzer Instance = new();

        public bool TryAnalyze(ExpressionSyntax expression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        {
            switch (expression)
            {
                case BinaryExpressionSyntax binary:
                    AnalyzeExpression(binary.Left, diagnostics, bindings);
                    if (binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        var continueTruth = binary.Operator == BinaryOperatorSyntax.And;
                        if (!TryResolveConditionTruth(binary.Left, bindings, out var leftTruth) || leftTruth == continueTruth)
                        {
                            var rightBindings = bindings.Clone();
                            StaticConditionRefinements.Apply(binary.Left, continueTruth, rightBindings);
                            AnalyzeExpression(binary.Right, diagnostics, rightBindings);
                        }
                    }
                    else
                    {
                        AnalyzeExpression(binary.Right, diagnostics, bindings);
                    }
                    StaticStructuralDiagnostics.AnalyzeBinaryOperation(binary, diagnostics, bindings);
                    return true;

                case ChainedComparisonExpressionSyntax chained:
                    AnalyzeExpressions(chained.Operands, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeChainedComparisonOperations(chained, diagnostics, bindings);
                    return true;

                case UnaryExpressionSyntax unary:
                    AnalyzeExpression(unary.Operand, diagnostics, bindings);
                    StaticStructuralDiagnostics.AnalyzeUnaryOperation(unary, diagnostics, bindings);
                    return true;

                case ConditionalExpressionSyntax conditional:
                    AnalyzeExpression(conditional.Condition, diagnostics, bindings);
                    if (TryResolveConditionTruth(conditional.Condition, bindings, out var conditionalTruth))
                    {
                        var selectedBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(conditional.Condition, conditionalTruth, selectedBindings);
                        AnalyzeExpression(
                            conditionalTruth ? conditional.Consequent : conditional.Alternative,
                            diagnostics,
                            selectedBindings);
                    }
                    else
                    {
                        var consequentBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(conditional.Condition, assumedTruth: true, consequentBindings);
                        AnalyzeExpression(conditional.Consequent, diagnostics, consequentBindings);
                        var alternativeBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(conditional.Condition, assumedTruth: false, alternativeBindings);
                        AnalyzeExpression(conditional.Alternative, diagnostics, alternativeBindings);
                    }
                    return true;

                case AssignmentExpressionSyntax assignment:
                    AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                    return true;

                case LambdaExpressionSyntax lambda:
                    var lambdaBindings = bindings.Clone();
                    StaticBindingEngine.BindFunctionParametersUnknown(lambda.Parameters, lambdaBindings);
                    AnalyzeExpression(lambda.Body, diagnostics, lambdaBindings);
                    return true;

                default:
                    return false;
            }
        }
    }
}
