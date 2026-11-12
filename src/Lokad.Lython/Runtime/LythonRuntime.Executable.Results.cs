using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private void PublishInstructionResult(in ExecutableInstruction instruction, object value)
            => PublishInstructionResult(ref _execution, this, instruction, value);

        private static void PublishInstructionResult(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, object value)
        {
            if (!instruction.HasLocalResult)
            {
                execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
            }
            else CompleteLocalResult(ref execution, invocation, instruction, value);
        }

        private static void CompleteLocalResult(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, object value)
        {
            // Preserve the original input retirement and its disposal order.
            // Pending cleanup must finish before the binding becomes visible;
            // the unchanged driver and following store already implement that.
            if (instruction.LiveExtent < execution.Stack.LiveExtent) execution.Stack.RetireTo(instruction.LiveExtent);
            if (execution.Stack.HasPendingDisposals)
            {
                execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
                return;
            }

            execution.Stack.ForgetResultInput(instruction.ResultSlot);
            ref readonly var store = ref execution.Code.Instructions[execution.InstructionIndex + 1];
            execution.Stack.CheckExecution(store.Span);
            StoreLocalValue(execution.Code, execution.Locals, invocation, store.LocalSlot, value, store.Span);
            execution.InstructionIndex++;
        }
    }
}
