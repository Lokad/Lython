namespace Lokad.Lython.Frontend;

/// <summary>Provides the direct expression children shared by syntax-oriented frontend passes.</summary>
internal static class ExpressionSyntaxTraversal
{
    public static Children EnumerateChildren(ExpressionSyntax expression) => new(expression);

    // Ordinary direct fields/list entries need no iterator object. Recursive
    // formatted/comprehension/target and optional-slice walks keep their existing
    // iterator, only when those shapes are actually encountered.
    internal readonly struct Children(ExpressionSyntax expression)
    {
        public Enumerator GetEnumerator() => new(expression);
        public bool Any(Func<ExpressionSyntax, bool> predicate)
        {
            foreach (var child in this) if (predicate(child)) return true;
            return false;
        }
    }

    internal struct Enumerator(ExpressionSyntax expression) : IDisposable
    {
        private int _index;
        private IEnumerator<ExpressionSyntax>? _nested;
        public ExpressionSyntax Current { get; private set; } = null!;

        public bool MoveNext()
        {
            if (_index < 0) return false;
            if (expression is FormattedStringExpressionSyntax or ListComprehensionExpressionSyntax or
                GeneratorExpressionSyntax or DictLiteralExpressionSyntax or SetComprehensionExpressionSyntax or
                DictComprehensionExpressionSyntax or SliceExpressionSyntax or SliceValueExpressionSyntax)
            {
                _nested ??= EnumerateComplexChildren(expression).GetEnumerator();
                if (!_nested.MoveNext()) { _index = -1; return false; }
                Current = _nested.Current;
                return true;
            }
            var index = _index++;
            Current = (expression switch
            {
                UnpackedTypeExpressionSyntax item when index == 0 => item.Value,
                YieldExpressionSyntax item when index == 0 => item.Value,
                ListLiteralExpressionSyntax item when index < item.Items.Count => item.Items[index].Expression,
                SetLiteralExpressionSyntax item when index < item.Items.Count => item.Items[index].Expression,
                TupleLiteralExpressionSyntax item when index < item.Items.Count => item.Items[index].Expression,
                ParenthesizedExpressionSyntax item when index == 0 => item.Inner,
                MemberExpressionSyntax item when index == 0 => item.Target,
                CallExpressionSyntax item when index == 0 => item.Target,
                CallExpressionSyntax item when index <= item.Arguments.Count => item.Arguments[index - 1].Expression,
                SubscriptExpressionSyntax item when index == 0 => item.Target,
                SubscriptExpressionSyntax item when index == 1 => item.Index,
                BinaryExpressionSyntax item when index == 0 => item.Left,
                BinaryExpressionSyntax item when index == 1 => item.Right,
                ChainedComparisonExpressionSyntax item when index < item.Operands.Count => item.Operands[index],
                UnaryExpressionSyntax item when index == 0 => item.Operand,
                ConditionalExpressionSyntax item when index == 0 => item.Condition,
                ConditionalExpressionSyntax item when index == 1 => item.Consequent,
                ConditionalExpressionSyntax item when index == 2 => item.Alternative,
                AssignmentExpressionSyntax item when index == 0 => item.Expression,
                LambdaExpressionSyntax item when index == 0 => item.Body,
                _ => null
            })!;
            if (Current is null) { _index = -1; return false; }
            return true;
        }

        public void Dispose()
        {
            _index = -1;
            _nested?.Dispose();
            _nested = null;
            Current = null!;
        }
    }

    private static IEnumerable<ExpressionSyntax> EnumerateComplexChildren(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
                foreach (var child in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    yield return child;
                }
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                yield return listComprehension.ItemExpression;
                foreach (var clause in listComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) yield return read;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case GeneratorExpressionSyntax generator:
                yield return generator.ItemExpression;
                foreach (var clause in generator.Clauses)
                {
                    yield return clause.Iterable;
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) yield return read;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case DictLiteralExpressionSyntax dictionary:
                foreach (var item in dictionary.Items)
                {
                    yield return item.Key;
                    if (!item.IsUnpacking) yield return item.Value;
                }
                break;

            case SetComprehensionExpressionSyntax setComprehension:
                yield return setComprehension.ItemExpression;
                foreach (var clause in setComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) yield return read;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                yield return dictComprehension.KeyExpression;
                yield return dictComprehension.ValueExpression;
                foreach (var clause in dictComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) yield return read;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case SliceExpressionSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                break;

            case SliceValueExpressionSyntax slice:
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                break;

        }
    }
}
