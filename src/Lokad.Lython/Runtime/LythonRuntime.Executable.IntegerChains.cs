using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool TryExecuteClosureArithmetic(ExecutableCodeObject code, IReadOnlyList<ExecutableCell>? cells,
        int index, int liveExtent, ExecutionContext context)
    {
        ref readonly var load = ref code.Instructions[index];
        // A clean statement extent has no input retirement or pending user.
        if (!load.HasClosureArithmetic || liveExtent != load.ResultSlot
            || cells is null || (uint)load.ClosureSlot >= (uint)cells.Count) return false;
        ref readonly var arithmetic = ref code.Instructions[index + 1];
        return cells[load.ClosureSlot].TryApplyIntegerArithmetic(AugmentedBinaryOperator(arithmetic.AugmentedOperator),
            (long)(BigInteger)code.Constants[load.ClosureArithmeticConstant]!, context,
            arithmetic.Span, code.Instructions[index + 2].Span);
    }

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

        // The caller boxes the one published value using existing minor-
        // storage headroom. No object represents intermediate results.
        result = accumulator;
        return true;
    }

    private static bool ContinuesIntegerChain(ExecutableCodeObject code, in ExecutableInstruction next, int slot)
        => next.OpCode == ExecutableOpCode.Binary && next.ResultSlot == slot
            && (next.InputStart == slot && next.SecondInput < code.FirstTemporarySlot
                || next.SecondInput == slot && next.InputStart < code.FirstTemporarySlot);
}
