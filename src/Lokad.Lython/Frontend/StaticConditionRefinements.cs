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

        if (condition is BinaryExpressionSyntax comparison &&
            TryApplyLengthComparison(comparison.Left, comparison.Operator, comparison.Right, assumedTruth, bindings))
        {
            return;
        }

        if (condition is ChainedComparisonExpressionSyntax chained &&
            TryApplyChainedLengthComparison(chained, assumedTruth, bindings))
        {
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

    private static bool TryApplyChainedLengthComparison(
        ChainedComparisonExpressionSyntax comparison,
        bool assumedTruth,
        AbstractState bindings)
    {
        if (assumedTruth)
        {
            var recognized = false;
            for (var i = 0; i < comparison.Operators.Count; i++)
            {
                recognized |= TryApplyLengthComparison(
                    comparison.Operands[i],
                    comparison.Operators[i],
                    comparison.Operands[i + 1],
                    assumedTruth: true,
                    bindings);
            }

            return recognized;
        }

        AbstractState? failedPaths = null;
        var priorComparisons = bindings.Clone();
        var recognizedFailure = false;
        for (var i = 0; i < comparison.Operators.Count; i++)
        {
            var failedComparison = priorComparisons.Clone();
            recognizedFailure |= TryApplyLengthComparison(
                comparison.Operands[i],
                comparison.Operators[i],
                comparison.Operands[i + 1],
                assumedTruth: false,
                failedComparison);
            failedPaths = failedPaths is null
                ? failedComparison
                : AbstractState.Merge(failedPaths, failedComparison);

            TryApplyLengthComparison(
                comparison.Operands[i],
                comparison.Operators[i],
                comparison.Operands[i + 1],
                assumedTruth: true,
                priorComparisons);
        }

        if (recognizedFailure)
        {
            bindings.ReplaceWith(failedPaths!);
        }

        return recognizedFailure;
    }

    private static bool TryApplyLengthComparison(
        ExpressionSyntax left,
        BinaryOperatorSyntax op,
        ExpressionSyntax right,
        bool assumedTruth,
        AbstractState bindings)
    {
        string sequenceName;
        int length;
        if (TryGetLengthTarget(left, bindings, out sequenceName) && TryGetKnownInteger(right, bindings, out length))
        {
            // Already normalized as len(sequence) <op> integer.
        }
        else if (TryGetLengthTarget(right, bindings, out sequenceName) && TryGetKnownInteger(left, bindings, out length))
        {
            op = ReverseComparison(op);
        }
        else
        {
            return false;
        }

        if (!IsOrderingOrEqualityComparison(op))
        {
            return false;
        }

        if (!assumedTruth)
        {
            op = NegateComparison(op);
        }

        var current = bindings.TryGetSequenceLength(sequenceName, out var knownBounds)
            ? knownBounds
            : new AbstractSequenceLengthBounds(0, null);
        bindings.SetSequenceLength(sequenceName, Refine(current, op, length));
        return true;
    }

    private static bool TryGetLengthTarget(ExpressionSyntax expression, AbstractState bindings, out string sequenceName)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (bindings.TryGet("len", out _) ||
            expression is not CallExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "len" },
                Arguments: [{ Kind: CallArgumentKind.Positional, Expression: var argument }]
            })
        {
            sequenceName = string.Empty;
            return false;
        }

        while (argument is ParenthesizedExpressionSyntax argumentParenthesized)
        {
            argument = argumentParenthesized.Inner;
        }

        if (argument is IdentifierExpressionSyntax identifier &&
            bindings.TryGet(identifier.Name, out var value) &&
            StaticAbstractFacts.IsDefinitelySized(value))
        {
            sequenceName = identifier.Name;
            return true;
        }

        sequenceName = string.Empty;
        return false;
    }

    private static bool TryGetKnownInteger(ExpressionSyntax expression, AbstractState bindings, out int integer)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (expression is UnaryExpressionSyntax
            {
                Operator: UnaryOperatorSyntax.Minus,
                Operand: var operand
            } &&
            StaticAbstractValueResolver.TryResolve(operand, bindings, out var unsignedValue) &&
            StaticAbstractFacts.TryGetInt32(unsignedValue, out var unsignedInteger) &&
            unsignedInteger != int.MinValue)
        {
            integer = -unsignedInteger;
            return true;
        }

        if (StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) &&
            StaticAbstractFacts.TryGetInt32(value, out integer))
        {
            return true;
        }

        integer = 0;
        return false;
    }

    private static AbstractSequenceLengthBounds Refine(
        AbstractSequenceLengthBounds current,
        BinaryOperatorSyntax op,
        int length)
    {
        if (current.IsImpossible)
        {
            return current;
        }

        if (op == BinaryOperatorSyntax.NotEqual)
        {
            if (length < 0)
            {
                return current;
            }

            if (length == 0)
            {
                return Intersect(current, new AbstractSequenceLengthBounds(1, null));
            }

            if (current.MinimumLength == length && current.MaximumLength == length)
            {
                return AbstractSequenceLengthBounds.Impossible;
            }

            if (current.MaximumLength == length)
            {
                return Intersect(current, new AbstractSequenceLengthBounds(null, length - 1));
            }

            if (current.MinimumLength == length && length < int.MaxValue)
            {
                return Intersect(current, new AbstractSequenceLengthBounds(length + 1, null));
            }

            return current;
        }

        var constraint = op switch
        {
            BinaryOperatorSyntax.Less when length <= 0 => AbstractSequenceLengthBounds.Impossible,
            BinaryOperatorSyntax.Less => new AbstractSequenceLengthBounds(null, length - 1),
            BinaryOperatorSyntax.LessEqual when length < 0 => AbstractSequenceLengthBounds.Impossible,
            BinaryOperatorSyntax.LessEqual => new AbstractSequenceLengthBounds(null, length),
            BinaryOperatorSyntax.Greater when length == int.MaxValue => AbstractSequenceLengthBounds.Impossible,
            BinaryOperatorSyntax.Greater => new AbstractSequenceLengthBounds(Math.Max(0, length + 1), null),
            BinaryOperatorSyntax.GreaterEqual => new AbstractSequenceLengthBounds(Math.Max(0, length), null),
            BinaryOperatorSyntax.Equal when length < 0 => AbstractSequenceLengthBounds.Impossible,
            BinaryOperatorSyntax.Equal => AbstractSequenceLengthBounds.Exact(length),
            _ => current
        };

        return Intersect(current, constraint);
    }

    private static AbstractSequenceLengthBounds Intersect(
        AbstractSequenceLengthBounds current,
        AbstractSequenceLengthBounds constraint)
    {
        if (current.IsImpossible || constraint.IsImpossible)
        {
            return AbstractSequenceLengthBounds.Impossible;
        }

        var minimum = current.MinimumLength is int currentMinimum
            ? constraint.MinimumLength is int constraintMinimum
                ? Math.Max(currentMinimum, constraintMinimum)
                : currentMinimum
            : constraint.MinimumLength;
        var maximum = current.MaximumLength is int currentMaximum
            ? constraint.MaximumLength is int constraintMaximum
                ? Math.Min(currentMaximum, constraintMaximum)
                : currentMaximum
            : constraint.MaximumLength;

        return minimum is int boundedMinimum && maximum is int boundedMaximum && boundedMinimum > boundedMaximum
            ? AbstractSequenceLengthBounds.Impossible
            : new AbstractSequenceLengthBounds(minimum, maximum);
    }

    private static bool IsOrderingOrEqualityComparison(BinaryOperatorSyntax op)
        => op is BinaryOperatorSyntax.Less or
            BinaryOperatorSyntax.LessEqual or
            BinaryOperatorSyntax.Greater or
            BinaryOperatorSyntax.GreaterEqual or
            BinaryOperatorSyntax.Equal or
            BinaryOperatorSyntax.NotEqual;

    private static BinaryOperatorSyntax ReverseComparison(BinaryOperatorSyntax op)
        => op switch
        {
            BinaryOperatorSyntax.Less => BinaryOperatorSyntax.Greater,
            BinaryOperatorSyntax.LessEqual => BinaryOperatorSyntax.GreaterEqual,
            BinaryOperatorSyntax.Greater => BinaryOperatorSyntax.Less,
            BinaryOperatorSyntax.GreaterEqual => BinaryOperatorSyntax.LessEqual,
            _ => op
        };

    private static BinaryOperatorSyntax NegateComparison(BinaryOperatorSyntax op)
        => op switch
        {
            BinaryOperatorSyntax.Less => BinaryOperatorSyntax.GreaterEqual,
            BinaryOperatorSyntax.LessEqual => BinaryOperatorSyntax.Greater,
            BinaryOperatorSyntax.Greater => BinaryOperatorSyntax.LessEqual,
            BinaryOperatorSyntax.GreaterEqual => BinaryOperatorSyntax.Less,
            BinaryOperatorSyntax.Equal => BinaryOperatorSyntax.NotEqual,
            BinaryOperatorSyntax.NotEqual => BinaryOperatorSyntax.Equal,
            _ => op
        };
}
