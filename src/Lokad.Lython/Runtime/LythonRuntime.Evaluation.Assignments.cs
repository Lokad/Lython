using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void ExecuteAssertStatement(AssertStatementSyntax statement, ExecutionContext context)
    {
        if (IsTruthy(EvaluateExpression(statement.Condition, context), context, statement.Condition.Span))
        {
            return;
        }

        var message = statement.Message is null
            ? string.Empty
            : ToInterpolatedPyString(EvaluateExpression(statement.Message, context), context).AsString();
        throw new LythonRuntimeException("AssertionError", message, statement.Span);
    }

    private static void ExecuteDeleteStatement(DeleteStatementSyntax statement, ExecutionContext context)
    {
        switch (statement.Target)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, statement.Span))
                {
                    throw new LythonRuntimeException("NameError", $"Name '{identifier.Name}' is not defined.", statement.Span);
                }

                return;

            case SubscriptExpressionSyntax subscript:
                var target = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
                switch (target)
                {
                    case IDeletablePySubscriptableValue subscriptable:
                        subscriptable.DeleteSubscript(index, statement.Span);
                        return;

                    case IMutablePySequenceValue sequence:
                        sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, statement.Span));
                        return;

                    case PyDict dict:
                        if (!dict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;

                    case PyDefaultDict defaultDict:
                        if (!defaultDict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;

                    case PyCounter counter:
                        _ = counter.Remove(ValidateDictionaryKey(index, statement.Span));
                        return;

                    case PyInstance instance:
                        InvokeItemMutation(instance, "__delitem__", [CallArgumentValue.Positional(index)], context, statement.Span);
                        return;

                    case PyTuple:
                        throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", statement.Span);

                    case PyString:
                        throw new LythonRuntimeException("TypeError", "String does not support item deletion.", statement.Span);

                    default:
                        throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", statement.Span);
                }

            case SliceExpressionSyntax slice:
                ExecuteSliceDeletion(
                    EvaluateExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateExpression(slice.Step, context),
                    statement.Span);
                return;

            case MemberExpressionSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, member.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;

            default:
                throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
        }
    }

    private static bool EvaluateChainedComparison(ChainedComparisonExpressionSyntax chainedComparison, ExecutionContext context)
    {
        var left = EvaluateExpression(chainedComparison.Operands[0], context);
        for (var i = 0; i < chainedComparison.Operators.Count; i++)
        {
            var right = EvaluateExpression(chainedComparison.Operands[i + 1], context);
            if (!EvaluateComparisonOperator(left, right, chainedComparison.Operators[i], context, chainedComparison.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static bool EvaluateComparisonOperator(object left, object right, BinaryOperatorSyntax op, ExecutionContext context, LythonSourceSpan span)
    {
        return (bool)EvaluateBinaryOperator(op, left, right, context, span);
    }

    private static async ValueTask<bool> EvaluateComparisonOperatorAsync(
        object left,
        object right,
        BinaryOperatorSyntax op,
        ExecutionContext context,
        LythonSourceSpan span)
        => (bool)await EvaluateBinaryOperatorAsync(op, left, right, context, span).ConfigureAwait(false);

    private static object CreateLambda(LambdaExpressionSyntax lambda, ExecutionContext context)
    {
        var loweredParameters = lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();
        return new LambdaFunction(
            loweredParameters,
            LoweredScript.LowerStandaloneExpression(lambda.Body),
            context,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
    }

    private static void ExecuteAugmentedAssignment(AugmentedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = ResolveAugmentedAssignmentTarget(statement.Target, context);
        var right = EvaluateExpression(statement.Expression, context);
        var updated = EvaluateAugmentedAssignment(target.CurrentValue, right, statement.Operator, context, statement.Span);
        target.Store(updated);
    }

    private sealed record AugmentedAssignmentTargetReference(object CurrentValue, Action<object> Store);

    private static void ExecuteAnnotatedAssignment(AnnotatedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        if (statement.Expression is not null)
        {
            StoreName(statement.Name, EvaluateExpression(statement.Expression, context), context, statement.Span);
        }
    }

    private static void ExecuteChainedAssignment(ChainedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var value = EvaluateExpression(statement.Expression, context);
        foreach (var target in statement.Targets)
        {
            AssignTarget(target, value, context);
        }
    }

    private static void ExecuteSubscriptAssignment(SubscriptAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var index = EvaluateExpression(statement.Index, context);
        var value = EvaluateExpression(statement.Expression, context);
        SetSubscriptValue(target, index, value, statement.Span, context);
    }

    private static void InvokeItemMutation(
        PyInstance instance,
        string methodName,
        CallArgumentValue[] arguments,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute(methodName, context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
        }

        _ = callable.Invoke(arguments, span, context);
    }

    private static void ExecuteSliceAssignment(SliceAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var start = statement.Start is null ? null : EvaluateExpression(statement.Start, context);
        var end = statement.End is null ? null : EvaluateExpression(statement.End, context);
        var step = statement.Step is null ? null : EvaluateExpression(statement.Step, context);
        var value = EvaluateExpression(statement.Expression, context);
        ExecuteSliceAssignment(target, start, end, step, value, statement.Span, context);
    }

    private static void ExecuteMemberAssignment(MemberAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var value = EvaluateExpression(statement.Expression, context);
        if (!PyMemberAccess.TryAssign(target, statement.MemberName, value, context, statement.Span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", statement.Span);
        }
    }

    private static void AssignTarget(AssignmentTargetSyntax target, object value, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                StoreName(name.Name, value, context, name.Span);
                return;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                AssignTargets(unpacking.Targets, value, unpacking.Span, context);
                return;
            case SubscriptAssignmentTargetSyntax subscript:
                AssignSubscriptTarget(subscript, value, context);
                return;
            case SliceAssignmentTargetSyntax slice:
                AssignSliceTarget(slice, value, context);
                return;
            case MemberAssignmentTargetSyntax member:
                AssignMemberTarget(member, value, context);
                return;
            default:
                throw new InvalidOperationException($"Unsupported assignment target syntax: {target.GetType().Name}");
        }
    }

    private static void AssignSubscriptTarget(SubscriptAssignmentTargetSyntax subscript, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(subscript.Target, context);
        var index = EvaluateExpression(subscript.Index, context);
        SetSubscriptValue(target, index, value, subscript.Span, context);
    }

    private static void SetSubscriptValue(object target, object index, object value, LythonSourceSpan span, ExecutionContext context)
    {
        switch (target)
        {
            case IMutablePySubscriptableValue subscriptable:
                subscriptable.SetSubscript(index, value, span);
                return;

            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, span), value);
                return;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                dict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(dict.Count, span);
                return;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                defaultDict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(defaultDict.Count, span);
                return;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, span);
                counter.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(counter.Count, span);
                return;
            case PyInstance instance:
                InvokeItemMutation(
                    instance,
                    "__setitem__",
                    [CallArgumentValue.Positional(index), CallArgumentValue.Positional(value)],
                    context,
                    span);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", span);
            case PyString or string:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", span);
        }
    }

    private static void AssignSliceTarget(SliceAssignmentTargetSyntax slice, object value, ExecutionContext context)
    {
        ExecuteSliceAssignment(
            EvaluateExpression(slice.Target, context),
            slice.Start is null ? null : EvaluateExpression(slice.Start, context),
            slice.End is null ? null : EvaluateExpression(slice.End, context),
            slice.Step is null ? null : EvaluateExpression(slice.Step, context),
            value,
            slice.Span,
            context);
    }

    private static void AssignMemberTarget(MemberAssignmentTargetSyntax member, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(member.Target, context);
        SetMemberValue(target, member.MemberName, value, member.Span, context);
    }

    private static void SetMemberValue(object target, string memberName, object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (!PyMemberAccess.TryAssign(target, memberName, value, context, span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", span);
        }
    }

    private static AugmentedAssignmentTargetReference ResolveAugmentedAssignmentTarget(AssignmentTargetSyntax target, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                var currentValue = ResolveName(name.Name, name.Span, context);

                return new AugmentedAssignmentTargetReference(
                    currentValue,
                    value => StoreName(name.Name, value, context, name.Span));

            case SubscriptAssignmentTargetSyntax subscript:
                var subscriptTarget = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = EvaluateExpression(slice.Target, context);
                var start = slice.Start is null ? null : EvaluateExpression(slice.Start, context);
                var end = slice.End is null ? null : EvaluateExpression(slice.End, context);
                var step = slice.Step is null ? null : EvaluateExpression(slice.Step, context);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case MemberAssignmentTargetSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!TryResolveRuntimeMember(memberTarget, member.MemberName, context, member.Span, out var memberValue))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, member.MemberName, member.Span);
                }

                return new AugmentedAssignmentTargetReference(
                    memberValue,
                    value => SetMemberValue(memberTarget, member.MemberName, value, member.Span, context));

            default:
                throw new LythonRuntimeException("TypeError", "Unsupported augmented assignment target.", target.Span);
        }
    }

    private static object ReadSubscriptValue(object target, object index, LythonSourceSpan span, ExecutionContext context)
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

    private static void ExecuteSliceAssignment(
        object target,
        object? start,
        object? end,
        object? step,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (target is PyList list)
        {
            list.AttachMemoryGovernor(context.MemoryGovernor, span);
            var values = ToSequence(value, span, context).ToArray();
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.SetSlice(bounds, values, span);
            context.ObserveCollectionCount(list.Count, span);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice assignment.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice assignment.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice assignment.", span);
    }

    private static void ExecuteSliceDeletion(
        object target,
        object? start,
        object? end,
        object? step,
        LythonSourceSpan span)
    {
        if (target is PyList list)
        {
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.DeleteSlice(bounds);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice deletion.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice deletion.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice deletion.", span);
    }

    private static void ExecuteUnpackingAssignment(UnpackingAssignmentStatementSyntax statement, ExecutionContext context)
    {
        AssignTargets(statement.Targets, EvaluateExpression(statement.Expression, context), statement.Expression.Span, context);
    }

    private static void ExecuteWithStatement(
        WithStatementSyntax statement,
        LoweredExpression? loweredContextExpression,
        IReadOnlyList<LoweredStatement>? loweredBody,
        ExecutionContext context)
    {
        _ = PyContextManagers.ExecuteWith(
            loweredContextExpression is null
                ? EvaluateExpression(statement.ContextExpression, context)
                : EvaluateLoweredExpression(loweredContextExpression, context),
            statement.VariableName,
            loweredBody ?? LoweredScript.Lower(new ScriptSyntax(statement.Body)).Statements,
            statement.Span,
            statement.ContextExpression.Span,
            context);
    }

    private static async ValueTask ExecuteWithStatementAsync(
        WithStatementSyntax statement,
        LoweredExpression loweredContextExpression,
        IReadOnlyList<LoweredStatement> loweredBody,
        ExecutionContext context)
    {
        _ = await PyContextManagers.ExecuteWithAsync(
                await EvaluateLoweredExpressionAsync(loweredContextExpression, context).ConfigureAwait(false),
                statement.VariableName,
                loweredBody,
                statement.Span,
                statement.ContextExpression.Span,
                context)
            .ConfigureAwait(false);
    }

    private static object EvaluateAugmentedAssignment(
        object currentValue,
        object right,
        AugmentedAssignmentOperatorSyntax op,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var inPlaceMethod = op switch
        {
            AugmentedAssignmentOperatorSyntax.Add => "__iadd__",
            AugmentedAssignmentOperatorSyntax.Subtract => "__isub__",
            AugmentedAssignmentOperatorSyntax.Multiply => "__imul__",
            AugmentedAssignmentOperatorSyntax.Divide => "__itruediv__",
            AugmentedAssignmentOperatorSyntax.FloorDivide => "__ifloordiv__",
            AugmentedAssignmentOperatorSyntax.Modulo => "__imod__",
            AugmentedAssignmentOperatorSyntax.Power => "__ipow__",
            AugmentedAssignmentOperatorSyntax.BitwiseOr => "__ior__",
            AugmentedAssignmentOperatorSyntax.BitwiseXor => "__ixor__",
            AugmentedAssignmentOperatorSyntax.BitwiseAnd => "__iand__",
            AugmentedAssignmentOperatorSyntax.LeftShift => "__ilshift__",
            AugmentedAssignmentOperatorSyntax.RightShift => "__irshift__",
            _ => null,
        };
        if (inPlaceMethod is not null &&
            TryInvokeBinarySpecialMethod(currentValue, inPlaceMethod, right, context, span, out var inPlaceResult))
        {
            return inPlaceResult;
        }

        if (op == AugmentedAssignmentOperatorSyntax.Add &&
            currentValue is PyList currentList)
        {
            currentList.AddRange(ToSequence(right, span, context));
            return currentList;
        }

        if (op == AugmentedAssignmentOperatorSyntax.Multiply &&
            currentValue is PyList multipliedList &&
            right is BigInteger repeatCount)
        {
            multipliedList.RepeatInPlace(ToListRepeatCount(repeatCount, span), span);
            context.ObserveCollectionCount(multipliedList.Count, span);
            return multipliedList;
        }

        if (currentValue is PySet currentSet && right is PySet rightSet)
        {
            switch (op)
            {
                case AugmentedAssignmentOperatorSyntax.BitwiseOr:
                    currentSet.UnionWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseAnd:
                    currentSet.IntersectWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseXor:
                    currentSet.SymmetricExceptWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.Subtract:
                    currentSet.ExceptWith(rightSet);
                    return currentSet;
            }
        }

        return op switch
        {
            AugmentedAssignmentOperatorSyntax.Add => EvaluateAdd(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Subtract => EvaluateSubtract(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Multiply => EvaluateMultiply(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Divide => EvaluateDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.FloorDivide => EvaluateFloorDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Modulo => EvaluateModulo(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Power => EvaluatePower(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.LeftShift => EvaluateLeftShift(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.RightShift => EvaluateRightShift(currentValue, right, span),
            _ => throw new InvalidOperationException($"Unsupported augmented assignment operator: {op}")
        };
    }
}
