using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static void AssignTarget(LoweredStoreTarget target, object value, ExecutionContext context)
        => AssignPreparedTargetAsync(target, value, context, false).GetAwaiter().GetResult();

    internal static ValueTask AssignTargetAsync(LoweredStoreTarget target, object value, ExecutionContext context)
        => AssignPreparedTargetAsync(target, value, context, true);

    private static async ValueTask AssignPreparedTargetAsync(LoweredStoreTarget target,
        object value, ExecutionContext context, bool asynchronous)
    {
        ValueTask<object> Read(LoweredExpression expression) => asynchronous
            ? EvaluateLoweredExpressionAsync(expression, context) : new(EvaluateLoweredExpression(expression, context));
        switch (target)
        {
            case LoweredNameStoreTarget name:
                StoreName(name.Name, value, context, name.Span);
                break;
            case LoweredMemberStoreTarget member:
                var receiver = await Read(member.Receiver).ConfigureAwait(false);
                if (!asynchronous) SetMemberValue(receiver, member.MemberName, value, member.Span, context);
                else if (!await PyMemberAccess.TryAssignAsync(receiver, member.MemberName, value, context, member.Span).ConfigureAwait(false))
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", member.Span);
                break;
            case LoweredSubscriptStoreTarget item:
                var container = await Read(item.Receiver).ConfigureAwait(false);
                var index = await Read(item.Index).ConfigureAwait(false);
                if (asynchronous) await SetHeaderSubscriptAsync(container, index, value, item.Span, context).ConfigureAwait(false);
                else SetSubscriptValue(container, index, value, item.Span, context);
                break;
            case LoweredSliceStoreTarget slice:
                var sequence = await Read(slice.Receiver).ConfigureAwait(false);
                var start = slice.Start is null ? PyNone.Instance : await Read(slice.Start).ConfigureAwait(false);
                var end = slice.End is null ? PyNone.Instance : await Read(slice.End).ConfigureAwait(false);
                var step = slice.Step is null ? PyNone.Instance : await Read(slice.Step).ConfigureAwait(false);
                if (asynchronous) await ExecuteSliceAssignmentAsync(sequence, start, end, step, value, slice.Span, context).ConfigureAwait(false);
                else ExecuteSliceAssignment(sequence, start, end, step, value, slice.Span, context);
                break;
            case LoweredUnpackingStoreTarget group:
            {
                // Receivers and setters can allocate or suspend while this
                // snapshot stays live; retain its reservation through all stores.
                var layout = group.Layout;
                using var materialized = asynchronous
                    ? await MaterializeHeaderSequenceAsync(value, group.Span, context, layout.MaximumReadCount).ConfigureAwait(false)
                    : MaterializeUnpackingSequence(value, group.Span, context, layout.MaximumReadCount);
                var values = materialized.Items;
                if (!layout.AcceptsValueCount(values.Count))
                    throw new LythonRuntimeException("ValueError", layout.DescribeArityMismatch(values.Count), group.Span);
                for (var i = 0; i < group.Items.Count; i++)
                {
                    object childValue;
                    if (i == layout.StarredTargetIndex)
                    {
                        var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Count)), context.MemoryGovernor, group.Span);
                        childValue = rest;
                    }
                    else childValue = values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                        ? layout.SourceIndexForTrailingTarget(i, values.Count) : i];
                    await AssignPreparedTargetAsync(group.Items[i], childValue, context, asynchronous).ConfigureAwait(false);
                }
                break;
            }
            default: throw new InvalidOperationException("Unknown prepared store target.");
        }
    }

    private static async ValueTask SetHeaderSubscriptAsync(object receiver, object index, object value,
        LythonSourceSpan span, ExecutionContext context)
    {
        if (receiver is PyList && index is PySlice slice)
        {
            await ExecuteSliceAssignmentAsync(receiver, slice.StartBound, slice.StopBound, slice.StepBound, value, span, context).ConfigureAwait(false);
            return;
        }
        if (UsesNativeMutationIndex(receiver) && index is PyInstance)
            index = await CoerceTypeIndexAsync(index, context, span, true).ConfigureAwait(false) ?? PyNone.Instance;
        if (receiver is PyInstance instance)
        {
            if (!instance.Type.TryLookupInMro("__setitem__", 0, out var raw, out _))
                throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
            var member = await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false);
            if (member is not ICallable callable)
                throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
            _ = await callable.InvokeAsync([CallArgumentValue.Positional(index), CallArgumentValue.Positional(value)], span, context).ConfigureAwait(false);
        }
        else SetSubscriptValue(receiver, index, value, span, context);
    }

    private static async ValueTask<object> UnpackAnnotationAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        using var materialized = asynchronous
            ? await MaterializeHeaderSequenceAsync(value, span, context, maximumItems: 2).ConfigureAwait(false)
            : MaterializeUnpackingSequence(value, span, context, maximumItems: 2);
        if (materialized.Items.Count != 1)
            throw new LythonRuntimeException("ValueError", DescribeLoopArityMismatch(1, materialized.Items.Count), span);
        return materialized.Items[0];
    }

    private static ValueTask<UnpackingValues> MaterializeHeaderSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context, int? maximumItems = null)
    {
        context.CheckExecution(span);
        if (value is PyTuple tuple) return new(new UnpackingValues(tuple));
        return MaterializeHeaderIteratorAsync(value, span, context, maximumItems);
    }

    private static async ValueTask<UnpackingValues> MaterializeHeaderIteratorAsync(object value, LythonSourceSpan span, ExecutionContext context, int? maximumItems)
    {
        try { return new(await PyIteration.MaterializeLeasedAsync(value, span, context, maximumItems).ConfigureAwait(false)); }
        catch (PyNotIterableException)
        {
            throw CannotUnpack(value, span, context);
        }
    }
}
