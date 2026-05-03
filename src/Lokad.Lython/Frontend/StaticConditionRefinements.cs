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

        if (condition is IdentifierExpressionSyntax identifier &&
            bindings.TryGet(identifier.Name, out var identifierValue) &&
            identifierValue.Kind == AbstractValueKind.MaybeRegexMatch)
        {
            bindings.Set(identifier.Name, assumedTruth
                ? AbstractValue.RegexMatch((AbstractRegexMatchSummary)identifierValue.Value, identifier.Span)
                : AbstractValue.None(identifier.Span));
            return;
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
        if (!bindings.TryGet(identifier.Name, out var value) ||
            value.Kind != AbstractValueKind.MaybeRegexMatch)
        {
            return;
        }

        bindings.Set(identifier.Name, meansNone
            ? AbstractValue.None(identifier.Span)
            : AbstractValue.RegexMatch((AbstractRegexMatchSummary)value.Value, identifier.Span));
    }
}
