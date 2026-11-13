using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal ref partial struct NativeExecution
    {
        // Follow the existing instructions, retaining only an unobservable temporary.
        // Stores, calls and control flow still publish ordinary Python objects.
        private bool TryIntegerChain(in ExecutableInstruction first, object left, object right)
        {
            if (first.HasLocalResult || Index + 1 >= Code.Instructions.Length
                || !ContinuesIntegerChain(Code.Instructions[Index + 1], first.ResultSlot)
                || !TryMachineInteger(left, out var a) || !TryMachineInteger(right, out var b)) return false;

            var end = Index;
            long accumulator;
            try
            {
                if (!TryMachineBinary(first.BinaryOperator, a, b, out accumulator)) return false;
                while (end + 1 < Code.Instructions.Length
                    && ContinuesIntegerChain(Code.Instructions[end + 1], first.ResultSlot))
                {
                    ref readonly var next = ref Code.Instructions[end + 1];
                    _scope.CheckExecution(next.Span);
                    // Look ahead without raising an unbound-local error ahead of
                    // an earlier arithmetic error. No guest work has been published.
                    var otherSlot = next.InputStart == first.ResultSlot ? next.SecondInput : next.InputStart;
                    var other = otherSlot < 0 ? Code.Constants[~otherSlot] : Values[otherSlot];
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

            Index = end;
            // Fixed-size values use the existing minor-storage headroom. Only this
            // final value is boxed; no CLR object represents the intermediate results.
            Publish(Code.Instructions[end], (BigInteger)accumulator);
            return true;
        }

        private bool ContinuesIntegerChain(in ExecutableInstruction next, int slot)
            => next.OpCode == ExecutableOpCode.Binary && next.ResultSlot == slot
                && (next.InputStart == slot && next.SecondInput < Code.FirstTemporarySlot
                    || next.SecondInput == slot && next.InputStart < Code.FirstTemporarySlot);

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
}
