using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyComparison
{
    public static int Compare(object left, object right, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Compare(left, right, span);
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
            return CompareSequences(leftList, rightList, span);
        }

        if (TryAsTupleLike(left, out var leftTuple) && TryAsTupleLike(right, out var rightTuple))
        {
            return CompareSequences(leftTuple, rightTuple, span);
        }

        if (left is PyTimedelta or PyDate or PyTime or PyDateTime)
        {
            return PyDateTimeOps.Compare(left, right, span);
        }

        if (left is PyInstance leftInstance &&
            right is PyInstance rightInstance &&
            ReferenceEquals(leftInstance.Type, rightInstance.Type) &&
            leftInstance.Type.DataclassOrderEnabled)
        {
            return PyDataclass.CompareOrderedInstances(leftInstance, rightInstance, span);
        }

        throw new LythonRuntimeException("TypeError", "Values are not comparable.", span);
    }

    private static int CompareSequences(IReadOnlyList<object> left, IReadOnlyList<object> right, LythonSourceSpan span)
    {
        var common = Math.Min(left.Count, right.Count);
        for (var i = 0; i < common; i++)
        {
            if (PyEquality.AreEqual(left[i], right[i]))
            {
                continue;
            }

            return Compare(left[i], right[i], span);
        }

        return left.Count.CompareTo(right.Count);
    }

    private static bool TryAsTupleLike(object value, out IReadOnlyList<object> sequence)
    {
        switch (value)
        {
            case PyTuple tuple:
                sequence = tuple;
                return true;
            case PyNamedTupleObject namedTuple:
                sequence = namedTuple;
                return true;
            case PyTypingNamedTupleObject typingNamedTuple:
                sequence = typingNamedTuple;
                return true;
            case LythonRuntime.TimeStructTimeValue structTime:
                sequence = structTime;
                return true;
            default:
                sequence = Array.Empty<object>();
                return false;
        }
    }
}
