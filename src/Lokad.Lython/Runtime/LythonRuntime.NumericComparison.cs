using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static bool IsNumericComparison(BinaryOperatorSyntax op)
        => op is BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual or BinaryOperatorSyntax.Less
            or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual;

    // Exact CLR scalars have no guest slots. Share the existing exact numeric
    // ordering before structural/protocol setup; branches consume truth directly.
    private static bool TryEvaluateNumericComparison(BinaryOperatorSyntax op, object left, object right,
        ExecutionContext context, LythonSourceSpan span, out bool result)
    {
        result = false;
        if (!IsNumericComparison(op) || !PyNumberOps.TryAsNumber(left, out var lhs)
            || !PyNumberOps.TryAsNumber(right, out var rhs)) return false;
        context.CheckExecution(span);
        result = PyNumberOps.TryCompare(lhs, rhs, out var comparison)
            ? CompareOrderedResult(op, comparison) : op == BinaryOperatorSyntax.NotEqual;
        return true;
    }

    private static bool CompareOrderedResult(BinaryOperatorSyntax op, int comparison) => op switch
    {
        BinaryOperatorSyntax.Equal => comparison == 0,
        BinaryOperatorSyntax.NotEqual => comparison != 0,
        BinaryOperatorSyntax.Less => comparison < 0,
        BinaryOperatorSyntax.LessEqual => comparison <= 0,
        BinaryOperatorSyntax.Greater => comparison > 0,
        BinaryOperatorSyntax.GreaterEqual => comparison >= 0,
        _ => throw new InvalidOperationException("Unexpected numeric comparison."),
    };

    private static BinarySpecialMethodPair OrderedComparisonMethods(BinaryOperatorSyntax op) => op switch
    {
        BinaryOperatorSyntax.Less => new("__lt__", "__gt__"),
        BinaryOperatorSyntax.LessEqual => new("__le__", "__ge__"),
        BinaryOperatorSyntax.Greater => new("__gt__", "__lt__"),
        BinaryOperatorSyntax.GreaterEqual => new("__ge__", "__le__"),
        _ => throw new InvalidOperationException("Unexpected ordered comparison."),
    };

    private static string ComparisonSymbol(BinaryOperatorSyntax op) => op switch
    {
        BinaryOperatorSyntax.Less => "<",
        BinaryOperatorSyntax.LessEqual => "<=",
        BinaryOperatorSyntax.Greater => ">",
        BinaryOperatorSyntax.GreaterEqual => ">=",
        _ => throw new InvalidOperationException("Unexpected ordered comparison."),
    };
}
