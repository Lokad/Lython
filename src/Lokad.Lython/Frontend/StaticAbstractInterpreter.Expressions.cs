namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private static readonly IExpressionAnalyzer[] ExpressionAnalyzers =
    [
        CollectionExpressionAnalyzer.Instance,
        AccessCallExpressionAnalyzer.Instance,
        OperatorFlowExpressionAnalyzer.Instance,
    ];

    private static void AnalyzeExpression(
        ExpressionSyntax expression,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var analyzer in ExpressionAnalyzers)
        {
            if (analyzer.TryAnalyze(expression, diagnostics, bindings))
            {
                return;
            }
        }
    }

    private static void AnalyzeExpressions(
        IReadOnlyList<ExpressionSyntax> expressions,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var expression in expressions)
        {
            AnalyzeExpression(expression, diagnostics, bindings);
        }
    }

    private static AbstractState AnalyzeComprehensionClauses(
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        out bool reachable)
    {
        var comprehensionBindings = bindings.Clone();
        reachable = true;
        foreach (var clause in clauses)
        {
            AnalyzeExpression(clause.Iterable, diagnostics, comprehensionBindings);
            StaticIterationDiagnostics.AnalyzeLoopTarget(clause.Target, clause.Iterable, clause.Span, diagnostics, comprehensionBindings);
            StaticBindingEngine.BindLoopTargetFromIterable(clause.Target, clause.Iterable, comprehensionBindings);
            if (clause.Condition is not null)
            {
                AnalyzeExpression(clause.Condition, diagnostics, comprehensionBindings);
                if (TryResolveConditionTruth(clause.Condition, comprehensionBindings, out var conditionTruth) && !conditionTruth)
                {
                    reachable = false;
                    return comprehensionBindings;
                }

                StaticConditionRefinements.Apply(clause.Condition, assumedTruth: true, comprehensionBindings);
            }
        }

        return comprehensionBindings;
    }

    private static bool TryResolveConditionTruth(ExpressionSyntax condition, AbstractState bindings, out bool truth)
    {
        while (condition is ParenthesizedExpressionSyntax parenthesized)
        {
            condition = parenthesized.Inner;
        }

        if (condition is UnaryExpressionSyntax { Operator: UnaryOperatorSyntax.Not, Operand: var operand } &&
            TryResolveConditionTruth(operand, bindings, out var operandTruth))
        {
            truth = !operandTruth;
            return true;
        }

        if (condition is BinaryExpressionSyntax
            {
                Operator: BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And,
                Left: var logicalLeft,
                Right: var logicalRight
            } logical)
        {
            var continueTruth = logical.Operator == BinaryOperatorSyntax.And;
            if (TryResolveConditionTruth(logicalLeft, bindings, out var leftTruth))
            {
                if (leftTruth != continueTruth)
                {
                    truth = leftTruth;
                    return true;
                }

                var rightBindings = bindings.Clone();
                StaticConditionRefinements.Apply(logicalLeft, continueTruth, rightBindings);
                return TryResolveConditionTruth(logicalRight, rightBindings, out truth);
            }

            var conditionalRightBindings = bindings.Clone();
            StaticConditionRefinements.Apply(logicalLeft, continueTruth, conditionalRightBindings);
            if (TryResolveConditionTruth(logicalRight, conditionalRightBindings, out var rightTruth) &&
                rightTruth != continueTruth)
            {
                truth = rightTruth;
                return true;
            }

            truth = false;
            return false;
        }

        if (StaticAbstractValueResolver.TryResolve(condition, bindings, out var value))
        {
            if (StaticAbstractFacts.TryGetTruthiness(value, out truth))
            {
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

        static bool TryResolveNoneComparison(
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

    private interface IExpressionAnalyzer
    {
        /// <summary>Analyzes the expression when it belongs to this analyzer's syntax family.</summary>
        bool TryAnalyze(ExpressionSyntax expression, List<LythonDiagnostic> diagnostics, AbstractState bindings);
    }
}
