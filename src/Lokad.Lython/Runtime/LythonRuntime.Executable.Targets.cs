using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
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
                        var receiver = _execution.Stack[first + (instruction.ValueLast ? 0 : 1)];
                        var value = _execution.Stack[first + (instruction.ValueLast ? 1 : 0)];
                        var name = _execution.Code.Names[instruction.NameIndex];
                        var targetSpan = (LythonSourceSpan)_execution.Code.Constants[instruction.TargetSpanIndex]!;
                        if (asynchronous)
                            mutation = CompleteMemberMutation(PyMemberAccess.TryAssignAsync(receiver, name, value, this, targetSpan),
                                receiver, name, targetSpan, deleting: false);
                        else SetMemberValue(receiver, name, value, targetSpan, this);
                        break;
                    }
                    case ExecutableOpCode.ReadRetainedSubscript:
                    {
                        ValueTask<object> read;
                        try
                        {
                            read = asynchronous
                                ? ReadLoweredSubscriptAsync(_execution.Stack[first], _execution.Stack[first + 1], span, this)
                                : new(ReadSubscriptValue(_execution.Stack[first], _execution.Stack[first + 1], span, this));
                        }
                        catch { _execution.Stack.RetireTo(first); throw; }
                        return CompleteTargetRead(read, instruction);
                    }
                    case ExecutableOpCode.ReadRetainedSlice:
                    {
                        ValueTask<object> read;
                        try
                        {
                            var receiver = _execution.Stack[first];
                            var start = _execution.Stack[first + 1];
                            var stop = _execution.Stack[first + 2];
                            var step = _execution.Stack[first + 3];
                            if (receiver is PyInstance instance)
                            {
                                var key = CreateSliceValue(start, stop, step, span, this);
                                // Retain this exact key through RHS and augmented store.
                                _execution.Stack[first + 1] = key;
                                read = asynchronous ? GetUserItemAsync(instance, key, this, span)
                                    : new(GetUserItem(instance, key, this, span));
                            }
                            else read = asynchronous ? ReadSliceValueAsync(receiver, start, stop, step, span, this)
                                : new(ReadSliceValue(receiver, start, stop, step, span, this));
                        }
                        catch { _execution.Stack.RetireTo(first); throw; }
                        return CompleteTargetRead(read, instruction);
                    }
                    case ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast:
                    {
                        var receiverIndex = first + (instruction.ValueLast ? 0 : 1);
                        var receiver = _execution.Stack[receiverIndex];
                        var start = _execution.Stack[receiverIndex + 1];
                        var stop = _execution.Stack[receiverIndex + 2];
                        var step = _execution.Stack[receiverIndex + 3];
                        var value = _execution.Stack[first + (instruction.ValueLast ? 4 : 0)];
                        var targetSpan = (LythonSourceSpan)_execution.Code.Constants[instruction.TargetSpanIndex]!;
                        if (instruction.ValueLast && receiver is PyInstance)
                        {
                            if (asynchronous) mutation = SetHeaderSubscriptAsync(receiver, (PySlice)start, value, targetSpan, this);
                            else SetSubscriptValue(receiver, (PySlice)start, value, targetSpan, this);
                        }
                        else if (asynchronous) mutation = ExecuteSliceAssignmentAsync(receiver, start, stop, step, value, targetSpan, this);
                        else ExecuteSliceAssignment(receiver, start, stop, step, value, targetSpan, this);
                        break;
                    }
                    case ExecutableOpCode.UnpackValues:
                    {
                        var value = _execution.Stack.TakeValue(first, instruction.ResultSlot);
                        if (asynchronous)
                            return CompleteTargetUnpack(MaterializeHeaderSequenceAsync(value, span, this,
                                instruction.UnpackLayout.MaximumReadCount), instruction);
                        using var materialized = MaterializeUnpackingSequence(value, span, this, instruction.UnpackLayout.MaximumReadCount);
                        PublishUnpackedTarget(materialized.Items, instruction);
                        return default;
                    }
                    case ExecutableOpCode.FailAssertion:
                        ThrowAssertionError(instruction.HasAssertionMessage ? _execution.Stack.TakeValue(first, instruction.LiveExtent) : null, this, span);
                        return default;
                    case ExecutableOpCode.Raise:
                        var cause = instruction.HasRaiseCause ? _execution.Stack.TakeValue(first + 1, first + 1) : null;
                        ThrowRaisedValue(_execution.Stack.TakeValue(first, instruction.LiveExtent), instruction.HasRaiseCause, cause, this, span);
                        return default;
                    case ExecutableOpCode.DeleteName:
                        var deletedName = _execution.Code.Names[instruction.NameIndex];
                        if (!DeleteName(deletedName, this, span)) throw MissingDeletedName(deletedName, this, span);
                        return default;
                    case ExecutableOpCode.DeleteMember:
                    {
                        var receiver = _execution.Stack[first];
                        var name = _execution.Code.Names[instruction.NameIndex];
                        mutation = CompleteMemberMutation(asynchronous ? PyMemberAccess.TryDeleteAsync(receiver, name, this, span)
                            : new(PyMemberAccess.TryDelete(receiver, name, this, span)), receiver, name, span, deleting: true);
                        break;
                    }
                    case ExecutableOpCode.DeleteSubscript:
                        if (asynchronous) mutation = ExecuteResolvedSubscriptDeletionAsync(_execution.Stack[first], _execution.Stack[first + 1], span, this);
                        else DeleteSubscriptValue(_execution.Stack[first], _execution.Stack[first + 1], span, this);
                        break;
                    case ExecutableOpCode.DeleteSlice:
                    {
                        var (start, stop, step) = ReadExecutableSliceBounds(in _execution.Stack, instruction.SliceParts, first + 1);
                        if (asynchronous) mutation = ExecuteSliceDeletionAsync(_execution.Stack[first], start, stop, step, span, this);
                        else ExecuteSliceDeletion(_execution.Stack[first], start, stop, step, span, this);
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
                    _execution.Stack.RetireTo(instruction.LiveExtent);
                throw;
            }
            return CompleteTargetMutation(mutation, instruction.LiveExtent);
        }

        private ValueTask CompleteTargetMutation(ValueTask pending, int liveExtent)
        {
            if (!pending.IsCompleted) return AwaitTargetMutationAsync(pending, liveExtent);
            try { pending.GetAwaiter().GetResult(); }
            finally { _execution.Stack.RetireTo(liveExtent); }
            return default;
        }

        private async ValueTask AwaitTargetMutationAsync(ValueTask pending, int liveExtent)
        {
            try { await pending.ConfigureAwait(false); }
            finally { _execution.Stack.RetireTo(liveExtent); }
        }

        private ValueTask CompleteTargetRead(ValueTask<object> pending, ExecutableInstruction instruction)
        {
            if (!pending.IsCompleted) return AwaitTargetReadAsync(pending, instruction);
            try { _execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, pending.GetAwaiter().GetResult()); }
            catch { _execution.Stack.RetireTo(instruction.InputStart); throw; }
            return default;
        }

        private async ValueTask AwaitTargetReadAsync(ValueTask<object> pending, ExecutableInstruction instruction)
        {
            try { _execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, await pending.ConfigureAwait(false)); }
            catch { _execution.Stack.RetireTo(instruction.InputStart); throw; }
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
            if (deleting) throw PyMemberAccess.CreateMissingMemberError(receiver, name, span, this, operation: MissingMemberOperation.Delete);
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
                    var rest = new PyList(values.Skip(i).Take(layout.StarredValueCount(values.Count)), MemoryGovernor, instruction.Span);
                    _execution.Stack.PublishValue(instruction.ResultSlot + layout.TargetCount - 1 - i, instruction.ResultSlot + layout.TargetCount - i, rest);
                }
                else _execution.Stack.PublishValue(instruction.ResultSlot + layout.TargetCount - 1 - i,
                    instruction.ResultSlot + layout.TargetCount - i, values[layout.HasStarredTarget && i > layout.StarredTargetIndex
                        ? layout.SourceIndexForTrailingTarget(i, values.Count) : i]);
            }
        }
    }
}
