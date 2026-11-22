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
            || ReadCapturedCell(cells, load.ClosureSlot) is not { } cell) return false;
        ref readonly var arithmetic = ref code.Instructions[index + 1];
        return cell.TryApplyIntegerArithmetic(AugmentedBinaryOperator(arithmetic.AugmentedOperator),
            (long)(BigInteger)code.Constants[load.ClosureArithmeticConstant]!, context,
            arithmetic.Span, code.Instructions[index + 2].Span);
    }

    // Both executable forms follow the same closed arithmetic sequence. Only the
    // final result is published through the caller's ordinary lifetime machinery.
    private static bool TryIntegerChain(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int index, long left, long right, ExecutionContext scope,
        out int end, out long result)
    {
        end = index;
        result = 0;
        ref readonly var first = ref code.Instructions[index];
        long accumulator;
        if (!TryMachineBinary(first.BinaryOperator, left, right, out accumulator)) return false;
        while (code.Instructions[end].HasIntegerContinuation)
        {
            ref readonly var next = ref code.Instructions[end + 1];
            scope.CheckExecution(next.Span);
            // Look ahead without raising an unbound-local error ahead of
            // an earlier arithmetic error. No guest work has been published.
            var otherSlot = next.InputStart == first.ResultSlot ? next.SecondInput : next.InputStart;
            if (!TryReadMachineOperand(code, values, otherSlot, scope, out var operand)) return false;
            var a = next.InputStart == first.ResultSlot ? accumulator : operand;
            var b = next.SecondInput == first.ResultSlot ? accumulator : operand;
            if (!TryMachineBinary(next.BinaryOperator, a, b, out accumulator)) return false;
            end++;
        }

        // The caller boxes the one published value using existing minor-
        // storage headroom. No object represents intermediate results.
        result = accumulator;
        return true;
    }
}
