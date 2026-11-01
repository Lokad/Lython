using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private object ReadInlineInput(ExecutableInstruction load)
        {
            _context.CheckExecution(load.Span);
            return load.OpCode switch
            {
                ExecutableOpCode.LoadLocal => LoadLocal(_codeObject, _locals, load.LocalSlot, load.Span, _context),
                ExecutableOpCode.LoadConst => RuntimeValue(_codeObject.Constants[load.ConstantIndex]),
                _ => throw new InvalidOperationException("Invalid inline input."),
            };
        }

        // Calls and protocol-sensitive operations retain the existing actual
        // slots. A resumed type getter already owns these original inputs.
        private void PrepareInlineInputs(in ExecutableInstruction instruction)
        {
            if (instruction.InputPlan is not { } plan ||
                instruction.OpCode == ExecutableOpCode.Subscript && _preparedTypeSubscription is not null) return;
            foreach (var load in plan.Loads) _stack.Push(ReadInlineInput(load));
        }

        private object ReadArithmeticInput(ExecutableInputPlan? plan, int stackInputs, int first, int index)
            => index < stackInputs ? _stack[first + index] : ReadInlineInput(plan!.Loads[index - stackInputs]);

        // Value temporaries must not remain in the long-running driver after
        // their last use. Suspended operations retain their actual arguments.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ValueTask ExecuteArithmeticInputsAsync(ExecutableInstruction instruction, bool asynchronous)
        {
            var plan = instruction.InputPlan;
            var count = instruction.OpCode == ExecutableOpCode.Unary ? 1 : 2;
            var stackInputs = count - (plan?.Loads.Length ?? 0);
            var first = _stack.Count - stackInputs;
            try
            {
                var left = ReadArithmeticInput(plan, stackInputs, first, 0);
                var right = count == 2 ? ReadArithmeticInput(plan, stackInputs, first, 1) : PyNone.Instance;
                if (stackInputs == 0 && (plan?.DestinationSlot ?? -1) < 0)
                    _stack.ReservePushCapacity(); // before any callback/result allocation
                var native = count == 1 ? left is BigInteger or double or bool :
                    (left is BigInteger or double or bool && right is BigInteger or double or bool) ||
                    (left is PyString && right is PyString);
                if (asynchronous && !native)
                    return CompleteArithmeticInputsAsync(instruction, left, right, first, stackInputs);
                var result = instruction.OpCode switch
                {
                    ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator, left, right, _context, instruction.Span),
                    ExecutableOpCode.Augmented => EvaluateExecutableAugmented(instruction.AugmentedOperator, left, right, _context, instruction.Span),
                    ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, left, _context, instruction.Span),
                    _ => throw new InvalidOperationException("Invalid arithmetic input operation."),
                };
                CompleteArithmeticInputs(instruction, first, stackInputs, result);
                return ValueTask.CompletedTask;
            }
            catch { _stack.RemoveTail(_stack.Count - first); throw; }
        }

        private async ValueTask CompleteArithmeticInputsAsync(ExecutableInstruction instruction, object left, object right,
            int first, int stackInputs)
        {
            try
            {
                var result = instruction.OpCode switch
                {
                    ExecutableOpCode.Binary => await EvaluateBinaryOperatorAsync(instruction.BinaryOperator, left, right, _context, instruction.Span).ConfigureAwait(false),
                    ExecutableOpCode.Augmented => await EvaluateAugmentedAssignmentAsync(left, right, MapExecutableAugmented(instruction.AugmentedOperator), _context, instruction.Span).ConfigureAwait(false),
                    ExecutableOpCode.Unary => await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), left, _context, instruction.Span).ConfigureAwait(false),
                    _ => throw new InvalidOperationException("Invalid arithmetic input operation."),
                };
                CompleteArithmeticInputs(instruction, first, stackInputs, result);
            }
            catch { _stack.RemoveTail(_stack.Count - first); throw; }
        }

        private void CompleteArithmeticInputs(ExecutableInstruction instruction, int first, int stackInputs, object result)
        {
            if (instruction.InputPlan is { DestinationSlot: >= 0 } plan)
            {
                _stack.RemoveTail(stackInputs);
                _context.CheckExecution(plan.DestinationSpan);
                StoreLocalValue(_codeObject, _locals, _localCells, _context, plan.DestinationSlot, result, plan.DestinationSpan);
            }
            else if (stackInputs != 0) CompleteExecutableValueTransfer(ref _stack, first, result);
            else _stack.PushAdmitted(result);
        }
    }
}
