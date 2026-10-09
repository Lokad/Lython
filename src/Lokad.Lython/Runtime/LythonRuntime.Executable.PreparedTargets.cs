using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private async ValueTask ExecutePreparedTargetOperationAsync(ExecutableOperation operation, LythonSourceSpan span, bool asynchronous)
        {
            switch (operation)
            {
                case ExecutableFailAssertion assertion:
                    ThrowAssertionError(assertion.HasMessage ? Pop(_stack, span) : null, context, span);
                    break;
                case ExecutableRaise raised:
                    var cause = raised.HasCause ? Pop(_stack, span) : null;
                    ThrowRaisedValue(Pop(_stack, span), raised.HasCause, cause, context, span);
                    break;
                case ExecutableDeletePreparedTarget deleted:
                    using (var storage = context.MemoryGovernor.ReserveTemporary(1024, span))
                    {
                        var reads = AssignmentTargetFacts.Reads(deleted.Target).ToArray();
                        var values = CaptureTargetReads(reads, _stack.Count - reads.Length);
                        _stack.RemoveTail(reads.Length);
                        await DeletePreparedTargetAsync(deleted.Target, values, context, span, asynchronous).ConfigureAwait(false);
                        SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                    }
                    break;
                case ExecutableUnpackValues unpack:
                    using (var materialized = asynchronous
                        ? await MaterializeHeaderSequenceAsync(Pop(_stack, span), span, context).ConfigureAwait(false)
                        : MaterializeUnpackingSequence(Pop(_stack, span), span, context))
                    {
                        var values = materialized.Items;
                        var layout = unpack.Layout;
                        if (!layout.AcceptsValueCount(values.Count))
                            throw new LythonRuntimeException("ValueError", layout.DescribeArityMismatch(values.Count), span);
                        for (var i = layout.TargetCount - 1; i >= 0; i--)
                        {
                            if (i == layout.StarredTargetIndex)
                            {
                                var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Count)), context.MemoryGovernor, span);
                                context.Services.State.CallTemporaries.TrackFreshMutable(rest, rest.CommittedStorageBytes, span);
                                _stack.Push(rest);
                            }
                            else _stack.Push(values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                                ? layout.SourceIndexForTrailingTarget(i, values.Count) : i]);
                        }
                    }
                    break;
                case ExecutableReadPreparedTarget read:
                    using (var storage = context.MemoryGovernor.ReserveTemporary(1024, span))
                    {
                        var reads = AssignmentTargetFacts.Reads(read.Target).ToArray();
                        var values = CaptureTargetReads(reads, _stack.Count - reads.Length);
                        PushObserved(await ReadPreparedTargetAsync(read.Target, values, context, span, asynchronous).ConfigureAwait(false), span);
                    }
                    break;
                case ExecutableStorePreparedTarget store:
                    using (var storage = context.MemoryGovernor.ReserveTemporary(1024, span))
                    {
                        var result = store.ValueLast ? Pop(_stack, span) : null;
                        var reads = AssignmentTargetFacts.Reads(store.Target).ToArray();
                        var values = CaptureTargetReads(reads, _stack.Count - reads.Length);
                        _stack.RemoveTail(reads.Length);
                        result ??= Pop(_stack, span);
                        await StorePreparedTargetAsync(store.Target, values, result, context, span, asynchronous).ConfigureAwait(false);
                    }
                    break;
                default: throw new InvalidOperationException("Unknown prepared target operation.");
            }
        }

        private Dictionary<ExpressionSyntax, object> CaptureTargetReads(IReadOnlyList<ExpressionSyntax> reads, int first)
        {
            var result = new Dictionary<ExpressionSyntax, object>(reads.Count);
            for (var i = 0; i < reads.Count; i++) result[reads[i]] = _stack[first + i];
            return result;
        }
    }

    private static async ValueTask DeletePreparedTargetAsync(AssignmentTargetSyntax target,
        IReadOnlyDictionary<ExpressionSyntax, object> reads, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                if (!DeleteName(name.Name, context, span))
                    throw MissingDeletedName(name.Name, context, span);
                break;
            case MemberAssignmentTargetSyntax member:
                var receiver = reads[member.Target];
                var deleted = asynchronous ? await PyMemberAccess.TryDeleteAsync(receiver, member.MemberName, context, span).ConfigureAwait(false)
                    : PyMemberAccess.TryDelete(receiver, member.MemberName, context, span);
                if (!deleted) throw PyMemberAccess.CreateMissingMemberError(receiver, member.MemberName, span, context, operation: MissingMemberOperation.Delete);
                break;
            case SubscriptAssignmentTargetSyntax item:
                if (asynchronous) await ExecuteResolvedSubscriptDeletionAsync(reads[item.Target], reads[item.Index], span, context).ConfigureAwait(false);
                else ExecuteResolvedSubscriptDeletion(reads[item.Target], reads[item.Index], span, context);
                break;
            case SliceAssignmentTargetSyntax slice:
                var sequence = reads[slice.Target];
                var start = slice.Start is null ? null : reads[slice.Start];
                var end = slice.End is null ? null : reads[slice.End];
                var step = slice.Step is null ? null : reads[slice.Step];
                if (asynchronous) await ExecuteSliceDeletionAsync(sequence, start, end, step, span, context).ConfigureAwait(false);
                else ExecuteSliceDeletion(sequence, start, end, step, span, context);
                break;
            default: throw new InvalidOperationException("Invalid prepared delete target.");
        }
    }

    private static async ValueTask<object> ReadPreparedTargetAsync(AssignmentTargetSyntax target,
        IReadOnlyDictionary<ExpressionSyntax, object> reads, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        switch (target)
        {
            case MemberAssignmentTargetSyntax member:
                var receiver = reads[member.Target];
                var result = asynchronous ? await TryResolveRuntimeMemberAsync(receiver, member.MemberName, context, span).ConfigureAwait(false)
                    : TryResolveRuntimeMember(receiver, member.MemberName, context, span, out var value)
                        ? (Found: true, Value: value) : (Found: false, Value: (object)PyNone.Instance);
                if (!result.Found) throw PyMemberAccess.CreateMissingMemberError(receiver, member.MemberName, span, context);
                return result.Value;
            case SubscriptAssignmentTargetSyntax item:
                var container = reads[item.Target];
                var index = reads[item.Index];
                return container is PyInstance instance
                    ? asynchronous ? await GetUserItemAsync(instance, index, context, span).ConfigureAwait(false) : GetUserItem(instance, index, context, span)
                    : ReadSubscriptValue(container, index, span, context);
            case SliceAssignmentTargetSyntax slice:
                var sequence = reads[slice.Target];
                var start = slice.Start is null ? PyNone.Instance : reads[slice.Start];
                var end = slice.End is null ? PyNone.Instance : reads[slice.End];
                var step = slice.Step is null ? PyNone.Instance : reads[slice.Step];
                if (sequence is PyInstance sequenceInstance)
                {
                    var key = new PySlice(start, end, step);
                    return asynchronous ? await GetUserItemAsync(sequenceInstance, key, context, span).ConfigureAwait(false)
                        : GetUserItem(sequenceInstance, key, context, span);
                }
                return PyIndexing.ReadSlice(sequence, start, end, step, span, context);
            default: throw new InvalidOperationException("Prepared reads require an attribute or item target.");
        }
    }

    private static async ValueTask StorePreparedTargetAsync(AssignmentTargetSyntax target,
        IReadOnlyDictionary<ExpressionSyntax, object> reads, object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        switch (target)
        {
            case MemberAssignmentTargetSyntax member:
                var receiver = reads[member.Target];
                var assigned = asynchronous ? await PyMemberAccess.TryAssignAsync(receiver, member.MemberName, value, context, span).ConfigureAwait(false)
                    : PyMemberAccess.TryAssign(receiver, member.MemberName, value, context, span);
                if (!assigned) throw RuntimeErrors.Type("Object does not support attribute assignment", span);
                break;
            case SubscriptAssignmentTargetSyntax item:
                if (asynchronous) await SetHeaderSubscriptAsync(reads[item.Target], reads[item.Index], value, span, context).ConfigureAwait(false);
                else SetSubscriptValue(reads[item.Target], reads[item.Index], value, span, context);
                break;
            case SliceAssignmentTargetSyntax slice:
                var sequence = reads[slice.Target];
                var start = slice.Start is null ? PyNone.Instance : reads[slice.Start];
                var end = slice.End is null ? PyNone.Instance : reads[slice.End];
                var step = slice.Step is null ? PyNone.Instance : reads[slice.Step];
                if (sequence is PyInstance)
                {
                    var key = new PySlice(start, end, step);
                    if (asynchronous) await SetHeaderSubscriptAsync(sequence, key, value, span, context).ConfigureAwait(false);
                    else SetSubscriptValue(sequence, key, value, span, context);
                }
                else ExecuteSliceAssignment(sequence, start, end, step, value, span, context);
                break;
            default: throw new InvalidOperationException("Prepared stores require an attribute or item target.");
        }
    }
}
