using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static async ValueTask<object> EvaluateLoweredExpressionAsync(LoweredExpression expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            var value = await DispatchLoweredExpressionAsync(
                    expression,
                    context,
                    AsynchronousLoweredStatementExecution.Instance)
                .ConfigureAwait(false);

            context.ObserveValue(value, expression.Span);
            return value;
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

    private static async ValueTask<PyList> CreateLoweredListLiteralAsync(LoweredListLiteralExpression list, ExecutionContext context)
    {
        if (list.List.HasUnpacking)
        {
            var expanded = new PyList([], context.MemoryGovernor, list.Span);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var value = RuntimeValue(await EvaluateLoweredExpressionAsync(list.Items[i].Expression, context).ConfigureAwait(false));
                if (!list.Items[i].IsUnpacking)
                {
                    AddListDisplayValue(expanded, value, list.Span, context);
                    continue;
                }

                await foreach (var item in ToSequenceAsync(value, list.Items[i].Span, context).ConfigureAwait(false))
                {
                    AddListDisplayValue(expanded, item, list.Span, context);
                }
            }

            return expanded;
        }

        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(await EvaluateLoweredExpressionAsync(list.Items[i].Expression, context).ConfigureAwait(false));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static async ValueTask<PyTuple> CreateLoweredTupleLiteralAsync(
        LoweredTupleLiteralExpression tuple,
        ExecutionContext context)
    {
        if (!tuple.Tuple.HasUnpacking)
        {
            return await CreateTupleAsync(
                    tuple.Items.Count,
                    async i => RuntimeValue(await EvaluateLoweredExpressionAsync(tuple.Items[i].Expression, context).ConfigureAwait(false)),
                    context,
                    tuple.Span)
                .ConfigureAwait(false);
        }

        var expanded = new List<object>();
        for (var i = 0; i < tuple.Items.Count; i++)
        {
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(tuple.Items[i].Expression, context).ConfigureAwait(false));
            if (!tuple.Items[i].IsUnpacking)
            {
                AddTupleDisplayValue(expanded, value, tuple.Span, context);
                continue;
            }

            await foreach (var item in ToSequenceAsync(value, tuple.Items[i].Span, context).ConfigureAwait(false))
            {
                AddTupleDisplayValue(expanded, item, tuple.Span, context);
            }
        }

        context.ObserveCollectionCount(expanded.Count, tuple.Span);
        return new PyTuple(expanded, context.MemoryGovernor, tuple.Span);
    }

    private static async ValueTask<object> EvaluateLoweredDictLiteralAsync(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is LoweredDictionaryUnpackingItem unpacking)
            {
                var mapping = RuntimeValue(await EvaluateLoweredExpressionAsync(unpacking.Mapping, context).ConfigureAwait(false));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Item.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Item.Span, context.MemoryGovernor), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (LoweredDictionaryKeyValueItem)item;
            var key = ValidateDictionaryKey(await EvaluateLoweredExpressionAsync(keyValue.Key, context).ConfigureAwait(false), keyValue.Key.Span, context.MemoryGovernor);
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(keyValue.Value, context).ConfigureAwait(false));
            result.SetItem(key, value);
            context.ObserveCollectionCount(result.Count, dict.Span);
        }

        return result;
    }

    private static async ValueTask<object> EvaluateLoweredSetLiteralAsync(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        var items = new PySet(context.MemoryGovernor, set.Span);
        for (var i = 0; i < set.Items.Count; i++)
        {
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(set.Items[i].Expression, context).ConfigureAwait(false));
            if (!set.Items[i].IsUnpacking)
            {
                AddSetLiteralValue(items, value, set.Items[i].Span, set.Span, context);
                continue;
            }

            await foreach (var item in ToSequenceAsync(value, set.Items[i].Span, context).ConfigureAwait(false))
            {
                AddSetLiteralValue(items, RuntimeValue(item), set.Items[i].Span, set.Span, context);
            }
        }

        return items;
    }

    private static async ValueTask<object> EvaluateLoweredFormattedStringAsync(LoweredFormattedStringExpression formatted, ExecutionContext context)
        => await EvaluateLoweredFormattedStringPartsCoreAsync(
                formatted.Parts,
                context,
                formatted.Span,
                expression => EvaluateLoweredExpressionAsync(expression, context))
            .ConfigureAwait(false);

    private static async ValueTask<object> EvaluateLoweredListComprehensionAsync(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                scope,
                async itemScope => result.Add(RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ItemExpression, itemScope).ConfigureAwait(false))))
            .ConfigureAwait(false);
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredSetComprehensionAsync(LoweredSetComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                scope,
                async itemScope =>
                {
                    var item = ValidateSetItem(
                        await EvaluateLoweredExpressionAsync(comprehension.ItemExpression, itemScope).ConfigureAwait(false),
                        comprehension.ItemExpression.Span,
                        itemScope.MemoryGovernor);
                    result.Add(item);
                })
            .ConfigureAwait(false);
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredDictComprehensionAsync(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        var scope = new ExecutionContext(context);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                scope,
                async itemScope =>
                {
                    var key = ValidateDictionaryKey(
                        await EvaluateLoweredExpressionAsync(comprehension.KeyExpression, itemScope).ConfigureAwait(false),
                        comprehension.KeyExpression.Span,
                        itemScope.MemoryGovernor);
                    result.SetItem(key, RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ValueExpression, itemScope).ConfigureAwait(false)));
                })
            .ConfigureAwait(false);
        PropagateComprehensionBindings(scope, context, comprehension.Clauses.Select(clause => clause.Target), comprehension.Span);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static async ValueTask EvaluateLoweredComprehensionClausesAsync(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Func<ExecutionContext, ValueTask> emit)
    {
        var clause = clauses[index];
        var iterable = await EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false);

        await foreach (var item in ToSequenceAsync(iterable, clause.Iterable.Span, context).ConfigureAwait(false))
        {
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, context);

            if (clause.Condition is not null &&
                !await IsTruthyAsync(
                        await EvaluateLoweredExpressionAsync(clause.Condition, context).ConfigureAwait(false),
                        context,
                        clause.Condition.Span)
                    .ConfigureAwait(false))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                await emit(context).ConfigureAwait(false);
            }
            else
            {
                await EvaluateLoweredComprehensionClausesAsync(clauses, index + 1, context, emit).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<object> EvaluateLoweredSubscriptAsync(LoweredSubscriptExpression subscript, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(subscript.Target, context).ConfigureAwait(false);
        var index = await EvaluateLoweredExpressionAsync(subscript.Index, context).ConfigureAwait(false);
        return ReadLoweredSubscript(target, index, subscript.Span, context);
    }

    private static async ValueTask<object> EvaluateLoweredSliceAsync(LoweredSliceExpression slice, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(slice.Target, context).ConfigureAwait(false);
        var start = slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false);
        var end = slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false);
        var step = slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false);
        return PyIndexing.ReadSlice(target, start, end, step, slice.Span, context);
    }

    private static async ValueTask<object> ResolveLoweredMemberAsync(LoweredMemberExpression member, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(member.Target, context).ConfigureAwait(false);
        return ResolveLoweredMemberValue(member, target, context);
    }

    private static async ValueTask<object> EvaluateLoweredBinaryAsync(LoweredBinaryExpression binary, ExecutionContext context)
    {
        if (binary.Binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
            return await IsTruthyAsync(leftValue, context, binary.Left.Span).ConfigureAwait(false)
                ? leftValue
                : await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);
        }

        if (binary.Binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
            return !await IsTruthyAsync(leftValue, context, binary.Left.Span).ConfigureAwait(false)
                ? leftValue
                : await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);
        }

        var left = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
        var right = await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);
        return await EvaluateBinaryOperatorAsync(binary.Binary.Operator, left, right, context, binary.Span).ConfigureAwait(false);
    }

    private static async ValueTask<bool> EvaluateLoweredChainedComparisonAsync(LoweredChainedComparisonExpression chained, ExecutionContext context)
    {
        var left = await EvaluateLoweredExpressionAsync(chained.Operands[0], context).ConfigureAwait(false);
        for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
        {
            var right = await EvaluateLoweredExpressionAsync(chained.Operands[i + 1], context).ConfigureAwait(false);
            if (!await EvaluateComparisonOperatorAsync(left, right, chained.ChainedComparison.Operators[i], context, chained.Span).ConfigureAwait(false))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static async ValueTask<object> EvaluateLoweredUnaryAsync(LoweredUnaryExpression unary, ExecutionContext context)
    {
        var operand = await EvaluateLoweredExpressionAsync(unary.Operand, context).ConfigureAwait(false);
        return await EvaluateUnaryOperatorAsync(unary.Unary.Operator, operand, context, unary.Span).ConfigureAwait(false);
    }

    private static async ValueTask ExecuteLoweredAssertStatementAsync(LoweredAssertStatement statement, ExecutionContext context)
    {
        if (await IsTruthyAsync(
                await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false),
                context,
                statement.Condition.Span)
            .ConfigureAwait(false))
        {
            return;
        }

        ThrowAssertionError(
            statement.Message is null
                ? null
                : await EvaluateLoweredExpressionAsync(statement.Message, context).ConfigureAwait(false),
            context,
            statement.Span);
    }

    private static async ValueTask ExecuteLoweredDeleteStatementAsync(LoweredDeleteStatement statement, ExecutionContext context)
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

                var target = await EvaluateLoweredExpressionAsync(subscript.Target, context).ConfigureAwait(false);
                var index = await EvaluateLoweredExpressionAsync(subscript.Index, context).ConfigureAwait(false);
                ExecuteResolvedSubscriptDeletion(target, index, statement.Span, context);
                return;

            case SliceExpressionSyntax:
                if (statement.Target is not LoweredSliceExpression slice)
                {
                    break;
                }

                ExecuteSliceDeletion(
                    await EvaluateLoweredExpressionAsync(slice.Target, context).ConfigureAwait(false),
                    slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false),
                    slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false),
                    slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false),
                    statement.Span);
                return;

            case MemberExpressionSyntax memberSyntax:
                if (statement.Target is not LoweredMemberExpression member)
                {
                    break;
                }

                var memberTarget = await EvaluateLoweredExpressionAsync(member.Target, context).ConfigureAwait(false);
                if (!PyMemberAccess.TryDelete(memberTarget, memberSyntax.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;
        }

        throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
    }

    private static async ValueTask ExecuteLoweredRaiseStatementAsync(LoweredRaiseStatement statement, ExecutionContext context)
    {
        if (statement.Expression is null)
        {
            ThrowReraisedException(statement.Span, context);
            return;
        }

        var raised = await EvaluateLoweredExpressionAsync(statement.Expression, context).ConfigureAwait(false);
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
            thrown.PythonCause = CoerceRaiseCause(await EvaluateLoweredExpressionAsync(statement.CauseExpression, context).ConfigureAwait(false), statement.Span, context);
            thrown.SuppressPythonContext = true;
        }

        throw thrown;
    }

    private static async ValueTask<object> EvaluateLoweredAssignmentExpressionAsync(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = await EvaluateLoweredExpressionAsync(assignment.Expression, context).ConfigureAwait(false);
        return StoreLoweredAssignmentResult(assignment, value, context);
    }

    private static async ValueTask<object> CreateLoweredLambdaAsync(LoweredLambdaExpression lambda, ExecutionContext context)
    {
        var loweredParameters = LowerLambdaParameters(lambda);
        var function = new LambdaFunction(
            loweredParameters,
            lambda.Body,
            context,
            await BuildDefaultArgumentMapAsync(loweredParameters, expression => EvaluateLoweredExpressionAsync(expression, context)).ConfigureAwait(false));
        ChargeFunctionValue(context, lambda.Span);
        ChargeClosureRetention(context, context.Variables.Count, context.MemoryGovernor, lambda.Span);
        return function;
    }

    private static async ValueTask<object> InvokeLoweredCallAsync(LoweredCallExpression call, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(call.Target, context).ConfigureAwait(false);
        return await InvokeCallableTargetAsync(
                target,
                call.Call.Target.Span,
                call.Span,
                context,
                () => CallExpansion.ExpandLoweredArgumentsAsync(call.Arguments, context, EvaluateLoweredExpressionAsync))
            .ConfigureAwait(false);
    }

    private static async ValueTask InvokeInitSubclassAsync(PyType type, CallArgumentValue[] keywordArguments, LythonSourceSpan span, ExecutionContext context)
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

        _ = await callable.InvokeAsync(keywordArguments, span, context).ConfigureAwait(false);
    }
}
