using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyDict ExecuteExecutableMakeDict(ExecutableValueStack stack, int pairCount, LythonSourceSpan span, ExecutionContext context)
    {
        var valueCount = checked(pairCount * 2);
        if (valueCount < 0 || stack.Count < valueCount)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - valueCount;
        var dict = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < valueCount; i += 2)
        {
            var key = ValidateDictionaryKey(stack[start + i], span);
            dict.SetItem(key, stack[start + i + 1]);
        }

        stack.RemoveTail(valueCount);

        context.ObserveCollectionCount(dict.Count, span);
        return dict;
    }

    private static PySet ExecuteExecutableMakeSet(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        var set = new PySet(context.MemoryGovernor, span);
        for (var i = start; i < stack.Count; i++)
        {
            set.Add(ValidateSetItem(stack[i], span));
        }

        stack.RemoveTail(count);

        context.ObserveCollectionCount(set.Count, span);
        return set;
    }

    private static object ExecuteExecutableSubscript(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        if (target is PyInstance instance)
        {
            return GetUserItem(instance, index, context, span);
        }

        return PyIndexing.ReadIndex(target, CoerceIndexProtocol(index, context, span), span);
    }

    private static IPyContextManager PopContextManager(ExecutableValueStack stack, LythonSourceSpan span)
    {
        var value = Pop(stack, span);
        if (value is IPyContextManager manager)
        {
            return manager;
        }

        throw RuntimeErrors.Type("Object does not support the context manager protocol.", span);
    }

    private static object ExecuteExecutableSlice(ExecutableValueStack stack, ExecutableSliceParts parts, LythonSourceSpan span, ExecutionContext context)
    {
        object? step = null;
        object? end = null;
        object? start = null;

        if ((parts & ExecutableSliceParts.Step) != 0)
        {
            step = Pop(stack, span);
        }
        if ((parts & ExecutableSliceParts.End) != 0)
        {
            end = Pop(stack, span);
        }
        if ((parts & ExecutableSliceParts.Start) != 0)
        {
            start = Pop(stack, span);
        }

        var target = Pop(stack, span);
        return PyIndexing.ReadSlice(target, start, end, step, span);
    }

    private static bool TryReadExecutableMemberCache(object target, ExecutableMemberCache cache, out object? value)
    {
        // Identity is required: equal mutable Python values can expose different
        // instance members, while cacheable builtin targets have stable lookup rules.
        if (ReferenceEquals(cache.Target, target))
        {
            value = cache.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static void TryWriteExecutableMemberCache(object target, object value, ExecutableMemberCache cache)
    {
        if (!CanCacheRuntimeMemberTarget(target))
        {
            cache.Target = null;
            cache.Value = null;
            return;
        }

        cache.Target = target;
        cache.Value = value;
    }

    private static bool CanCacheRuntimeMemberTarget(object target)
        => target is not IPyContextualDynamicAttributes &&
           target is (PyString
               or PyBytes
               or PyList
               or PyTuple
               or PyDict
               or PySet
               or PyDecimal
               or PyDate
               or PyTime
               or PyDateTime
               or PyTimedelta
               or PyTimezone
               or RePatternObject
               or ReMatchObject
               or PyPath
               or PyModule);

    private static object Pop(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        return stack.Pop();
    }

    private static object Peek(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        return stack.Peek();
    }

    private static IEnumerator<object> PeekIterator(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0 || stack.Peek() is not IEnumerator<object> iterator)
        {
            throw RuntimeErrors.Runtime("executable iterator stack is invalid", span);
        }

        return iterator;
    }

    private static PyList CreateListFromStack(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RemoveTail(count);
        return new PyList(items, context.MemoryGovernor, span);
    }

    private static PyTuple CreateTupleFromStack(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RemoveTail(count);
        return new PyTuple(items, context.MemoryGovernor, span);
    }

    private static object EvaluateExecutableBinary(ExecutableBinaryOperator op, object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (TryEvaluateExecutableNumericProtocol(op, left, right, context, span, out var protocolResult))
        {
            return protocolResult;
        }

        return op switch
        {
            ExecutableBinaryOperator.Add => EvaluateAdd(left, right, context, span),
            ExecutableBinaryOperator.Subtract => EvaluateSubtract(left, right, span),
            ExecutableBinaryOperator.Multiply => EvaluateMultiply(left, right, context, span),
            ExecutableBinaryOperator.Divide => EvaluateDivide(left, right, span),
            ExecutableBinaryOperator.FloorDivide => EvaluateFloorDivide(left, right, span),
            ExecutableBinaryOperator.Modulo => EvaluateModulo(left, right, context, span),
            ExecutableBinaryOperator.Power => EvaluatePower(left, right, context, span),
            ExecutableBinaryOperator.BitwiseOr => EvaluateBitwiseOr(left, right, span),
            ExecutableBinaryOperator.BitwiseXor => EvaluateBitwiseXor(left, right, span),
            ExecutableBinaryOperator.BitwiseAnd => EvaluateBitwiseAnd(left, right, span),
            ExecutableBinaryOperator.LeftShift => EvaluateLeftShift(left, right, context, span),
            ExecutableBinaryOperator.RightShift => EvaluateRightShift(left, right, span),
            ExecutableBinaryOperator.Less => EvaluateRichComparison(left, right, "__lt__", "__gt__", context, span, static value => value < 0),
            ExecutableBinaryOperator.LessEqual => EvaluateRichComparison(left, right, "__le__", "__ge__", context, span, static value => value <= 0),
            ExecutableBinaryOperator.Greater => EvaluateRichComparison(left, right, "__gt__", "__lt__", context, span, static value => value > 0),
            ExecutableBinaryOperator.GreaterEqual => EvaluateRichComparison(left, right, "__ge__", "__le__", context, span, static value => value >= 0),
            ExecutableBinaryOperator.Is => AreIdentical(left, right),
            ExecutableBinaryOperator.IsNot => !AreIdentical(left, right),
            ExecutableBinaryOperator.In => Contains(right, left, context, span),
            ExecutableBinaryOperator.NotIn => !Contains(right, left, context, span),
            ExecutableBinaryOperator.Equal => AreEqualWithProtocols(left, right, context, span),
            ExecutableBinaryOperator.NotEqual => !AreEqualWithProtocols(left, right, context, span),
            _ => throw new InvalidOperationException($"Executable IR contains unknown binary operator {op}."),
        };
    }

    private static object EvaluateExecutableUnary(ExecutableUnaryOperator op, object operand, ExecutionContext context, LythonSourceSpan span)
    {
        var method = op switch
        {
            ExecutableUnaryOperator.Plus => "__pos__",
            ExecutableUnaryOperator.Minus => "__neg__",
            ExecutableUnaryOperator.BitwiseNot => "__invert__",
            _ => null,
        };
        if (method is not null && TryInvokeUnarySpecialMethod(operand, method, context, span, out var protocolResult))
        {
            return protocolResult;
        }

        return op switch
        {
            ExecutableUnaryOperator.Not => !IsTruthy(operand, context, span),
            ExecutableUnaryOperator.Plus => EvaluateUnaryPlus(operand, span),
            ExecutableUnaryOperator.Minus => EvaluateUnaryMinus(operand, span),
            ExecutableUnaryOperator.BitwiseNot => EvaluateBitwiseNot(operand, span),
            _ => throw new InvalidOperationException($"Executable IR contains unknown unary operator {op}."),
        };
    }

    private static bool TryEvaluateExecutableNumericProtocol(
        ExecutableBinaryOperator op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        out object result)
    {
        if (op == ExecutableBinaryOperator.Modulo && left is PyString)
        {
            result = PyNone.Instance;
            return false;
        }

        var methods = op switch
        {
            ExecutableBinaryOperator.Add => ("__add__", "__radd__"),
            ExecutableBinaryOperator.Subtract => ("__sub__", "__rsub__"),
            ExecutableBinaryOperator.Multiply => ("__mul__", "__rmul__"),
            ExecutableBinaryOperator.Divide => ("__truediv__", "__rtruediv__"),
            ExecutableBinaryOperator.FloorDivide => ("__floordiv__", "__rfloordiv__"),
            ExecutableBinaryOperator.Modulo => ("__mod__", "__rmod__"),
            ExecutableBinaryOperator.Power => ("__pow__", "__rpow__"),
            ExecutableBinaryOperator.BitwiseOr => ("__or__", "__ror__"),
            ExecutableBinaryOperator.BitwiseXor => ("__xor__", "__rxor__"),
            ExecutableBinaryOperator.BitwiseAnd => ("__and__", "__rand__"),
            ExecutableBinaryOperator.LeftShift => ("__lshift__", "__rlshift__"),
            ExecutableBinaryOperator.RightShift => ("__rshift__", "__rrshift__"),
            _ => (null, null),
        };
        if (methods.Item1 is not null &&
            (TryInvokeBinarySpecialMethod(left, methods.Item1, right, context, span, out result) ||
             TryInvokeBinarySpecialMethod(right, methods.Item2.RequireNotNull(), left, context, span, out result)))
        {
            return true;
        }

        result = PyNone.Instance;
        return false;
    }

    private static object EvaluateExecutableAugmented(ExecutableAugmentedOperator op, object currentValue, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAugmentedAssignment(
            currentValue,
            right,
            op switch
            {
                ExecutableAugmentedOperator.Add => AugmentedAssignmentOperatorSyntax.Add,
                ExecutableAugmentedOperator.Subtract => AugmentedAssignmentOperatorSyntax.Subtract,
                ExecutableAugmentedOperator.Multiply => AugmentedAssignmentOperatorSyntax.Multiply,
                ExecutableAugmentedOperator.Divide => AugmentedAssignmentOperatorSyntax.Divide,
                ExecutableAugmentedOperator.FloorDivide => AugmentedAssignmentOperatorSyntax.FloorDivide,
                ExecutableAugmentedOperator.Modulo => AugmentedAssignmentOperatorSyntax.Modulo,
                ExecutableAugmentedOperator.Power => AugmentedAssignmentOperatorSyntax.Power,
                ExecutableAugmentedOperator.BitwiseOr => AugmentedAssignmentOperatorSyntax.BitwiseOr,
                ExecutableAugmentedOperator.BitwiseXor => AugmentedAssignmentOperatorSyntax.BitwiseXor,
                ExecutableAugmentedOperator.BitwiseAnd => AugmentedAssignmentOperatorSyntax.BitwiseAnd,
                ExecutableAugmentedOperator.LeftShift => AugmentedAssignmentOperatorSyntax.LeftShift,
                ExecutableAugmentedOperator.RightShift => AugmentedAssignmentOperatorSyntax.RightShift,
                _ => throw new InvalidOperationException($"Executable IR contains unknown augmented operator {op}."),
            },
            context,
            span);
}
