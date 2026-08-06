using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class PyEquality
{
    public static bool AreEqual(object left, object right)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.AreEqual(left, right);
        }

        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            return PyNumberOps.AreEqual(lhs, rhs);
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is LythonRuntime.OpenPyxlColor leftColor && right is LythonRuntime.OpenPyxlColor rightColor)
        {
            return leftColor.Equals(rightColor);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return leftText.Equals(rightText);
        }

        if (left is PyPath leftPath && right is PyPath rightPath)
        {
            return leftPath.Value.Equals(rightPath.Value);
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            if (leftList.Count != rightList.Count)
            {
                return false;
            }

            for (var i = 0; i < leftList.Count; i++)
            {
                if (!AreEqual(leftList[i], rightList[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (TryAsTupleLike(left, out var leftTuple) && TryAsTupleLike(right, out var rightTuple))
        {
            if (leftTuple.Count != rightTuple.Count)
            {
                return false;
            }

            for (var i = 0; i < leftTuple.Count; i++)
            {
                if (!AreEqual(leftTuple[i], rightTuple[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is PyDeque leftDeque && right is PyDeque rightDeque)
        {
            if (leftDeque.Count != rightDeque.Count)
            {
                return false;
            }

            for (var i = 0; i < leftDeque.Count; i++)
            {
                if (!AreEqual(leftDeque[i], rightDeque[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is PyDict leftDict && right is PyDict rightDict)
        {
            if (leftDict.Count != rightDict.Count)
            {
                return false;
            }

            foreach (var pair in leftDict)
            {
                if (!rightDict.TryGetValue(pair.Key, out var other) || !AreEqual(pair.Value, other))
                {
                    return false;
                }
            }

            return true;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return CountersEqual(leftCounter, rightCounter);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            return leftSet.SetEquals(rightSet);
        }

        if (left is PyInstance leftInstance && right is PyInstance rightInstance)
        {
            if (!ReferenceEquals(leftInstance.Type, rightInstance.Type))
            {
                return false;
            }

            if (leftInstance.Type.DataclassEqEnabled && leftInstance.Type.DataclassFields is { } fields)
            {
                foreach (var field in fields.Where(field => field.Compare))
                {
                    _ = leftInstance.TryGetOwnAttribute(field.Name, out var leftValue);
                    _ = rightInstance.TryGetOwnAttribute(field.Name, out var rightValue);
                    if (!AreEqual(leftValue ?? PyNone.Instance, rightValue ?? PyNone.Instance))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return Equals(left, right);
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

    private static bool CountersEqual(PyCounter left, PyCounter right)
    {
        var keys = new List<object>();
        foreach (var key in left.Keys.Concat(right.Keys))
        {
            if (!keys.Any(existing => AreEqual(existing, key)))
            {
                keys.Add(key);
            }
        }

        foreach (var key in keys)
        {
            var leftValue = left.TryGetValue(key, out var foundLeft) ? foundLeft : BigInteger.Zero;
            var rightValue = right.TryGetValue(key, out var foundRight) ? foundRight : BigInteger.Zero;
            if (!AreEqual(leftValue, rightValue))
            {
                return false;
            }
        }

        return true;
    }
}
