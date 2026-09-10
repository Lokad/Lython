using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static object EvaluateLoweredExpression(LoweredExpression expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            return DispatchLoweredExpressionAsync(expression, context, SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static PyList CreateLoweredListLiteral(LoweredListLiteralExpression list, ExecutionContext context)
    {
        if (list.List.HasUnpacking)
        {
            var expanded = new PyList([], context.MemoryGovernor, list.Span);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateLoweredExpression(list.Items[i].Expression, context));
                AppendListDisplayItem(expanded, value, list.Items[i].IsUnpacking, list.Items[i].Span, list.Span, context);
            }

            return expanded;
        }

        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(EvaluateLoweredExpression(list.Items[i].Expression, context));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static PyTuple CreateLoweredTupleLiteral(LoweredTupleLiteralExpression tuple, ExecutionContext context)
    {
        if (!tuple.Tuple.HasUnpacking)
        {
            return CreateTuple(
                tuple.Items.Count,
                i => RuntimeValue(EvaluateLoweredExpression(tuple.Items[i].Expression, context)),
                context,
                tuple.Span);
        }

        var expanded = new List<object>();
        for (var i = 0; i < tuple.Items.Count; i++)
        {
            var value = RuntimeValue(EvaluateLoweredExpression(tuple.Items[i].Expression, context));
            AppendTupleDisplayItem(expanded, value, tuple.Items[i].IsUnpacking, tuple.Items[i].Span, tuple.Span, context);
        }

        context.ObserveCollectionCount(expanded.Count, tuple.Span);
        return new PyTuple(expanded, context.MemoryGovernor, tuple.Span);
    }

    private static object EvaluateLoweredDictLiteral(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is LoweredDictionaryUnpackingItem unpacking)
            {
                var mapping = RuntimeValue(EvaluateLoweredExpression(unpacking.Mapping, context));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Item.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Item.Span, context.MemoryGovernor), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (LoweredDictionaryKeyValueItem)item;
            var key = ValidateDictionaryKey(EvaluateLoweredExpression(keyValue.Key, context), keyValue.Key.Span, context.MemoryGovernor);
            var value = RuntimeValue(EvaluateLoweredExpression(keyValue.Value, context));
            result.SetItem(key, value);
            context.ObserveCollectionCount(result.Count, dict.Span);
        }

        return result;
    }

    private static object EvaluateLoweredSetLiteral(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        var items = new PySet(context.MemoryGovernor, set.Span);
        for (var i = 0; i < set.Items.Count; i++)
        {
            var value = RuntimeValue(EvaluateLoweredExpression(set.Items[i].Expression, context));
            AddSetLiteralItem(items, value, set.Items[i].IsUnpacking, set.Items[i].Span, set.Span, context);
        }

        return items;
    }

    private static object EvaluateLoweredFormattedString(LoweredFormattedStringExpression formatted, ExecutionContext context)
        => EvaluateLoweredFormattedStringPartsCoreAsync(
                formatted.Parts,
                context,
                formatted.Span,
                expression => ValueTask.FromResult(EvaluateLoweredExpression(expression, context)))
            .GetAwaiter()
            .GetResult();

    private static object EvaluateLoweredListComprehension(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            scope,
            itemScope => result.Add(RuntimeValue(EvaluateLoweredExpression(comprehension.ItemExpression, itemScope))));
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateLoweredSetComprehension(LoweredSetComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            scope,
            itemScope =>
            {
                var item = ValidateSetItem(
                    EvaluateLoweredExpression(comprehension.ItemExpression, itemScope),
                    comprehension.ItemExpression.Span,
                    itemScope.MemoryGovernor);
                result.Add(item);
            });
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateLoweredDictComprehension(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            scope,
            itemScope =>
            {
                var key = ValidateDictionaryKey(EvaluateLoweredExpression(comprehension.KeyExpression, itemScope), comprehension.KeyExpression.Span, itemScope.MemoryGovernor);
                result.SetItem(key, RuntimeValue(EvaluateLoweredExpression(comprehension.ValueExpression, itemScope)));
            });
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static void EvaluateLoweredComprehensionClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Action<ExecutionContext> emit)
    {
        var clause = clauses[index];
        var iterable = EvaluateLoweredExpression(clause.Iterable, context);

        foreach (var item in ToSequence(iterable, clause.Iterable.Span, context))
        {
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, context);

            if (clause.Condition is not null && !IsTruthy(EvaluateLoweredExpression(clause.Condition, context), context, clause.Condition.Span))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                emit(context);
            }
            else
            {
                EvaluateLoweredComprehensionClauses(clauses, index + 1, context, emit);
            }
        }
    }

    private static object EvaluateLoweredSubscript(LoweredSubscriptExpression subscript, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(subscript.Target, context);
        var index = EvaluateLoweredExpression(subscript.Index, context);
        return ReadLoweredSubscript(target, index, subscript.Span, context);
    }

    private static object EvaluateLoweredSlice(LoweredSliceExpression slice, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(slice.Target, context);
        var start = slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context);
        var end = slice.End is null ? null : EvaluateLoweredExpression(slice.End, context);
        var step = slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context);
        return PyIndexing.ReadSlice(target, start, end, step, slice.Span, context);
    }

    private static object ResolveLoweredMember(LoweredMemberExpression member, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(member.Target, context);
        return ResolveLoweredMemberValue(member, target, context);
    }

    private static object EvaluateLoweredBinary(LoweredBinaryExpression binary, ExecutionContext context)
    {
        if (binary.Binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateLoweredExpression(binary.Left, context);
            return IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue
                : EvaluateLoweredExpression(binary.Right, context);
        }

        if (binary.Binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateLoweredExpression(binary.Left, context);
            return !IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue
                : EvaluateLoweredExpression(binary.Right, context);
        }

        var left = EvaluateLoweredExpression(binary.Left, context);
        var right = EvaluateLoweredExpression(binary.Right, context);

        return EvaluateLoweredBinaryOperator(binary, left, right, context);
    }

    private static bool EvaluateLoweredChainedComparison(LoweredChainedComparisonExpression chained, ExecutionContext context)
    {
        var left = EvaluateLoweredExpression(chained.Operands[0], context);
        for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
        {
            var right = EvaluateLoweredExpression(chained.Operands[i + 1], context);
            if (!EvaluateComparisonOperator(left, right, chained.ChainedComparison.Operators[i], context, chained.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static object EvaluateLoweredUnary(LoweredUnaryExpression unary, ExecutionContext context)
    {
        var operand = EvaluateLoweredExpression(unary.Operand, context);
        return EvaluateLoweredUnaryOperator(unary, operand, context);
    }

    private static void ExecuteLoweredAssertStatement(LoweredAssertStatement statement, ExecutionContext context)
    {
        if (IsTruthy(EvaluateLoweredExpression(statement.Condition, context), context, statement.Condition.Span))
        {
            return;
        }

        ThrowAssertionError(
            statement.Message is null ? null : EvaluateLoweredExpression(statement.Message, context),
            context,
            statement.Span);
    }

    private static void ExecuteLoweredDeleteStatement(LoweredDeleteStatement statement, ExecutionContext context)
    {
        switch (statement.Target.Syntax)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, statement.Span))
                {
                    throw new LythonRuntimeException("NameError", $"Name '{identifier.Name}' is not defined.", statement.Span);
                }

                return;

            case SubscriptExpressionSyntax:
                if (statement.Target is not LoweredSubscriptExpression subscript)
                {
                    break;
                }

                var target = EvaluateLoweredExpression(subscript.Target, context);
                var index = EvaluateLoweredExpression(subscript.Index, context);
                ExecuteResolvedSubscriptDeletion(target, index, statement.Span, context);
                return;

            case SliceExpressionSyntax:
                if (statement.Target is not LoweredSliceExpression slice)
                {
                    break;
                }

                ExecuteSliceDeletion(
                    EvaluateLoweredExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateLoweredExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context),
                    statement.Span);
                return;

            case MemberExpressionSyntax memberSyntax:
                if (statement.Target is not LoweredMemberExpression member)
                {
                    break;
                }

                var memberTarget = EvaluateLoweredExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, memberSyntax.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;
        }

        throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
    }

    private static void ExecuteResolvedSubscriptDeletion(
        object target,
        object index,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        switch (target)
        {
            case IDeletablePySubscriptableValue subscriptable:
                subscriptable.DeleteSubscript(index, span);
                return;
            case IMutablePySequenceValue sequence:
                sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, span));
                return;
            case PyDict dict:
                if (!dict.Remove(ValidateDictionaryKey(index, span)))
                {
                    throw new LythonRuntimeException("KeyError", "Key was not found.", span);
                }

                return;
            case PyDefaultDict defaultDict:
                if (!defaultDict.Remove(ValidateDictionaryKey(index, span)))
                {
                    throw new LythonRuntimeException("KeyError", "Key was not found.", span);
                }

                return;
            case PyCounter counter:
                _ = counter.Remove(ValidateDictionaryKey(index, span));
                return;
            case PyInstance instance:
                InvokeItemMutation(instance, "__delitem__", [CallArgumentValue.Positional(index)], context, span);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", span);
            case PyString:
                throw new LythonRuntimeException("TypeError", "String does not support item deletion.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", span);
        }
    }

    private static void ExecuteLoweredRaiseStatement(LoweredRaiseStatement statement, ExecutionContext context)
    {
        if (statement.Expression is null)
        {
            ThrowReraisedException(statement.Span, context);
            return;
        }

        var raised = EvaluateLoweredExpression(statement.Expression, context);

        if (raised is ExceptionTypeValue typeValue &&
            typeValue.Invoke([], statement.Span, context) is PyException constructed)
        {
            raised = constructed;
        }

        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(statement.Span);
        }

        var thrown = new LythonRuntimeException(instance.Identity, instance.Message, statement.Span, null, instance.Value);
        AttachImplicitRaiseChain(thrown, instance, context);
        if (statement.CauseExpression is not null)
        {
            thrown.PythonCause = CoerceRaiseCause(EvaluateLoweredExpression(statement.CauseExpression, context), statement.Span, context);
            thrown.SuppressPythonContext = true;
        }

        throw thrown;
    }

    private static object CreateLoweredLambda(LoweredLambdaExpression lambda, ExecutionContext context)
    {
        var loweredParameters = LowerLambdaParameters(lambda);
        var function = new LambdaFunction(
            loweredParameters,
            lambda.Body,
            context,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
        ChargeFunctionValue(context, lambda.Span);
        ChargeClosureRetention(context, context.Variables.Count, context.MemoryGovernor, lambda.Span);
        return function;
    }

    private static object EvaluateLoweredAssignmentExpression(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = EvaluateLoweredExpression(assignment.Expression, context);
        return StoreLoweredAssignmentResult(assignment, value, context);
    }

    private static object InvokeLoweredCall(LoweredCallExpression call, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(call.Target, context);
        return InvokeCallableTarget(
            target,
            call.Call.Target.Span,
            call.Span,
            context,
            () => CallExpansion.ExpandLoweredArguments(call.Arguments, context, EvaluateLoweredExpression));
    }

    private static void InvokeInitSubclass(PyType type, CallArgumentValue[] keywordArguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!type.TryLookupInMro("__init_subclass__", 1, out var rawMethod, out _))
        {
            return;
        }

        object candidate = rawMethod switch
        {
            IPyBindableCallable bindable => bindable.Bind(type),
            IPyDescriptor descriptor => descriptor.Get(type, type, context, span),
            _ => rawMethod
        };

        if (candidate is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "__init_subclass__ must be callable.", span);
        }

        _ = callable.Invoke(keywordArguments, span, context);
    }
}
