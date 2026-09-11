using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyComparison
{
    private static LythonRuntimeException CompareFailed(string? operation, object left, object right, LythonSourceSpan span)
        => operation is null
            ? new LythonRuntimeException("TypeError", "Values are not comparable.", span)
            : RuntimeErrors.UnsupportedComparison(operation, left, right, span);

    public static int Compare(object left, object right, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Compare(left, right, span, operation);
        }

        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            return PyNumberOps.Compare(lhs, rhs);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return PyString.CompareOrdinal(leftText, rightText);
        }

        if (left is PyPath leftPath && right is PyPath rightPath)
        {
            return PyString.CompareOrdinal(leftPath.Value, rightPath.Value);
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            return CompareSequences(leftList, rightList, span, operation);
        }

        if (PyTupleLike.TryGetItems(left, out var leftTuple) && PyTupleLike.TryGetItems(right, out var rightTuple))
        {
            return CompareSequences(leftTuple, rightTuple, span, operation);
        }

        if (left is PyTimedelta or PyDate or PyTime or PyDateTime)
        {
            return PyDateTimeOps.Compare(left, right, span, operation);
        }

        if (left is PyInstance leftInstance &&
            right is PyInstance rightInstance &&
            ReferenceEquals(leftInstance.Type, rightInstance.Type) &&
            leftInstance.Type.DataclassOrderEnabled)
        {
            return PyDataclass.CompareOrderedInstances(leftInstance, rightInstance, span);
        }

        throw CompareFailed(operation, left, right, span);
    }

    private static int CompareSequences(IReadOnlyList<object> left, IReadOnlyList<object> right, LythonSourceSpan span, string? operation = null)
    {
        var common = Math.Min(left.Count, right.Count);
        for (var i = 0; i < common; i++)
        {
            if (PyEquality.AreEqual(left[i], right[i]))
            {
                continue;
            }

            return Compare(left[i], right[i], span, operation);
        }

        return left.Count.CompareTo(right.Count);
    }

}
