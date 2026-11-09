using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static bool IsScalarComparison(BinaryOperatorSyntax op)
        => op is BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual or BinaryOperatorSyntax.Less
            or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual;

    // Exact numbers and strings have no guest slots. Expressions and branches
    // share this entry before structural/protocol setup.
    private static bool TryEvaluateScalarComparison(BinaryOperatorSyntax op, object left, object right,
        ExecutionContext context, LythonSourceSpan span, out bool result)
    {
        result = false;
        if (!IsScalarComparison(op)) return false;
        // Keep the existing numeric prefix small; the bulk text kernel does
        // not belong in every numeric comparison's dispatch body.
        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            context.CheckExecution(span);
            result = PyNumberOps.TryCompare(lhs, rhs, out var comparison)
                ? CompareOrderedResult(op, comparison) : op == BinaryOperatorSyntax.NotEqual;
            return true;
        }
        if (left is not PyString leftText || right is not PyString rightText) return false;
        result = op switch
        {
            BinaryOperatorSyntax.Equal => leftText.Equals(rightText, context, span),
            BinaryOperatorSyntax.NotEqual => !leftText.Equals(rightText, context, span),
            _ => CompareOrderedResult(op, PyString.CompareOrdinal(leftText, rightText, context, span)),
        };
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
        _ => throw new InvalidOperationException("Unexpected scalar comparison."),
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
