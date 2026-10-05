using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static ValueTask AssignTargetAsync(LoweredStoreTarget target, object value, ExecutionContext context)
        => AssignTargetAsync(target.Syntax, target.Reads, value, context);

    private static async ValueTask AssignTargetAsync(AssignmentTargetSyntax target,
        IReadOnlyDictionary<ExpressionSyntax, LoweredExpression> reads, object value, ExecutionContext context)
    {
        ValueTask<object> Read(ExpressionSyntax expression) => EvaluateLoweredExpressionAsync(reads[expression], context);
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                StoreName(name.Name, value, context, name.Span);
                break;
            case MemberAssignmentTargetSyntax member:
                var receiver = await Read(member.Target).ConfigureAwait(false);
                if (!await PyMemberAccess.TryAssignAsync(receiver, member.MemberName, value, context, member.Span).ConfigureAwait(false))
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", member.Span);
                break;
            case SubscriptAssignmentTargetSyntax item:
                var container = await Read(item.Target).ConfigureAwait(false);
                var index = await Read(item.Index).ConfigureAwait(false);
                await SetHeaderSubscriptAsync(container, index, value, item.Span, context).ConfigureAwait(false);
                break;
            case SliceAssignmentTargetSyntax slice:
                var sequence = await Read(slice.Target).ConfigureAwait(false);
                var start = slice.Start is null ? PyNone.Instance : await Read(slice.Start).ConfigureAwait(false);
                var end = slice.End is null ? PyNone.Instance : await Read(slice.End).ConfigureAwait(false);
                var step = slice.Step is null ? PyNone.Instance : await Read(slice.Step).ConfigureAwait(false);
                if (sequence is PyInstance)
                    await SetHeaderSubscriptAsync(sequence, new PySlice(start, end, step), value, slice.Span, context).ConfigureAwait(false);
                else ExecuteSliceAssignment(sequence, start, end, step, value, slice.Span, context);
                break;
            case UnpackingAssignmentTargetGroupSyntax group:
                var values = await MaterializeHeaderSequenceAsync(value, group.Span, context).ConfigureAwait(false);
                var layout = UnpackingLayout.FromTargets(group.Targets);
                if (!layout.AcceptsValueCount(values.Length))
                    throw new LythonRuntimeException("ValueError", layout.DescribeArityMismatch(values.Length), group.Span);
                for (var i = 0; i < group.Targets.Count; i++)
                {
                    object childValue;
                    if (i == layout.StarredTargetIndex)
                    {
                        var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Length)), context.MemoryGovernor, group.Span);
                        context.Services.State.CallTemporaries.TrackFreshMutable(rest, rest.CommittedStorageBytes);
                        childValue = rest;
                    }
                    else childValue = values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                        ? layout.SourceIndexForTrailingTarget(i, values.Length) : i];
                    await AssignTargetAsync(AssignmentTargetFacts.FromUnpacking(group.Targets[i]), reads, childValue, context).ConfigureAwait(false);
                }
                break;
        }
    }

    private static async ValueTask SetHeaderSubscriptAsync(object receiver, object index, object value,
        LythonSourceSpan span, ExecutionContext context)
    {
        if (receiver is PyInstance instance)
        {
            if (!instance.TryGetAttribute("__setitem__", context, span, out var member) || member is not ICallable callable)
                throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
            _ = await callable.InvokeAsync([CallArgumentValue.Positional(index), CallArgumentValue.Positional(value)], span, context).ConfigureAwait(false);
        }
        else SetSubscriptValue(receiver, index, value, span, context);
    }

    private static async ValueTask<object[]> MaterializeHeaderSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context)
    {
        try { return (await PyIteration.MaterializeAsync(value, span, context).ConfigureAwait(false)).ToArray(); }
        catch (PyNotIterableException)
        {
            throw new LythonRuntimeException("TypeError", $"cannot unpack non-iterable {UnboundTypeMethod.PythonTypeName(value, context)} object", span);
        }
    }
}
