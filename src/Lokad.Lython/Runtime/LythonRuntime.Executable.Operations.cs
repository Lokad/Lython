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

    private static bool TryReadExecutableMemberCache(
        object target,
        ExecutableMemberCache? cache,
        [MaybeNullWhen(false)] out object value)
    {
        // Identity is required: equal mutable Python values can expose different
        // instance members, while cacheable builtin targets have stable lookup rules.
        if (cache is not null && ReferenceEquals(cache.Target, target))
        {
            value = cache.Value;
            return true;
        }

        value = null;
        return false;
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
        var syntaxOperator = op switch
        {
            ExecutableBinaryOperator.Add => BinaryOperatorSyntax.Add,
            ExecutableBinaryOperator.Subtract => BinaryOperatorSyntax.Subtract,
            ExecutableBinaryOperator.Multiply => BinaryOperatorSyntax.Multiply,
            ExecutableBinaryOperator.Divide => BinaryOperatorSyntax.Divide,
            ExecutableBinaryOperator.FloorDivide => BinaryOperatorSyntax.FloorDivide,
            ExecutableBinaryOperator.Modulo => BinaryOperatorSyntax.Modulo,
            ExecutableBinaryOperator.Power => BinaryOperatorSyntax.Power,
            ExecutableBinaryOperator.BitwiseOr => BinaryOperatorSyntax.BitwiseOr,
            ExecutableBinaryOperator.BitwiseXor => BinaryOperatorSyntax.BitwiseXor,
            ExecutableBinaryOperator.BitwiseAnd => BinaryOperatorSyntax.BitwiseAnd,
            ExecutableBinaryOperator.LeftShift => BinaryOperatorSyntax.LeftShift,
            ExecutableBinaryOperator.RightShift => BinaryOperatorSyntax.RightShift,
            ExecutableBinaryOperator.Less => BinaryOperatorSyntax.Less,
            ExecutableBinaryOperator.LessEqual => BinaryOperatorSyntax.LessEqual,
            ExecutableBinaryOperator.Greater => BinaryOperatorSyntax.Greater,
            ExecutableBinaryOperator.GreaterEqual => BinaryOperatorSyntax.GreaterEqual,
            ExecutableBinaryOperator.Is => BinaryOperatorSyntax.Is,
            ExecutableBinaryOperator.IsNot => BinaryOperatorSyntax.IsNot,
            ExecutableBinaryOperator.In => BinaryOperatorSyntax.In,
            ExecutableBinaryOperator.NotIn => BinaryOperatorSyntax.NotIn,
            ExecutableBinaryOperator.Equal => BinaryOperatorSyntax.Equal,
            ExecutableBinaryOperator.NotEqual => BinaryOperatorSyntax.NotEqual,
            _ => throw new InvalidOperationException($"Executable IR contains unknown binary operator {op}."),
        };

        return EvaluateBinaryOperator(syntaxOperator, left, right, context, span);
    }

    private static object EvaluateExecutableUnary(ExecutableUnaryOperator op, object operand, ExecutionContext context, LythonSourceSpan span)
    {
        var syntaxOperator = op switch
        {
            ExecutableUnaryOperator.Not => UnaryOperatorSyntax.Not,
            ExecutableUnaryOperator.Plus => UnaryOperatorSyntax.Plus,
            ExecutableUnaryOperator.Minus => UnaryOperatorSyntax.Minus,
            ExecutableUnaryOperator.BitwiseNot => UnaryOperatorSyntax.BitwiseNot,
            _ => throw new InvalidOperationException($"Executable IR contains unknown unary operator {op}."),
        };

        return EvaluateUnaryOperator(syntaxOperator, operand, context, span);
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
