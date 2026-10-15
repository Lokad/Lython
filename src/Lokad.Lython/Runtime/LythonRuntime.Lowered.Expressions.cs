using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;
using System.Runtime.ExceptionServices;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Per-expression evaluation intentionally does not track interpreter depth:
    // depth is tracked once per Python nesting level (calls, bodies,
    // compound statements), so deep-but-shallow expression trees cannot
    // exhaust the counter budget while genuine nesting still trips it.
    // Execution steps are still counted for every node (MG25).
    internal static object EvaluateLoweredExpression(LoweredExpression expression, ExecutionContext context)
    {
        context.CheckExecution(expression.Span);
        LythonRuntimeException failure;
        try
        {
            ExecutionGuards.EnsureInterpreterStack(expression.Span);
            return context.UseSynchronousLoweredFunctionDispatch
                ? DispatchLoweredExpression(expression, context)
                : DispatchLoweredExpressionAsync(expression, context, SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            failure = ex;
        }

        // Propagate after releasing the catch's native exception state, just
        // as ExecuteStatements does, while preserving the original exception.
        ExceptionDispatchInfo.Throw(failure);
        return PyNone.Instance;
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
        return expanded.Count == 0 ? PyTuple.Empty : new PyTuple(expanded, context.MemoryGovernor, tuple.Span);
    }

    private static object EvaluateLoweredDictLiteral(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is LoweredDictionaryUnpackingItem unpacking)
            {
                var mapping = RuntimeValue(EvaluateLoweredExpression(unpacking.Mapping, context));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Item.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Item.Span), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (LoweredDictionaryKeyValueItem)item;
            var key = RuntimeValue(EvaluateLoweredExpression(keyValue.Key, context));
            var value = RuntimeValue(EvaluateLoweredExpression(keyValue.Value, context));
            result.SetItem(ValidateDictionaryKey(key, keyValue.Key.Span), value);
            context.ObserveCollectionCount(result.Count, dict.Span);
        }
        return result;
    }

    private static object EvaluateLoweredSetLiteral(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, set.Span);
        var items = new PySet(context.MemoryGovernor, set.Span);
        try
        {
            for (var i = 0; i < set.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateLoweredExpression(set.Items[i].Expression, context));
                AddSetLiteralItem(items, value, set.Items[i].IsUnpacking, set.Items[i].Span, set.Span, context);
            }
        }
        catch
        {
            items.ReleaseUnpublishedStorage();
            throw;
        }
        return items;
    }

    private static object EvaluateLoweredFormattedString(LoweredFormattedStringExpression formatted, ExecutionContext context)
        => EvaluateLoweredFormattedStringPartsCoreAsync(
                formatted.Parts,
                context,
                formatted.Span,
                expression => ValueTask.FromResult(EvaluateLoweredExpression(expression, context)),
                asynchronous: false)
            .GetAwaiter()
            .GetResult();

    private static object EvaluateLoweredListComprehension(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        var outer = ToSequence(EvaluateLoweredExpression(comprehension.Clauses[0].Iterable, context), comprehension.Clauses[0].Iterable.Span, context);
        using var scope = context.EnterComprehension(comprehension.ScopeFacts);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            itemScope => result.Add(RuntimeValue(EvaluateLoweredExpression(comprehension.ItemExpression, itemScope))), outer);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateLoweredSetComprehension(LoweredSetComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, comprehension.Span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, comprehension.Span);
        try
        {
            var outer = ToSequence(EvaluateLoweredExpression(comprehension.Clauses[0].Iterable, context), comprehension.Clauses[0].Iterable.Span, context);
            using var scope = context.EnterComprehension(comprehension.ScopeFacts);
            EvaluateLoweredComprehensionClauses(
                comprehension.Clauses,
                0,
                context,
                itemScope =>
                {
                    var item = ValidateSetItem(
                        EvaluateLoweredExpression(comprehension.ItemExpression, itemScope),
                        comprehension.ItemExpression.Span);
                    result.Add(item);
                }, outer);

            context.ObserveCollectionCount(result.Count, comprehension.Span);
        }
        catch
        {
            result.ReleaseUnpublishedStorage();
            throw;
        }
        return result;
    }

    private static object EvaluateLoweredDictComprehension(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, comprehension.Span);
        var outer = ToSequence(EvaluateLoweredExpression(comprehension.Clauses[0].Iterable, context), comprehension.Clauses[0].Iterable.Span, context);
        using var scope = context.EnterComprehension(comprehension.ScopeFacts);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            itemScope =>
            {
                var key = ValidateDictionaryKey(EvaluateLoweredExpression(comprehension.KeyExpression, itemScope), comprehension.KeyExpression.Span);
                result.SetItem(key, RuntimeValue(EvaluateLoweredExpression(comprehension.ValueExpression, itemScope)));
            }, outer);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static void EvaluateLoweredComprehensionClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Action<ExecutionContext> emit,
        IEnumerable<object>? outerSequence = null)
    {
        var clause = clauses[index];
        var sequence = outerSequence ?? ToSequence(EvaluateLoweredExpression(clause.Iterable, context), clause.Iterable.Span, context);

        foreach (var item in sequence)
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
        return ReadSliceValue(target, start, end, step, slice.Span, context);
    }

    private static object EvaluateLoweredSliceValue(LoweredSliceValueExpression slice, ExecutionContext context)
    {
        var start = slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context);
        var end = slice.End is null ? null : EvaluateLoweredExpression(slice.End, context);
        var step = slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context);
        return CreateSliceValue(start, end, step, slice.Span, context);
    }

    private static PySlice CreateSliceValue(object? start, object? end, object? step, LythonSourceSpan span, ExecutionContext context)
        => PyIndexing.CreateMappingSliceKey(start, end, step, context, span);

    private static object ReadSliceValue(object target, object? start, object? end, object? step, LythonSourceSpan span, ExecutionContext context)
        => target is PyInstance instance
            ? GetUserItem(instance, CreateSliceValue(start, end, step, span, context), context, span)
            : PyIndexing.ReadSlice(target, start, end, step, span, context);

    private static ValueTask<object> ReadSliceValueAsync(object target, object? start, object? end, object? step, LythonSourceSpan span, ExecutionContext context)
        => target is ElementTreeModule.Element element
            ? ElementTreeModule.ReadSliceAsync(element, start, end, step, context, span, true)
            : target is PyInstance instance
            ? GetUserItemAsync(instance, CreateSliceValue(start, end, step, span, context), context, span)
            : new(PyIndexing.ReadSlice(target, start, end, step, span, context));

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
        ExecuteLoweredDeleteTarget(statement.Target.Syntax, statement.Target, statement.Span, context);
    }

    private static void ExecuteLoweredDeleteTarget(ExpressionSyntax syntax, LoweredExpression loweredTarget, LythonSourceSpan span, ExecutionContext context)
    {
        while (syntax is ParenthesizedExpressionSyntax parenthesized
            && loweredTarget is LoweredParenthesizedExpression loweredParenthesized)
        {
            syntax = parenthesized.Inner;
            loweredTarget = loweredParenthesized.Inner;
        }

        if (TryGetDeleteDisplayItems(loweredTarget, out var syntaxItems, out var loweredItems))
        {
            for (var i = 0; i < syntaxItems.Count; i++)
            {
                if (syntaxItems[i] is CollectionValueItemSyntax valueItem)
                {
                    ExecuteLoweredDeleteTarget(
                        valueItem.Expression,
                        loweredItems[i].Expression,
                        valueItem.Span,
                        context);
                }
            }

            return;
        }

        switch (syntax)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, span))
                {
                    throw MissingDeletedName(identifier.Name, context, span);
                }

                return;

            case SubscriptExpressionSyntax:
                if (loweredTarget is not LoweredSubscriptExpression subscript)
                {
                    break;
                }

                var target = EvaluateLoweredExpression(subscript.Target, context);
                var index = EvaluateLoweredExpression(subscript.Index, context);
                ExecuteResolvedSubscriptDeletion(target, index, span, context);
                return;

            case SliceExpressionSyntax:
                if (loweredTarget is not LoweredSliceExpression slice)
                {
                    break;
                }

                ExecuteSliceDeletion(
                    EvaluateLoweredExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateLoweredExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context),
                    span, context);
                return;

            case MemberExpressionSyntax memberSyntax:
                if (loweredTarget is not LoweredMemberExpression member)
                {
                    break;
                }

                var memberTarget = EvaluateLoweredExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, memberSyntax.MemberName, context, span))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, memberSyntax.MemberName, span, context, operation: MissingMemberOperation.Delete);
                }

                return;
        }

        throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", span);
    }

    private static void ExecuteResolvedSubscriptDeletion(
        object target,
        object index,
        LythonSourceSpan span,
        ExecutionContext context)
    {
            using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        // Slice objects delete through the shared slice path on lists like
        // CPython; every other receiver keeps its existing behaviour.
        if (index is PySlice slice && target is PyList)
        {
            ExecuteSliceDeletion(target, slice.StartBound, slice.StopBound, slice.StepBound, span, context);
            return;
        }

        switch (target)
        {
            case IDeletablePySubscriptableValue subscriptable:
                subscriptable.DeleteSubscript(index, span);
                return;
            case IMutablePySequenceValue sequence:
                sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, span, PyIndexing.TargetKind(sequence), PyIndexing.IndexOperation.Delete));
                return;
            case PyDict dict:
                if (!dict.Remove(ValidateDictionaryKey(index, span)))
                {
                    throw RuntimeErrors.MissingKey(index, span);
                }

                return;
            case PyDefaultDict defaultDict:
                if (!defaultDict.Remove(ValidateDictionaryKey(index, span)))
                {
                    throw RuntimeErrors.MissingKey(index, span);
                }

                return;
            case PyCounter counter:
                _ = counter.Remove(ValidateDictionaryKey(index, span));
                return;
            case PyInstance instance:
                InvokeItemMutation(instance, "__delitem__", [CallArgumentValue.Positional(index)], context, span);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "'tuple' object doesn't support item deletion", span);
            case PyString:
                throw new LythonRuntimeException("TypeError", "'str' object doesn't support item deletion", span);
            case PyBytes:
                throw new LythonRuntimeException("TypeError", "'bytes' object doesn't support item deletion", span);
            default:
                throw DeletionNotSupported(target, context, span);
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
        var cause = statement.CauseExpression is null ? null : EvaluateLoweredExpression(statement.CauseExpression, context);
        ThrowRaisedValue(raised, statement.CauseExpression is not null, cause, context, statement.Span);
    }

    private static void ThrowRaisedValue(object raised, bool hasCause, object? cause, ExecutionContext context, LythonSourceSpan span)
    {
        if (raised is ExceptionTypeValue typeValue && typeValue.Invoke([], span, context) is PyException constructed)
            raised = constructed;
        if (raised is not PyException instance) throw RuntimeErrors.RaiseExpectsException(span);
        var thrown = new LythonRuntimeException(instance.Identity, instance.Message, span, null, instance.Value);
        AttachImplicitRaiseChain(thrown, instance, context);
        if (hasCause)
        {
            thrown.PythonCause = CoerceRaiseCause(cause!, span, context);
            thrown.SuppressPythonContext = true;
        }
        throw thrown;
    }

    private static object CreateLoweredLambda(LoweredLambdaExpression lambda, ExecutionContext context)
    {
        var loweredParameters = LowerLambdaParameters(lambda);
        using var defaultStorage = ReserveDefaultArgumentsWorkingStorage(loweredParameters.Count(static p => p.DefaultValue is not null), context.MemoryGovernor, lambda.Span);
        return CreateLambdaFunction(lambda, loweredParameters,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)), context);
    }

    private static object EvaluateLoweredAssignmentExpression(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = EvaluateLoweredExpression(assignment.Expression, context);
        return StoreLoweredAssignmentResult(assignment, value, context);
    }

    private static object InvokeLoweredCall(LoweredCallExpression call, ExecutionContext context)
    {
        object target;
        if (call.Target is LoweredMemberExpression member)
        {
            // Preserve the member expression's checkpoint and stack admission;
            // only this immediate call may reuse its bound callable.
            context.CheckExecution(member.Span);
            ExecutionGuards.EnsureInterpreterStack(member.Span);
            target = ResolveLoweredMemberValue(member, EvaluateLoweredExpression(member.Target, context), context, true);
        }
        else target = EvaluateLoweredExpression(call.Target, context);
        if (call.Call.PreloadImplicitSuperArguments)
        {
            context.CheckExecution(call.Span);
            PreloadImplicitSuperArguments(context, call.Span);
        }
        return InvokeCallableTarget(
            target,
            call.Call.Target.Span,
            call.Span,
            context,
            () => CallExpansion.ExpandLoweredArguments(call.Arguments, context, EvaluateLoweredExpression, target));
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
