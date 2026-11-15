using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool TryReadMachineOperand(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int source, out long value)
        => source < 0 ? TryMachineInteger(code.Constants[~source], out value) : values[source].TryGetInteger(out value);

    private static bool TryEvaluateIntegerInstruction(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int index, ExecutionContext context, out int end, out ExecutableSlot value)
    {
        end = index;
        value = default;
        ref readonly var instruction = ref code.Instructions[index];
        if (instruction.OpCode is not (ExecutableOpCode.Binary or ExecutableOpCode.Augmented)
            || !TryReadMachineOperand(code, values, instruction.InputStart, out var left)
            || !TryReadMachineOperand(code, values, instruction.SecondInput, out var right)) return false;
        if (instruction.OpCode == ExecutableOpCode.Binary)
        {
            if (IsScalarComparison(instruction.BinaryOperator))
            {
                context.CheckExecution(instruction.Span);
                value.Object = RuntimeValue(CompareOrderedResult(instruction.BinaryOperator, left.CompareTo(right)));
                return true;
            }
            if (TryIntegerChain(code, values, index, left, right, context, out var last, out var chained))
            {
                end = last;
                value.SetInteger(chained);
                return true;
            }
        }
        // Matrix assignment must reach the Python protocol/error path, just
        // as it did before closed integer operations shared this dispatcher.
        if (instruction.OpCode == ExecutableOpCode.Augmented
            && instruction.AugmentedOperator == AugmentedAssignmentOperatorSyntax.MatrixMultiply) return false;
        var operation = instruction.OpCode == ExecutableOpCode.Augmented
            ? AugmentedBinaryOperator(instruction.AugmentedOperator) : instruction.BinaryOperator;
        if (!TryMachineBinary(operation, left, right, out var result)) return false;
        value.SetInteger(result);
        return true;
    }

    internal ref partial struct NativeExecution
    {
        private bool TryIntegerOperation()
        {
            if (!TryEvaluateIntegerInstruction(Code, Values, Index, _scope, out var end, out var result)) return false;
            Index = end;
            Publish(Code.Instructions[end], result);
            return true;
        }

        private bool TryIntegerComparison(in ExecutableInstruction instruction, out bool result)
        {
            result = false;
            return TryReadMachineOperand(Code, Values, instruction.InputStart, out var left)
                && TryReadMachineOperand(Code, Values, instruction.SecondInput, out var right)
                && TryIntegerComparison(instruction.BinaryOperator, left, right, instruction.Span, out result);
        }

        private bool TryIntegerComparison(BinaryOperatorSyntax operation, long left, long right,
            LythonSourceSpan span, out bool result)
        {
            result = false;
            if (!IsScalarComparison(operation)) return false;
            _scope.CheckExecution(span);
            result = CompareOrderedResult(operation, left.CompareTo(right));
            return true;
        }

        private void PublishInteger(in ExecutableInstruction instruction, long value)
        {
            var slot = default(ExecutableSlot);
            slot.SetInteger(value);
            Publish(instruction, slot);
        }

        private object? Materialize(int slot) => Values[slot].Object;
    }
}
