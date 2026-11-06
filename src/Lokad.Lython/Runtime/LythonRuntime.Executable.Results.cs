using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private void PublishInstructionResult(in ExecutableInstruction instruction, object value)
        {
            if (!instruction.HasLocalResult)
            {
                _execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
            }
            else CompleteLocalResult(instruction, value);
        }

        // Keep computed guest temporaries out of the long dispatch loop, as
        // ordinary stores do. Unchanged operand publication needs no new fence.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CompleteLocalResult(in ExecutableInstruction instruction, object value)
        {
            // Preserve the original input retirement and its disposal order.
            // Pending cleanup must finish before the binding becomes visible;
            // the unchanged driver and following store already implement that.
            if (instruction.LiveExtent < _execution.Stack.LiveExtent) _execution.Stack.RetireTo(instruction.LiveExtent);
            if (_execution.Stack.HasPendingDisposals)
            {
                _execution.Stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
                return;
            }

            _execution.Stack.ForgetResultInput(instruction.ResultSlot);
            ref readonly var store = ref _execution.Code.Instructions[_execution.InstructionIndex + 1];
            CheckExecution(store.Span);
            StoreLocalValue(_execution.Code, _execution.Locals, _execution.LocalCells, this, store.LocalSlot, value, store.Span);
            _execution.InstructionIndex++;
        }
    }
}
