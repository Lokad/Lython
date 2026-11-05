using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Keep computed object temporaries out of the long dispatch loop, just
        // like ordinary stores. Inputs remain owned until operator/store finish.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ValueTask ExecuteLocalAugmented(in ExecutableInstruction instruction, bool asynchronous)
        {
            try
            {
                var left = ReadOperand(instruction.InputStart);
                var right = ReadOperand(instruction.SecondInput);
                if (!asynchronous || (left is BigInteger or double or bool && right is BigInteger or double or bool)
                    || (left is PyString && right is PyString))
                {
                    CommitLocalAugmented(instruction, EvaluateAugmentedAssignment(left, right,
                        instruction.LocalAugmentedOperator, _context, instruction.Span));
                    return default;
                }

                var pending = EvaluateAugmentedAssignmentAsync(left, right,
                    instruction.LocalAugmentedOperator, _context, instruction.Span);
                if (!pending.IsCompleted) return AwaitLocalAugmented(pending, instruction);
                CommitLocalAugmented(instruction, pending.GetAwaiter().GetResult());
                return default;
            }
            catch { _stack.RetireTo(instruction.LiveExtent); throw; }
        }

        private void CommitLocalAugmented(in ExecutableInstruction instruction, object value)
        {
            // A callback can suspend or refuse admission. Do not publish its
            // result into the binding after cancellation or terminal refusal.
            _context.CheckExecution(instruction.Span);
            StoreLocalValue(_codeObject, _locals, _localCells, _context,
                instruction.LocalSlot, value, instruction.Span);
            _stack.RetireTo(instruction.LiveExtent);
        }

        private async ValueTask AwaitLocalAugmented(ValueTask<object> pending, ExecutableInstruction instruction)
        {
            try { CommitLocalAugmented(instruction, await pending.ConfigureAwait(false)); }
            catch { _stack.RetireTo(instruction.LiveExtent); throw; }
        }
    }
}
