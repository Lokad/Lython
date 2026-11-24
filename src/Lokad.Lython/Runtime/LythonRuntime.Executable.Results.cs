using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private void PublishInstructionResult(in ExecutableInstruction instruction, object value)
            => PublishInstructionResult(ref _execution, this, instruction, value);

        private void PublishInstructionResult(in ExecutableInstruction instruction, ExecutableSlot value)
            => PublishInstructionResult(ref _execution, this, instruction, value);

        private static void PublishInstructionResult(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, object value)
            => PublishInstructionResult(ref execution, invocation, instruction, ExecutableSlot.FromObject(value));

        private static void PublishInstructionResult(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, ExecutableSlot value)
        {
            if (!instruction.HasResultConsumer)
            {
                execution.Stack.PublishSlot(instruction.ResultSlot, instruction.LiveExtent, value);
            }
            else CompleteResultConsumer(ref execution, invocation, instruction, value);
        }

        private static void CompleteResultConsumer(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, ExecutableSlot value)
        {
            // Preserve the original input retirement and its disposal order.
            // Pending cleanup must finish before the consumer becomes visible;
            // the unchanged driver and following consumer already implement that.
            if (instruction.LiveExtent < execution.Stack.LiveExtent) execution.Stack.RetireTo(instruction.LiveExtent);
            if (execution.Stack.HasPendingDisposals || !execution.Stack.CanContinue)
            {
                // An interrupted execution reaches the unchanged consumer entry
                // check, outside the producer's operation-exception handling.
                execution.Stack.PublishSlot(instruction.ResultSlot, instruction.LiveExtent, value);
                return;
            }

            execution.Stack.ForgetResultInput(instruction.ResultSlot);
            ref readonly var consumer = ref execution.Code.Instructions[execution.InstructionIndex + 1];
            if (consumer.OpCode == ExecutableOpCode.StoreLocal)
                StoreLocalValue(execution.Code, execution.Locals, invocation, consumer.LocalSlot, value, consumer.Span);
            else if (consumer.OpCode == ExecutableOpCode.StoreFunctionDefault)
                StoreDefaultResult(execution.Stack.Values, consumer, value, execution.Stack.State);
            else if (consumer.OpCode == ExecutableOpCode.StoreClosure)
            {
                if (execution.Captures.GetCell(consumer.ClosureSlot) is not { CanStoreDirectly: true } cell)
                {
                    // Keep the computed value for the normal namespace-aware store.
                    execution.Stack.PublishSlot(instruction.ResultSlot, instruction.LiveExtent, value);
                    return;
                }
                cell.StoreSlot(value);
            }
            else if (consumer.OpCode != ExecutableOpCode.PopTop)
                throw new InvalidOperationException("Invalid prepared result consumer.");
            // Discarded calls cannot return private iteration loans. No
            // publication or scalar materialization is needed for their result.
            else if (value.IsUnassigned) _ = value.Object.RequireNotNull();
            execution.InstructionIndex++;
        }
    }

    // Keep the wide-assignment loop out of both ordinary dispatch loops.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReverseAssignmentValues(Span<ExecutableSlot> values, ExecutionState state, LythonSourceSpan span)
    {
        // RHS evaluation fills the slots left to right. Targets take them off the
        // stack in that same order, and a wide display still checks cancellation.
        for (int left = 0, right = values.Length - 1; left < right; left++, right--)
        {
            if ((left & 255) == 255) state.CheckExecution(span);
            (values[left], values[right]) = (values[right], values[left]);
        }
    }

    private static void StoreDefaultResult(Span<ExecutableSlot> slots,
        in ExecutableInstruction consumer, ExecutableSlot value, ExecutionState state)
        => StoreDefaultResult(ref slots[consumer.InputStart], consumer, value, state);

    private static void StoreDefaultResult(ref ExecutableSlot destination,
        in ExecutableInstruction consumer, ExecutableSlot value, ExecutionState state)
    {
        if (destination.HasPrivatePositionalDefault)
            destination = ExecutableSlot.OwnPrivatePositionalDefault(StoreInlinePositionalDefault(value, state, consumer.Span));
        else if (destination.HasPrivateKeywordDefault)
        {
            state.CheckExecution(consumer.Span);
            destination = ExecutableSlot.OwnPrivateKeywordDefault(RuntimeValue(value.Object));
        }
        else if (destination.Reference is PyTuple positional)
            positional.SetPrivateDefault(consumer.DefaultValueIndex, value, state, consumer.Span);
        else StorePreparedKeywordDefault((object?[])destination.Reference!, consumer.DefaultValueIndex, value, state, consumer.Span);
    }
}
