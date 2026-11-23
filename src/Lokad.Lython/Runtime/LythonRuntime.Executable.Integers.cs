using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool TryEvaluateIntegerInstruction(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int index, ExecutionContext context, out int end, out ExecutableSlot value)
    {
        end = index;
        value = default;
        ref readonly var instruction = ref code.Instructions[index];
        if (instruction.OpCode is not (ExecutableOpCode.Binary or ExecutableOpCode.Augmented)
            || !TryReadMachineOperand(code, values, instruction.InputStart, context, out var left)
            || !TryReadMachineOperand(code, values, instruction.SecondInput, context, out var right)) return false;
        if (instruction.OpCode == ExecutableOpCode.Binary)
        {
            if (IsScalarComparison(instruction.BinaryOperator))
            {
                context.CheckExecution(instruction.Span);
                value.Object = RuntimeValue(CompareOrderedResult(instruction.BinaryOperator, left.CompareTo(right)));
                return true;
            }
            if (instruction.HasIntegerContinuation &&
                TryIntegerChain(code, values, index, left, right, context, out var last, out var chained))
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

    // The completed call's result can feed the already evaluated left operand.
    // Read only physical slots. Overflow and protocol values leave the tape
    // and operands available to the original augmented operation.
    private static bool TryCombineCallAugmentation(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        in ExecutableInstruction call, int index, ExecutableSlot returned, ExecutionState state, out ExecutableSlot result)
    {
        result = default;
        ref readonly var update = ref code.Instructions[index];
        if (update.OpCode != ExecutableOpCode.Augmented || update.SecondInput != call.ResultSlot ||
            (uint)update.InputStart >= (uint)call.ResultSlot ||
            update.AugmentedOperator == AugmentedAssignmentOperatorSyntax.MatrixMultiply || !state.CanContinue ||
            !returned.TryGetInteger(out var right) || !values[update.InputStart].TryGetInteger(out var left) ||
            !TryMachineBinary(AugmentedBinaryOperator(update.AugmentedOperator), left, right, out var integer)) return false;
        result.SetInteger(integer);
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
            return TryReadMachineOperand(Code, Values, instruction.InputStart, _scope, out var left)
                && TryReadMachineOperand(Code, Values, instruction.SecondInput, _scope, out var right)
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

    }
}
