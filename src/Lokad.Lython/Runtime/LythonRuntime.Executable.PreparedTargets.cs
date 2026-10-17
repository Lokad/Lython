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
                case ExecutableStoreMember member:
                {
                    var first = _stack.Count - 2;
                    var receiver = _stack[first + (member.ValueLast ? 0 : 1)];
                    var value = _stack[first + (member.ValueLast ? 1 : 0)];
                    try
                    {
                        if (asynchronous)
                        {
                            if (!await PyMemberAccess.TryAssignAsync(receiver, member.MemberName,
                                value, _context, member.TargetSpan).ConfigureAwait(false))
                                throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", member.TargetSpan);
                        }
                        else SetMemberValue(receiver, member.MemberName, value, member.TargetSpan, _context);
                    }
                    finally { _stack.RemoveTail(2); }
                    break;
                }
                case ExecutableReadRetainedSubscript:
                {
                    var first = _stack.Count - 2;
                    try
                    {
                        // Keep the captured receiver and raw key through RHS and store.
                        var value = asynchronous
                            ? await ReadLoweredSubscriptAsync(_stack[first], _stack[first + 1], span, _context).ConfigureAwait(false)
                            : ReadSubscriptValue(_stack[first], _stack[first + 1], span, _context);
                        PushObserved(value, span);
                    }
                    catch { _stack.RemoveTail(_stack.Count - first); throw; }
                    break;
                }
                case ExecutableStoreSubscript store:
                {
                    var first = _stack.Count - 3;
                    var receiver = _stack[first + (store.ValueLast ? 0 : 1)];
                    var key = _stack[first + (store.ValueLast ? 1 : 2)];
                    var value = _stack[first + (store.ValueLast ? 2 : 0)];
                    try
                    {
                        if (asynchronous)
                            await SetHeaderSubscriptAsync(receiver, key, value, store.TargetSpan, _context).ConfigureAwait(false);
                        else SetSubscriptValue(receiver, key, value, store.TargetSpan, _context);
                    }
                    finally { _stack.RemoveTail(3); }
                    break;
                }
                case ExecutableReadRetainedSlice:
                {
                    var first = _stack.Count - 4;
                    try
                    {
                        var receiver = _stack[first];
                        var start = _stack[first + 1];
                        var stop = _stack[first + 2];
                        var step = _stack[first + 3];
                        object value;
                        if (receiver is PyInstance instance)
                        {
                            var key = CreateSliceValue(start, stop, step, span, _context);
                            // The existing start slot retains the exact key for the store.
                            _stack[first + 1] = key;
                            value = asynchronous
                                ? await GetUserItemAsync(instance, key, _context, span).ConfigureAwait(false)
                                : GetUserItem(instance, key, _context, span);
                        }
                        else value = asynchronous
                            ? await ReadSliceValueAsync(receiver, start, stop, step, span, _context).ConfigureAwait(false)
                            : ReadSliceValue(receiver, start, stop, step, span, _context);
                        PushObserved(value, span);
                    }
                    catch { _stack.RemoveTail(_stack.Count - first); throw; }
                    break;
                }
                case ExecutableStoreSlice slice:
                {
                    var first = _stack.Count - 5;
                    try
                    {
                        var receiverIndex = first + (slice.ValueLast ? 0 : 1);
                        var receiver = _stack[receiverIndex];
                        var start = _stack[receiverIndex + 1];
                        var stop = _stack[receiverIndex + 2];
                        var step = _stack[receiverIndex + 3];
                        var value = _stack[first + (slice.ValueLast ? 4 : 0)];
                        if (slice.ValueLast && receiver is PyInstance)
                        {
                            if (asynchronous)
                                await SetHeaderSubscriptAsync(receiver, (PySlice)start, value, slice.TargetSpan, _context).ConfigureAwait(false);
                            else SetSubscriptValue(receiver, (PySlice)start, value, slice.TargetSpan, _context);
                        }
                        else if (asynchronous)
                            await ExecuteSliceAssignmentAsync(receiver, start, stop, step, value, slice.TargetSpan, _context).ConfigureAwait(false);
                        else ExecuteSliceAssignment(receiver, start, stop, step, value, slice.TargetSpan, _context);
                    }
                    finally { _stack.RemoveTail(5); }
                    break;
                }
                case ExecutableFailAssertion assertion:
                    ThrowAssertionError(assertion.HasMessage ? Pop(ref _stack, span) : null, _context, span);
                    break;
                case ExecutableRaise raised:
                    var cause = raised.HasCause ? Pop(ref _stack, span) : null;
                    ThrowRaisedValue(Pop(ref _stack, span), raised.HasCause, cause, _context, span);
                    break;
                case ExecutableDeletePreparedTarget deleted:
                    await DeletePreparedTargetAsync(deleted.Target, span, asynchronous).ConfigureAwait(false);
                    break;
                case ExecutableUnpackValues unpack:
                    using (var materialized = asynchronous
                        ? await MaterializeHeaderSequenceAsync(Pop(ref _stack, span), span, _context, unpack.Layout.MaximumReadCount).ConfigureAwait(false)
                        : MaterializeUnpackingSequence(Pop(ref _stack, span), span, _context, unpack.Layout.MaximumReadCount))
                    {
                        var values = materialized.Items;
                        var layout = unpack.Layout;
                        if (!layout.AcceptsValueCount(values.Count))
                            throw new LythonRuntimeException("ValueError", layout.DescribeArityMismatch(values.Count), span);
                        for (var i = layout.TargetCount - 1; i >= 0; i--)
                        {
                            if (i == layout.StarredTargetIndex)
                            {
                                var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Count)), _context.MemoryGovernor, span);
                                _stack.Push(rest);
                            }
                            else _stack.Push(values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                                ? layout.SourceIndexForTrailingTarget(i, values.Count) : i]);
                        }
                    }
                    break;
                default: throw new InvalidOperationException("Unknown prepared target operation.");
            }
        }

        private async ValueTask DeletePreparedTargetAsync(AssignmentTargetSyntax target, LythonSourceSpan span, bool asynchronous)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax name:
                    if (!DeleteName(name.Name, _context, span))
                        throw MissingDeletedName(name.Name, _context, span);
                    break;
                case MemberAssignmentTargetSyntax member:
                    try
                    {
                        var receiver = _stack[_stack.Count - 1];
                        var deleted = asynchronous ? await PyMemberAccess.TryDeleteAsync(receiver, member.MemberName, _context, span).ConfigureAwait(false)
                            : PyMemberAccess.TryDelete(receiver, member.MemberName, _context, span);
                        if (!deleted) throw PyMemberAccess.CreateMissingMemberError(receiver, member.MemberName, span, _context, operation: MissingMemberOperation.Delete);
                    }
                    finally { _stack.RemoveTail(1); }
                    break;
                case SubscriptAssignmentTargetSyntax:
                {
                    var first = _stack.Count - 2;
                    try
                    {
                        if (asynchronous) await ExecuteResolvedSubscriptDeletionAsync(_stack[first], _stack[first + 1], span, _context).ConfigureAwait(false);
                        else DeleteSubscriptValue(_stack[first], _stack[first + 1], span, _context);
                    }
                    finally { _stack.RemoveTail(2); }
                    break;
                }
                case SliceAssignmentTargetSyntax slice:
                {
                    // Match the compiler's read order and retain every raw operand
                    // until deletion (including protocol I/O) has finished.
                    var count = 1 + (slice.Start is null ? 0 : 1) + (slice.End is null ? 0 : 1) + (slice.Step is null ? 0 : 1);
                    var first = _stack.Count - count;
                    try
                    {
                        var sequence = _stack[first];
                        var next = first + 1;
                        var start = slice.Start is null ? null : _stack[next++];
                        var end = slice.End is null ? null : _stack[next++];
                        var step = slice.Step is null ? null : _stack[next];
                        if (asynchronous) await ExecuteSliceDeletionAsync(sequence, start, end, step, span, _context).ConfigureAwait(false);
                        else ExecuteSliceDeletion(sequence, start, end, step, span, _context);
                    }
                    finally { _stack.RemoveTail(count); }
                    break;
                }
                default: throw new InvalidOperationException("Invalid prepared delete target.");
            }
        }
    }
}
