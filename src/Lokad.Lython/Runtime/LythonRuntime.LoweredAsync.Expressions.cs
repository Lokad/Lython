using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Per-expression evaluation intentionally does not track interpreter depth:
    // depth is tracked once per Python nesting level (calls, bodies,
    // compound statements), so deep-but-shallow expression trees cannot
    // exhaust the counter budget while genuine nesting still trips it.
    // Cancellation checks remain on every node.
    internal static async ValueTask<object> EvaluateLoweredExpressionAsync(LoweredExpression expression, ExecutionContext context)
    {
        context.CheckExecution(expression.Span);
        try
        {
            ExecutionState.EnsureInterpreterStack(expression.Span);
            var value = await DispatchLoweredExpressionAsync(
                    expression,
                    context,
                    AsynchronousLoweredExpressionExecution.Instance)
                .ConfigureAwait(false);
            return value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
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

                await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(value, list.Items[i].Span, context).ConfigureAwait(false))
                {
                    AddListDisplayValue(expanded, item, list.Span, context);
                }
            }

            context.CheckExecution(list.Span);
            return expanded;
        }

        using var preflight2286 = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(await EvaluateLoweredExpressionAsync(list.Items[i].Expression, context).ConfigureAwait(false));
        }

        var result = new PyList(items, context.MemoryGovernor, list.Span);
        context.CheckExecution(list.Span);
        return result;
    }

    private static async ValueTask<PyTuple> CreateLoweredTupleLiteralAsync(
        LoweredTupleLiteralExpression tuple,
        ExecutionContext context)
    {
        if (!tuple.Tuple.HasUnpacking)
        {
            var result = await CreateTupleAsync(
                    tuple.Items.Count,
                    async i => RuntimeValue(await EvaluateLoweredExpressionAsync(tuple.Items[i].Expression, context).ConfigureAwait(false)),
                    context,
                    tuple.Span)
                .ConfigureAwait(false);
            context.CheckExecution(tuple.Span);
            return result;
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(32, tuple.Span);
        var expanded = new List<object>();
        for (var i = 0; i < tuple.Items.Count; i++)
        {
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(tuple.Items[i].Expression, context).ConfigureAwait(false));
            if (!tuple.Items[i].IsUnpacking)
            {
                AddTupleDisplayValue(expanded, storage, value, tuple.Span, context);
                continue;
            }

            await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(value, tuple.Items[i].Span, context).ConfigureAwait(false))
            {
                AddTupleDisplayValue(expanded, storage, item, tuple.Span, context);
            }
        }

        var unpacked = FinishTupleDisplay(expanded, storage, tuple.Span, context);
        context.CheckExecution(tuple.Span);
        return unpacked;
    }

    private static async ValueTask<object> EvaluateLoweredDictLiteralAsync(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is LoweredDictionaryUnpackingItem unpacking)
            {
                var mapping = RuntimeValue(await EvaluateLoweredExpressionAsync(unpacking.Mapping, context).ConfigureAwait(false));
                using var ambient = PyStructuralGuard.PushAmbient(context, dict.Span);
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Item.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Item.Span), RuntimeValue(pair.Value));
                    context.CheckExecution(dict.Span);
                }

                continue;
            }

            var keyValue = (LoweredDictionaryKeyValueItem)item;
            var key = RuntimeValue(await EvaluateLoweredExpressionAsync(keyValue.Key, context).ConfigureAwait(false));
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(keyValue.Value, context).ConfigureAwait(false));
            using (PyStructuralGuard.PushAmbient(context, dict.Span)) result.SetItem(ValidateDictionaryKey(key, keyValue.Key.Span), value);
            context.CheckExecution(dict.Span);
        }
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredSetLiteralAsync(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        var items = new PySet(context.MemoryGovernor, set.Span);
        try
        {
            for (var i = 0; i < set.Items.Count; i++)
            {
                var value = RuntimeValue(await EvaluateLoweredExpressionAsync(set.Items[i].Expression, context).ConfigureAwait(false));
                if (!set.Items[i].IsUnpacking)
                {
                    using var ambient = PyStructuralGuard.PushAmbient(context, set.Span);
                    AddSetLiteralValue(items, value, set.Items[i].Span, set.Span, context);
                    continue;
                }

                await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(value, set.Items[i].Span, context).ConfigureAwait(false))
                {
                    using var ambient = PyStructuralGuard.PushAmbient(context, set.Span);
                    AddSetLiteralValue(items, RuntimeValue(item), set.Items[i].Span, set.Span, context);
                }
            }
        }
        catch
        {
            items.ReleaseUnpublishedStorage();
            throw;
        }
        return items;
    }

    private static async ValueTask<object> EvaluateLoweredFormattedStringAsync(LoweredFormattedStringExpression formatted, ExecutionContext context)
        => await EvaluateLoweredFormattedStringPartsCoreAsync(
                formatted.Parts,
                context,
                formatted.Span,
                expression => EvaluateLoweredExpressionAsync(expression, context),
                asynchronous: true)
            .ConfigureAwait(false);

    private static async ValueTask<object> EvaluateLoweredListComprehensionAsync(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        await using var outer = await PyIteration.Cursor.CreateAsync(await EvaluateLoweredExpressionAsync(comprehension.Clauses[0].Iterable, context).ConfigureAwait(false), comprehension.Clauses[0].Iterable.Span, context).ConfigureAwait(false);
        using var scope = context.EnterComprehension(comprehension.ScopeFacts);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                context,
                async itemScope => result.Add(RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ItemExpression, itemScope).ConfigureAwait(false))), outer)
            .ConfigureAwait(false);

        context.CheckExecution(comprehension.Span);
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredSetComprehensionAsync(LoweredSetComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, comprehension.Span);
        try
        {
            await using var outer = await PyIteration.Cursor.CreateAsync(await EvaluateLoweredExpressionAsync(comprehension.Clauses[0].Iterable, context).ConfigureAwait(false), comprehension.Clauses[0].Iterable.Span, context).ConfigureAwait(false);
            using var scope = context.EnterComprehension(comprehension.ScopeFacts);
            await EvaluateLoweredComprehensionClausesAsync(
                    comprehension.Clauses,
                    0,
                    context,
                    async itemScope =>
                    {
                        var item = ValidateSetItem(
                            await EvaluateLoweredExpressionAsync(comprehension.ItemExpression, itemScope).ConfigureAwait(false),
                            comprehension.ItemExpression.Span);
                        using var ambient = PyStructuralGuard.PushAmbient(itemScope, comprehension.Span);
                        result.Add(item);
                    }, outer)
                .ConfigureAwait(false);

            context.CheckExecution(comprehension.Span);
        }
        catch
        {
            result.ReleaseUnpublishedStorage();
            throw;
        }
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredDictComprehensionAsync(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        await using var outer = await PyIteration.Cursor.CreateAsync(await EvaluateLoweredExpressionAsync(comprehension.Clauses[0].Iterable, context).ConfigureAwait(false), comprehension.Clauses[0].Iterable.Span, context).ConfigureAwait(false);
        using var scope = context.EnterComprehension(comprehension.ScopeFacts);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                context,
                async itemScope =>
                {
                    var key = ValidateDictionaryKey(
                        await EvaluateLoweredExpressionAsync(comprehension.KeyExpression, itemScope).ConfigureAwait(false),
                        comprehension.KeyExpression.Span);
                    var value = RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ValueExpression, itemScope).ConfigureAwait(false));
                    using var ambient = PyStructuralGuard.PushAmbient(itemScope, comprehension.Span);
                    result.SetItem(key, value);
                }, outer)
            .ConfigureAwait(false);

        context.CheckExecution(comprehension.Span);
        return result;
    }

    private static async ValueTask EvaluateLoweredComprehensionClausesAsync(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Func<ExecutionContext, ValueTask> emit,
        PyIteration.Cursor? outerSequence = null)
    {
        var clause = clauses[index];
        await using var sequence = outerSequence ?? await PyIteration.Cursor.CreateAsync(await EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false), clause.Iterable.Span, context).ConfigureAwait(false);

        while (true)
        {
            var nextItem = await sequence.TryMoveNextAsync().ConfigureAwait(false);
            if (!nextItem.HasValue) break;
            var item = nextItem.Value;
            await AssignTargetAsync(clause.StoreTarget, item, context).ConfigureAwait(false);

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
        return await ReadLoweredSubscriptAsync(target, index, subscript.Span, context).ConfigureAwait(false);
    }

    private static async ValueTask<object> EvaluateLoweredSliceAsync(LoweredSliceExpression slice, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(slice.Target, context).ConfigureAwait(false);
        var start = slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false);
        var end = slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false);
        var step = slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false);
        return await ReadSliceValueAsync(target, start, end, step, slice.Span, context).ConfigureAwait(false);
    }

    private static async ValueTask<object> EvaluateLoweredSliceValueAsync(LoweredSliceValueExpression slice, ExecutionContext context)
    {
        var start = slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false);
        var end = slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false);
        var step = slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false);
        return CreateSliceValue(start, end, step, slice.Span, context);
    }

    private static async ValueTask<object> ResolveLoweredMemberAsync(LoweredMemberExpression member, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(member.Target, context).ConfigureAwait(false);
        var result = await TryResolveRuntimeMemberAsync(target, member.Member.MemberName, context, member.Span).ConfigureAwait(false);
        if (!result.Found) throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span, context);
        return result.Value;
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

    private static async ValueTask<object> EvaluateLoweredChainedComparisonAsync(LoweredChainedComparisonExpression chained, ExecutionContext context)
    {
        var left = await EvaluateLoweredExpressionAsync(chained.Operands[0], context).ConfigureAwait(false);
        for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
        {
            var right = await EvaluateLoweredExpressionAsync(chained.Operands[i + 1], context).ConfigureAwait(false);
            var result = await EvaluateBinaryOperatorAsync(chained.ChainedComparison.Operators[i], left, right, context, chained.Span).ConfigureAwait(false);
            if (i == chained.ChainedComparison.Operators.Count - 1 || !await IsTruthyAsync(result, context, chained.Span).ConfigureAwait(false))
            {
                return result;
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

    private static async ValueTask<object> EvaluateLoweredAssignmentExpressionAsync(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = await EvaluateLoweredExpressionAsync(assignment.Expression, context).ConfigureAwait(false);
        return StoreLoweredAssignmentResult(assignment, value, context);
    }

    private static async ValueTask<object> CreateLoweredLambdaAsync(LoweredLambdaExpression lambda, ExecutionContext context)
    {
        var defaults = await EvaluateFunctionDefaults(lambda.DefaultValues, lambda.ParameterLayout, context, lambda.Span, true)
            .ConfigureAwait(false);
        return CreateLambdaWithUnpublishedDefaults(lambda.Binding, defaults, context, lambda.Span);
    }

    private static async ValueTask<object> InvokeLoweredCallAsync(LoweredCallExpression call, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(call.Target, context).ConfigureAwait(false);
        if (call.Call.PreloadImplicitSuperArguments)
        {
            context.CheckExecution(call.Span);
            PreloadImplicitSuperArguments(context, call.Span);
        }
        using var storage = context.MemoryGovernor.ReserveTemporary(0, call.Span);
        return await InvokeCallableTargetAsync(
                target,
                call.Call.Target.Span,
                call.Span,
                context,
                () => CallExpansion.ExpandLoweredArgumentsAsync(call.Arguments, context, EvaluateLoweredExpressionAsync, target, storage))
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
