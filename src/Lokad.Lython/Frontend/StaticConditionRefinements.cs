namespace Lokad.Lython.Frontend;

internal static class StaticConditionRefinements
{
    public static void Apply(ExpressionSyntax condition, bool assumedTruth, AbstractState bindings)
    {
        while (condition is ParenthesizedExpressionSyntax parenthesized)
        {
            condition = parenthesized.Inner;
        }

        if (condition is UnaryExpressionSyntax { Operator: UnaryOperatorSyntax.Not, Operand: var operand })
        {
            Apply(operand, !assumedTruth, bindings);
            return;
        }

        if (condition is BinaryExpressionSyntax
            {
                Operator: BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And,
                Left: var logicalLeft,
                Right: var logicalRight
            } logical)
        {
            ApplyLogical(logical.Operator, logicalLeft, logicalRight, assumedTruth, bindings);
            return;
        }

        if (condition is IdentifierExpressionSyntax identifier &&
            bindings.TryGet(identifier.Name, out var identifierValue))
        {
            if (identifierValue.Kind == AbstractValueKind.MaybeRegexMatch)
            {
                bindings.Set(identifier.Name, assumedTruth
                    ? AbstractValue.RegexMatch((AbstractRegexMatchSummary)identifierValue.Value, identifier.Span)
                    : AbstractValue.None(identifier.Span));
                return;
            }

            if (identifierValue.Kind == AbstractValueKind.MaybeNone)
            {
                var nonNoneValue = ((AbstractValue)identifierValue.Value).WithSpan(identifier.Span);
                if (assumedTruth)
                {
                    bindings.Set(identifier.Name, nonNoneValue);
                }
                else if (StaticAbstractFacts.TryGetTruthiness(nonNoneValue, out var innerTruth) && innerTruth)
                {
                    bindings.Set(identifier.Name, AbstractValue.None(identifier.Span));
                }

                return;
            }
        }

        if (condition is BinaryExpressionSyntax
            {
                Operator: BinaryOperatorSyntax.Is or BinaryOperatorSyntax.IsNot,
                Left: var left,
                Right: var right
            } binary)
        {
            ApplyNoneComparison(binary.Operator, left, right, assumedTruth, bindings);
        }
    }

    private static void ApplyLogical(
        BinaryOperatorSyntax op,
        ExpressionSyntax left,
        ExpressionSyntax right,
        bool assumedTruth,
        AbstractState bindings)
    {
        if (op == BinaryOperatorSyntax.Or && !assumedTruth ||
            op == BinaryOperatorSyntax.And && assumedTruth)
        {
            Apply(left, assumedTruth, bindings);
            Apply(right, assumedTruth, bindings);
            return;
        }

        var shortCircuitBindings = bindings.Clone();
        Apply(left, assumedTruth, shortCircuitBindings);

        var rightBindings = bindings.Clone();
        Apply(left, !assumedTruth, rightBindings);
        Apply(right, assumedTruth, rightBindings);

        bindings.MergeFrom(shortCircuitBindings, rightBindings);
    }

    private static void ApplyNoneComparison(
        BinaryOperatorSyntax op,
        ExpressionSyntax left,
        ExpressionSyntax right,
        bool assumedTruth,
        AbstractState bindings)
    {
        if (TryGetIdentifierComparedToNone(left, right, out var identifier) ||
            TryGetIdentifierComparedToNone(right, left, out identifier))
        {
            var meansNone = op == BinaryOperatorSyntax.Is ? assumedTruth : !assumedTruth;
            ApplyNoneRefinement(identifier, meansNone, bindings);
        }
    }

    private static bool TryGetIdentifierComparedToNone(
        ExpressionSyntax candidate,
        ExpressionSyntax other,
        out IdentifierExpressionSyntax identifier)
    {
        while (candidate is ParenthesizedExpressionSyntax candidateParenthesized)
        {
            candidate = candidateParenthesized.Inner;
        }

        while (other is ParenthesizedExpressionSyntax otherParenthesized)
        {
            other = otherParenthesized.Inner;
        }

        if (candidate is IdentifierExpressionSyntax candidateIdentifier &&
            other is NoneLiteralExpressionSyntax)
        {
            identifier = candidateIdentifier;
            return true;
        }

        identifier = default!;
        return false;
    }

    private static void ApplyNoneRefinement(IdentifierExpressionSyntax identifier, bool meansNone, AbstractState bindings)
    {
        if (!bindings.TryGet(identifier.Name, out var value))
        {
            return;
        }

        if (value.Kind == AbstractValueKind.MaybeRegexMatch)
        {
            bindings.Set(identifier.Name, meansNone
                ? AbstractValue.None(identifier.Span)
                : AbstractValue.RegexMatch((AbstractRegexMatchSummary)value.Value, identifier.Span));
            return;
        }

        if (value.Kind == AbstractValueKind.MaybeNone)
        {
            bindings.Set(identifier.Name, meansNone
                ? AbstractValue.None(identifier.Span)
                : ((AbstractValue)value.Value).WithSpan(identifier.Span));
        }
    }
}
