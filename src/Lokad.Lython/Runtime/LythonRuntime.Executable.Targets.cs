using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Target shape and physical operands are compiler-owned instructions.
        // Only incomplete protocol operations enter an async continuation.
        private ValueTask ExecuteTargetInstruction(ExecutableInstruction instruction, bool asynchronous)
        {
            var first = instruction.InputStart;
            var span = instruction.Span;
            ValueTask mutation = default;
            try
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast:
                    {
                        var receiver = _stack[first + (instruction.ValueLast ? 0 : 1)];
                        var value = _stack[first + (instruction.ValueLast ? 1 : 0)];
                        var name = _codeObject.Names[instruction.NameIndex];
                        var targetSpan = (LythonSourceSpan)_codeObject.Constants[instruction.TargetSpanIndex]!;
                        if (asynchronous)
                            mutation = CompleteMemberMutation(PyMemberAccess.TryAssignAsync(receiver, name, value, _context, targetSpan),
                                receiver, name, targetSpan, deleting: false);
                        else SetMemberValue(receiver, name, value, targetSpan, _context);
                        break;
                    }
                    case ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast:
                    {
                        var receiver = _stack[first + (instruction.ValueLast ? 0 : 1)];
                        var key = _stack[first + (instruction.ValueLast ? 1 : 2)];
                        var value = _stack[first + (instruction.ValueLast ? 2 : 0)];
                        var targetSpan = (LythonSourceSpan)_codeObject.Constants[instruction.TargetSpanIndex]!;
                        if (asynchronous) mutation = SetHeaderSubscriptAsync(receiver, key, value, targetSpan, _context);
                        else SetSubscriptValue(receiver, key, value, targetSpan, _context);
                        break;
                    }
                    case ExecutableOpCode.ReadRetainedSubscript:
                    {
                        ValueTask<object> read;
                        try
                        {
                            read = asynchronous
                                ? ReadLoweredSubscriptAsync(_stack[first], _stack[first + 1], span, _context)
                                : new(ReadSubscriptValue(_stack[first], _stack[first + 1], span, _context));
                        }
                        catch { _stack.RetireTo(first); throw; }
                        return CompleteTargetRead(read, instruction);
                    }
                    case ExecutableOpCode.ReadRetainedSlice:
                    {
                        ValueTask<object> read;
                        try
                        {
                            var receiver = _stack[first];
                            var start = _stack[first + 1];
                            var stop = _stack[first + 2];
                            var step = _stack[first + 3];
                            if (receiver is PyInstance instance)
                            {
                                var key = CreateSliceValue(start, stop, step, span, _context);
                                // Retain this exact key through RHS and augmented store.
                                _stack[first + 1] = key;
                                read = asynchronous ? GetUserItemAsync(instance, key, _context, span)
                                    : new(GetUserItem(instance, key, _context, span));
                            }
                            else read = asynchronous ? ReadSliceValueAsync(receiver, start, stop, step, span, _context)
                                : new(ReadSliceValue(receiver, start, stop, step, span, _context));
                        }
                        catch { _stack.RetireTo(first); throw; }
                        return CompleteTargetRead(read, instruction);
                    }
                    case ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast:
                    {
                        var receiverIndex = first + (instruction.ValueLast ? 0 : 1);
                        var receiver = _stack[receiverIndex];
                        var start = _stack[receiverIndex + 1];
                        var stop = _stack[receiverIndex + 2];
                        var step = _stack[receiverIndex + 3];
                        var value = _stack[first + (instruction.ValueLast ? 4 : 0)];
                        var targetSpan = (LythonSourceSpan)_codeObject.Constants[instruction.TargetSpanIndex]!;
                        if (instruction.ValueLast && receiver is PyInstance)
                        {
                            if (asynchronous) mutation = SetHeaderSubscriptAsync(receiver, (PySlice)start, value, targetSpan, _context);
                            else SetSubscriptValue(receiver, (PySlice)start, value, targetSpan, _context);
                        }
                        else if (asynchronous) mutation = ExecuteSliceAssignmentAsync(receiver, start, stop, step, value, targetSpan, _context);
                        else ExecuteSliceAssignment(receiver, start, stop, step, value, targetSpan, _context);
                        break;
                    }
                    case ExecutableOpCode.UnpackValues:
                    {
                        var value = _stack.TakeValue(first, instruction.ResultSlot);
                        if (asynchronous)
                            return CompleteTargetUnpack(MaterializeHeaderSequenceAsync(value, span, _context,
                                instruction.UnpackLayout.MaximumReadCount), instruction);
                        using var materialized = MaterializeUnpackingSequence(value, span, _context, instruction.UnpackLayout.MaximumReadCount);
                        PublishUnpackedTarget(materialized.Items, instruction);
                        return default;
                    }
                    case ExecutableOpCode.FailAssertion:
                        ThrowAssertionError(instruction.HasAssertionMessage ? _stack.TakeValue(first, instruction.LiveExtent) : null, _context, span);
                        return default;
                    case ExecutableOpCode.Raise:
                        var cause = instruction.HasRaiseCause ? _stack.TakeValue(first + 1, first + 1) : null;
                        ThrowRaisedValue(_stack.TakeValue(first, instruction.LiveExtent), instruction.HasRaiseCause, cause, _context, span);
                        return default;
                    case ExecutableOpCode.DeleteName:
                        var deletedName = _codeObject.Names[instruction.NameIndex];
                        if (!DeleteName(deletedName, _context, span)) throw MissingDeletedName(deletedName, _context, span);
                        return default;
                    case ExecutableOpCode.DeleteMember:
                    {
                        var receiver = _stack[first];
                        var name = _codeObject.Names[instruction.NameIndex];
                        mutation = CompleteMemberMutation(asynchronous ? PyMemberAccess.TryDeleteAsync(receiver, name, _context, span)
                            : new(PyMemberAccess.TryDelete(receiver, name, _context, span)), receiver, name, span, deleting: true);
                        break;
                    }
                    case ExecutableOpCode.DeleteSubscript:
                        if (asynchronous) mutation = ExecuteResolvedSubscriptDeletionAsync(_stack[first], _stack[first + 1], span, _context);
                        else DeleteSubscriptValue(_stack[first], _stack[first + 1], span, _context);
                        break;
                    case ExecutableOpCode.DeleteSlice:
                    {
                        var (start, stop, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, first + 1);
                        if (asynchronous) mutation = ExecuteSliceDeletionAsync(_stack[first], start, stop, step, span, _context);
                        else ExecuteSliceDeletion(_stack[first], start, stop, step, span, _context);
                        break;
                    }
                    default: throw new InvalidOperationException("Unexpected executable target instruction.");
                }
            }
            catch
            {
                // Only a mutation owns this trim. Retained reads and unpacking
                // have their own publication boundaries; do not retry a failed
                // disposal after transferring it to the completion helper below.
                if (instruction.OpCode is ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast
                    or ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
                    or ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast
                    or ExecutableOpCode.DeleteMember or ExecutableOpCode.DeleteSubscript or ExecutableOpCode.DeleteSlice)
                    _stack.RetireTo(instruction.LiveExtent);
                throw;
            }
            return CompleteTargetMutation(mutation, instruction.LiveExtent);
        }

        private ValueTask CompleteTargetMutation(ValueTask pending, int liveExtent)
        {
            if (!pending.IsCompleted) return AwaitTargetMutationAsync(pending, liveExtent);
            try { pending.GetAwaiter().GetResult(); }
            finally { _stack.RetireTo(liveExtent); }
            return default;
        }

        private async ValueTask AwaitTargetMutationAsync(ValueTask pending, int liveExtent)
        {
            try { await pending.ConfigureAwait(false); }
            finally { _stack.RetireTo(liveExtent); }
        }

        private ValueTask CompleteTargetRead(ValueTask<object> pending, ExecutableInstruction instruction)
        {
            if (!pending.IsCompleted) return AwaitTargetReadAsync(pending, instruction);
            try { _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, pending.GetAwaiter().GetResult()); }
            catch { _stack.RetireTo(instruction.InputStart); throw; }
            return default;
        }

        private async ValueTask AwaitTargetReadAsync(ValueTask<object> pending, ExecutableInstruction instruction)
        {
            try { _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, await pending.ConfigureAwait(false)); }
            catch { _stack.RetireTo(instruction.InputStart); throw; }
        }

        private ValueTask CompleteMemberMutation(ValueTask<bool> pending, object receiver, string name,
            LythonSourceSpan span, bool deleting)
        {
            if (!pending.IsCompleted) return AwaitMemberMutationAsync(pending, receiver, name, span, deleting);
            RequireMemberMutation(pending.GetAwaiter().GetResult(), receiver, name, span, deleting);
            return default;
        }

        private async ValueTask AwaitMemberMutationAsync(ValueTask<bool> pending, object receiver, string name,
            LythonSourceSpan span, bool deleting)
            => RequireMemberMutation(await pending.ConfigureAwait(false), receiver, name, span, deleting);

        private void RequireMemberMutation(bool succeeded, object receiver, string name, LythonSourceSpan span, bool deleting)
        {
            if (succeeded) return;
            if (deleting) throw PyMemberAccess.CreateMissingMemberError(receiver, name, span, _context, operation: MissingMemberOperation.Delete);
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", span);
        }

        private ValueTask CompleteTargetUnpack(ValueTask<PyIteration.DrainLease> pending, ExecutableInstruction instruction)
        {
            if (!pending.IsCompleted) return AwaitTargetUnpackAsync(pending, instruction);
            using var materialized = pending.GetAwaiter().GetResult();
            PublishUnpackedTarget(materialized.Items, instruction);
            return default;
        }

        private async ValueTask AwaitTargetUnpackAsync(ValueTask<PyIteration.DrainLease> pending, ExecutableInstruction instruction)
        {
            using var materialized = await pending.ConfigureAwait(false);
            PublishUnpackedTarget(materialized.Items, instruction);
        }

        private void PublishUnpackedTarget(IReadOnlyList<object> values, ExecutableInstruction instruction)
        {
            var layout = instruction.UnpackLayout;
            if (!layout.AcceptsValueCount(values.Count))
                throw new LythonRuntimeException("ValueError", layout.DescribeArityMismatch(values.Count), instruction.Span);
            for (var i = layout.TargetCount - 1; i >= 0; i--)
            {
                if (i == layout.StarredTargetIndex)
                {
                    var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Count)), _context.MemoryGovernor, instruction.Span);
                    _stack.PublishValue(instruction.ResultSlot + layout.TargetCount - 1 - i, instruction.ResultSlot + layout.TargetCount - i, rest);
                }
                else _stack.PublishValue(instruction.ResultSlot + layout.TargetCount - 1 - i,
                    instruction.ResultSlot + layout.TargetCount - i, values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                        ? layout.SourceIndexForTrailingTarget(i, values.Count) : i]);
            }
        }
    }
}
