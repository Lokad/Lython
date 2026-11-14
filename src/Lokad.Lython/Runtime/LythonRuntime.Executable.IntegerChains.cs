using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Both executable forms follow the same closed arithmetic sequence. Only the
    // final result is published through the caller's ordinary lifetime machinery.
    private static bool TryIntegerChain(ExecutableCodeObject code, ReadOnlySpan<object> values,
        int index, object left, object right, ExecutionContext scope, out int end, out long result)
    {
        end = index;
        result = 0;
        ref readonly var first = ref code.Instructions[index];
        if (first.HasLocalResult || index + 1 >= code.Instructions.Length
            || !ContinuesIntegerChain(code, code.Instructions[index + 1], first.ResultSlot)
            || !TryMachineInteger(left, out var a) || !TryMachineInteger(right, out var b)) return false;

        long accumulator;
        try
        {
            if (!TryMachineBinary(first.BinaryOperator, a, b, out accumulator)) return false;
            while (end + 1 < code.Instructions.Length
                && ContinuesIntegerChain(code, code.Instructions[end + 1], first.ResultSlot))
            {
                ref readonly var next = ref code.Instructions[end + 1];
                scope.CheckExecution(next.Span);
                // Look ahead without raising an unbound-local error ahead of
                // an earlier arithmetic error. No guest work has been published.
                var otherSlot = next.InputStart == first.ResultSlot ? next.SecondInput : next.InputStart;
                var other = otherSlot < 0 ? code.Constants[~otherSlot] : values[otherSlot];
                if (!TryMachineInteger(other, out var operand)) return false;
                a = next.InputStart == first.ResultSlot ? accumulator : operand;
                b = next.SecondInput == first.ResultSlot ? accumulator : operand;
                if (!TryMachineBinary(next.BinaryOperator, a, b, out accumulator)) return false;
                end++;
                if (next.HasLocalResult) break;
            }
        }
        catch (OverflowException)
        {
            // Exact promotion and major-storage admission remain in the shared
            // integer kernel. No admission was attempted and no value escaped.
            return false;
        }

        // The caller boxes the one published value using existing minor-
        // storage headroom. No object represents intermediate results.
        result = accumulator;
        return true;
    }

    private static bool ContinuesIntegerChain(ExecutableCodeObject code, in ExecutableInstruction next, int slot)
        => next.OpCode == ExecutableOpCode.Binary && next.ResultSlot == slot
            && (next.InputStart == slot && next.SecondInput < code.FirstTemporarySlot
                || next.SecondInput == slot && next.InputStart < code.FirstTemporarySlot);

    private static bool TryMachineInteger(object? value, out long result)
    {
        if (value is BigInteger integer && integer.GetByteCount() <= sizeof(long))
        {
            result = (long)integer;
            return true;
        }
        result = 0;
        return false;
    }

    private static bool TryMachineBinary(BinaryOperatorSyntax op, long left, long right, out long result)
    {
        result = 0;
        checked
        {
            switch (op)
            {
                case BinaryOperatorSyntax.Add: result = left + right; return true;
                case BinaryOperatorSyntax.Subtract: result = left - right; return true;
                case BinaryOperatorSyntax.Multiply: result = left * right; return true;
                case BinaryOperatorSyntax.FloorDivide when right != 0:
                    result = Math.DivRem(left, right, out var remainder);
                    if (remainder != 0 && (left < 0) != (right < 0)) result--;
                    return true;
                case BinaryOperatorSyntax.Modulo when right != 0:
                    result = left % right;
                    if (result != 0 && (left < 0) != (right < 0)) result += right;
                    return true;
                default: return false;
            }
        }
    }
}
