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
        result = CompareStringScalars(op, leftText, rightText, context, span);
        return true;
    }

    private static bool CompareStringScalars(BinaryOperatorSyntax op, PyString left, PyString right,
        ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (ReferenceEquals(left, right)) return CompareOrderedResult(op, 0);
        var lhs = left.Utf8Span;
        var rhs = right.Utf8Span;
        if (op is BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual && lhs.Length != rhs.Length)
            return op == BinaryOperatorSyntax.NotEqual;
        var comparison = 0;
        var length = Math.Min(lhs.Length, rhs.Length);
        for (var offset = 0; offset < length;)
        {
            context.CheckExecution(span);
            var count = Math.Min(4096, length - offset);
            comparison = lhs.Slice(offset, count).SequenceCompareTo(rhs.Slice(offset, count));
            if (comparison != 0) break;
            offset += count;
        }
        context.CheckExecution(span);
        return CompareOrderedResult(op, comparison == 0 ? lhs.Length.CompareTo(rhs.Length) : comparison);
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
