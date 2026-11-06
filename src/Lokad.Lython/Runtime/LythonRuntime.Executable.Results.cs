using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private void PublishInstructionResult(in ExecutableInstruction instruction, object value)
            => PublishInstructionResult(ref _execution, this, this, instruction, value);

        private static void PublishInstructionResult(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction, object value)
        {
            if (!instruction.HasLocalResult)
            {
                execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
            }
            else CompleteLocalResult(ref execution, scope, invocation, instruction, value);
        }

        // Keep computed guest temporaries out of the long dispatch loop, as
        // ordinary stores do. Unchanged operand publication needs no new fence.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CompleteLocalResult(ref ExecutableWorkState execution, ExecutionContext scope,
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
            scope.CheckExecution(store.Span);
            StoreLocalValue(execution.Code, execution.Locals, execution.LocalCells, invocation, store.LocalSlot, value, store.Span);
            execution.InstructionIndex++;
        }
    }
}
