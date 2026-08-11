using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class PyEquality
{
    public static bool AreEqual(object left, object right)
    {
        // Decimal coercion has its own Python rules and must run before the broader
        // numeric tower. In particular, falling back to CLR Equals would make equal
        // values with different runtime representations compare unequal.
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.AreEqual(left, right);
        }

        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            return PyNumberOps.AreEqual(lhs, rhs);
        }

        // Keep identity after numeric comparison: a boxed NaN must remain unequal to
        // itself even when both operands happen to reference the same boxed object.
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is IPythonExceptionType leftExceptionType && right is IPythonExceptionType rightExceptionType)
        {
            return leftExceptionType.ExceptionIdentity == rightExceptionType.ExceptionIdentity;
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

        if (PyTupleLike.TryGetItems(left, out var leftTuple) && PyTupleLike.TryGetItems(right, out var rightTuple))
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

            using var leftItems = leftDeque.GetEnumerator();
            using var rightItems = rightDeque.GetEnumerator();
            while (leftItems.MoveNext())
            {
                _ = rightItems.MoveNext();
                if (!AreEqual(leftItems.Current, rightItems.Current))
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

            static bool CountersEqual(PyCounter left, PyCounter right)
            {
                // Counter equality treats absent keys as having a zero count, unlike
                // ordinary dictionary equality, so compare the union of both key sets.
                var keys = new HashSet<object>(left.Keys, PyValueComparer.Instance);
                keys.UnionWith(right.Keys);

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

        if (left is PySet leftSet && right is PySet rightSet)
        {
            return leftSet.SetEquals(rightSet);
        }

        if (left is PyInstance leftInstance && right is PyInstance rightInstance)
        {
            // Generated dataclass equality is intentionally exact-type equality; base
            // and derived dataclass instances do not compare field-by-field in Python.
            if (!ReferenceEquals(leftInstance.Type, rightInstance.Type))
            {
                return false;
            }

            if (leftInstance.Type.DataclassEqEnabled && leftInstance.Type.DataclassComparableFields is { } fields)
            {
                foreach (var field in fields)
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

}
