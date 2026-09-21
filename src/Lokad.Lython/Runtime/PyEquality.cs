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
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                if (leftList.Count != rightList.Count)
                {
                    return false;
                }

                for (var i = 0; i < leftList.Count; i++)
                {
                    PyStructuralGuard.NoteWork();
                    if (!AreEqual(leftList[i], rightList[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        if (PyTupleLike.TryGetItems(left, out var leftTuple) && PyTupleLike.TryGetItems(right, out var rightTuple))
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                if (leftTuple.Count != rightTuple.Count)
                {
                    return false;
                }

                for (var i = 0; i < leftTuple.Count; i++)
                {
                    PyStructuralGuard.NoteWork();
                    if (!AreEqual(leftTuple[i], rightTuple[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        if (left is PyDeque leftDeque && right is PyDeque rightDeque)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
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
                    PyStructuralGuard.NoteWork();
                    if (!AreEqual(leftItems.Current, rightItems.Current))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // Slices compare structurally like CPython; bounds compare through
        // shared equality over the null-normalised bound shape.
        if (left is PySlice leftSlice && right is PySlice rightSlice)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return AreEqual(leftSlice.StartBound ?? PyNone.Instance, rightSlice.StartBound ?? PyNone.Instance)
                    && AreEqual(leftSlice.StopBound ?? PyNone.Instance, rightSlice.StopBound ?? PyNone.Instance)
                    && AreEqual(leftSlice.StepBound ?? PyNone.Instance, rightSlice.StepBound ?? PyNone.Instance);
            }
        }

        if (left is PyDict leftDict && right is PyDict rightDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return DictsEqual(leftDict, rightDict);
            }
        }

        if (left is PyCounter counterLeft && (right is PyDict || right is PyDefaultDict))
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return CounterDictContentEquals(counterLeft, right);
            }
        }

        if (right is PyCounter counterRight && (left is PyDict || left is PyDefaultDict))
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return CounterDictContentEquals(counterRight, left);
            }
        }

        if (left is PyDefaultDict leftDefaultDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return DefaultDictContentEquals(leftDefaultDict, right);
            }
        }

        if (right is PyDefaultDict rightDefaultDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return DefaultDictContentEquals(rightDefaultDict, left);
            }
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return CountersEqual(leftCounter, rightCounter);
            }
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                return leftSet.SetEquals(rightSet);
            }
        }

        if (left is PyRange leftRange && right is PyRange rightRange)
        {
            // Ranges compare as sequences like CPython: equal lengths
            // with matching starts, ignoring step on singletons.
            return leftRange.Length == rightRange.Length
                && (leftRange.Length.IsZero
                    || (leftRange.Start == rightRange.Start
                        && (leftRange.Length == BigInteger.One
                            || leftRange.Step == rightRange.Step)));
        }

        if (left is LythonRuntime.DictKeysView or LythonRuntime.DictItemsView or ChainMapKeysView or ChainMapItemsView || right is LythonRuntime.DictKeysView or LythonRuntime.DictItemsView or ChainMapKeysView or ChainMapItemsView)
        {
            using (PyStructuralGuard.EnterPair(left, right, null))
            {
                // Dict keys and items views compare as sets like CPython;
                // anything else falls through to the default comparison.
                if (!IsSetComparableViewOperand(left) || !IsSetComparableViewOperand(right))
                {
                    return false;
                }

                var leftItems = new HashSet<object>(ViewComparisonItems(left), PyValueComparer.Instance);
                return leftItems.SetEquals(ViewComparisonItems(right));
            }
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
                using (PyStructuralGuard.EnterPair(left, right, null))
                {
                    foreach (var field in fields)
                    {
                        PyStructuralGuard.NoteWork();
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
        }

        // Bound methods compare by receiver and function like CPython:
        // plain-function identity rides __func__ while engine methods, which
        // expose no __func__, compare their short names. Both sides must be
        // method-shaped; receivers compare by identity, never by value.
        if ((left is LythonRuntime.IPyBoundEngineMethod || left is PyBoundMethod) &&
            (right is LythonRuntime.IPyBoundEngineMethod || right is PyBoundMethod))
        {
            return BoundMethodsEqual(left, right);
        }

        // Builtin classmethods and staticmethods compare by owner and member
        // like CPython (fresh per read, so identity never holds for the
        // classmethod shape); hashes combine the same pair, keeping dict
        // keys coherent.
        if (left is LythonRuntime.BuiltinTypeMethod && right is LythonRuntime.BuiltinTypeMethod)
        {
            return BuiltinTypeMethodsEqual(left, right);
        }

        if (left is LythonRuntime.IPyRawBoundCallable && right is LythonRuntime.IPyRawBoundCallable)
        {
            return RawBoundCallablesEqual(left, right);
        }

        return Equals(left, right);
    }

    private static bool IsSetComparableViewOperand(object value)
        => value is PySet or LythonRuntime.DictKeysView or LythonRuntime.DictItemsView or ChainMapKeysView or ChainMapItemsView;

    private static IEnumerable<object> ViewComparisonItems(object value) => value switch
    {
        PySet set => set,
        LythonRuntime.DictKeysView keys => keys,
        LythonRuntime.DictItemsView items => items,
        ChainMapKeysView chainKeys => chainKeys,
        ChainMapItemsView chainItems => chainItems,
        _ => [],
    };


    private static bool BoundMethodsEqual(object left, object right)
    {
        if (left is not IPyDynamicAttributes leftAttributes ||
            right is not IPyDynamicAttributes rightAttributes ||
            !leftAttributes.TryGetMember("__self__", out var leftSelf) || leftSelf is null ||
            !rightAttributes.TryGetMember("__self__", out var rightSelf) || rightSelf is null ||
            !ReferenceEquals(leftSelf, rightSelf))
        {
            return false;
        }

        var leftHasFunc = leftAttributes.TryGetMember("__func__", out var leftFunc);
        var rightHasFunc = rightAttributes.TryGetMember("__func__", out var rightFunc);
        if (leftHasFunc || rightHasFunc)
        {
            return leftHasFunc && rightHasFunc && ReferenceEquals(leftFunc, rightFunc);
        }

        return leftAttributes.TryGetMember("__name__", out var leftName) &&
            rightAttributes.TryGetMember("__name__", out var rightName) &&
            leftName is PyString leftText &&
            rightName is PyString rightText &&
            string.Equals(leftText.AsString(), rightText.AsString(), StringComparison.Ordinal);
    }

    private static bool RawBoundCallablesEqual(object left, object right)
    {
        return left is LythonRuntime.IPyRawBoundCallable leftCallable &&
            right is LythonRuntime.IPyRawBoundCallable rightCallable &&
            leftCallable.BoundName is not null &&
            string.Equals(leftCallable.BoundName, rightCallable.BoundName, StringComparison.Ordinal) &&
            ReferenceEquals(leftCallable.BoundReceiver, rightCallable.BoundReceiver);
    }

    private static bool DictContentEqual(
        int leftCount,
        IEnumerable<KeyValuePair<object, object>> leftPairs,
        int rightCount,
        Func<object, (bool Found, object? Value)> rightLookup)
    {
        if (leftCount != rightCount)
        {
            return false;
        }

        foreach (var pair in leftPairs)
        {
            PyStructuralGuard.NoteWork();
            var (found, other) = rightLookup(pair.Key);
            // A miss carries the null (short-circuited above); every hit flows
            // from a successful TryGetValue whose contract guarantees non-null.
            if (!found || !AreEqual(pair.Value, other!))
            {
                return false;
            }
        }

        return true;
    }

    // The dict lookup lambda captures rightDict, so Roslyn instantiates its
    // closure on every AreEqual call; keep it in a callee so only dict
    // comparisons pay for it (same pattern as the operator counter branches).
    private static bool DictsEqual(PyDict leftDict, PyDict rightDict)
        => DictContentEqual(
            leftDict.Count,
            leftDict,
            rightDict.Count,
            key => rightDict.TryGetValue(key, out var value) ? (true, value) : (false, null));

    // Counter equality treats absent keys as having a zero count, unlike
    // ordinary dictionary equality, so compare the union of both key sets.
    internal static bool CountersEqual(PyCounter left, PyCounter right)
    {
        var keys = new HashSet<object>(left.Keys, PyValueComparer.Instance);
        keys.UnionWith(right.Keys);

        foreach (var key in keys)
        {
            PyStructuralGuard.NoteWork();
            var leftValue = left.TryGetValue(key, out var foundLeft) ? foundLeft : BigInteger.Zero;
            var rightValue = right.TryGetValue(key, out var foundRight) ? foundRight : BigInteger.Zero;
            if (!AreEqual(leftValue, rightValue))
            {
                return false;
            }
        }

        return true;
    }


    // A Counter facing a plain or default dict compares exactly like dicts;
    // only Counter-facing-Counter fills absent keys with zero counts.
    private static bool CounterDictContentEquals(PyCounter counter, object other)
    {
        if (other is PyDict plain)
        {
            return DictContentEqual(
                counter.Count,
                counter.Items,
                plain.Count,
                key => plain.TryGetValue(key, out var value) ? (true, value) : (false, null));
        }

        if (other is PyDefaultDict fellow)
        {
            return DictContentEqual(
                counter.Count,
                counter.Items,
                fellow.Count,
                key => fellow.TryGetValue(key, out var value) ? (true, value) : (false, null));
        }

        return false;
    }

    private static bool DefaultDictContentEquals(PyDefaultDict candidate, object other)
    {
        if (other is PyDict plain)
        {
            return DictContentEqual(
                candidate.Count,
                candidate.Items,
                plain.Count,
                key => plain.TryGetValue(key, out var value) ? (true, value) : (false, null));
        }

        if (other is PyDefaultDict fellow)
        {
            return DictContentEqual(
                candidate.Count,
                candidate.Items,
                fellow.Count,
                key => fellow.TryGetValue(key, out var value) ? (true, value) : (false, null));
        }

        // A Counter is a dict for equality like CPython, so compare by
        // content instead of declining.
        if (other is PyCounter counter)
        {
            return DictContentEqual(
                candidate.Count,
                candidate.Items,
                counter.Count,
                key => counter.TryGetValue(key, out var value) ? (true, value) : (false, null));
        }

        return false;
    }

    private static bool BuiltinTypeMethodsEqual(object left, object right)
    {
        return left is LythonRuntime.BuiltinTypeMethod leftMethod &&
            right is LythonRuntime.BuiltinTypeMethod rightMethod &&
            string.Equals(leftMethod.OwnerName, rightMethod.OwnerName, StringComparison.Ordinal) &&
            string.Equals(leftMethod.MemberName, rightMethod.MemberName, StringComparison.Ordinal);
    }

}
