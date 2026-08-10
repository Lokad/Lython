namespace Lokad.Lython.Frontend;

/// <summary>Provides the direct expression children shared by syntax-oriented frontend passes.</summary>
internal static class ExpressionSyntaxTraversal
{
    public static IEnumerable<ExpressionSyntax> EnumerateChildren(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
                foreach (var child in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    yield return child;
                }
                break;

            case ListLiteralExpressionSyntax list:
                foreach (var child in list.Items) yield return child.Expression;
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                yield return listComprehension.ItemExpression;
                foreach (var clause in listComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case GeneratorExpressionSyntax generator:
                yield return generator.ItemExpression;
                foreach (var clause in generator.Clauses)
                {
                    yield return clause.Iterable;
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

            case SetLiteralExpressionSyntax set:
                foreach (var child in set.Items) yield return child.Expression;
                break;

            case SetComprehensionExpressionSyntax setComprehension:
                yield return setComprehension.ItemExpression;
                foreach (var clause in setComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                yield return dictComprehension.KeyExpression;
                yield return dictComprehension.ValueExpression;
                foreach (var clause in dictComprehension.Clauses)
                {
                    yield return clause.Iterable;
                    if (clause.Condition is not null) yield return clause.Condition;
                }
                break;

            case TupleLiteralExpressionSyntax tuple:
                foreach (var child in tuple.Items) yield return child.Expression;
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                yield return parenthesized.Inner;
                break;

            case MemberExpressionSyntax member:
                yield return member.Target;
                break;

            case CallExpressionSyntax call:
                yield return call.Target;
                foreach (var argument in call.Arguments) yield return argument.Expression;
                break;

            case SubscriptExpressionSyntax subscript:
                yield return subscript.Target;
                yield return subscript.Index;
                break;

            case SliceExpressionSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                break;

            case BinaryExpressionSyntax binary:
                yield return binary.Left;
                yield return binary.Right;
                break;

            case ChainedComparisonExpressionSyntax chained:
                foreach (var child in chained.Operands) yield return child;
                break;

            case UnaryExpressionSyntax unary:
                yield return unary.Operand;
                break;

            case ConditionalExpressionSyntax conditional:
                yield return conditional.Condition;
                yield return conditional.Consequent;
                yield return conditional.Alternative;
                break;

            case AssignmentExpressionSyntax assignment:
                yield return assignment.Expression;
                break;

            case LambdaExpressionSyntax lambda:
                yield return lambda.Body;
                break;
        }
    }
}
