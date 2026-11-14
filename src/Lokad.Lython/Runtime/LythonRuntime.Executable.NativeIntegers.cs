using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // This sentinel exists only in a synchronous native prefix. Its primitive
    // payload is in that activation's borrowed working span, never a guest cell.
    private static readonly object NativeInteger = new();

    private static bool TryReadMachineOperand(ExecutableCodeObject code, ReadOnlySpan<object> values,
        ReadOnlySpan<long> integers, int source, out long value)
    {
        var stored = source < 0 ? code.Constants[~source] : values[source];
        if (ReferenceEquals(stored, NativeInteger))
        {
            value = integers[source];
            return true;
        }
        return TryMachineInteger(stored, out value);
    }

    internal ref partial struct NativeExecution
    {
        private bool TryIntegerOperation(in ExecutableInstruction instruction)
        {
            if (!TryReadMachineOperand(Code, Values, _integers, instruction.InputStart, out var left)
                || !TryReadMachineOperand(Code, Values, _integers, instruction.SecondInput, out var right)) return false;
            if (instruction.OpCode == ExecutableOpCode.Binary)
            {
                if (TryIntegerComparison(instruction.BinaryOperator, left, right, instruction.Span, out var truth))
                { Publish(instruction, RuntimeValue(truth)); return true; }
                if (TryIntegerChain(Code, Values, _integers, Index, left, right, _scope, out var end, out var chained))
                {
                    Index = end;
                    PublishInteger(Code.Instructions[end], chained);
                    return true;
                }
            }
            var operation = instruction.OpCode == ExecutableOpCode.Augmented
                ? AugmentedBinaryOperator(instruction.AugmentedOperator) : instruction.BinaryOperator;
            if (!TryMachineBinary(operation, left, right, out var result)) return false;
            // A declined chain can have inspected a successful prefix, but has
            // neither published it nor advanced the current instruction.
            PublishInteger(instruction, result);
            return true;
        }

        private bool TryIntegerComparison(in ExecutableInstruction instruction, out bool result)
        {
            result = false;
            return TryReadMachineOperand(Code, Values, _integers, instruction.InputStart, out var left)
                && TryReadMachineOperand(Code, Values, _integers, instruction.SecondInput, out var right)
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
            var slot = instruction.HasLocalResult ? Code.Instructions[Index + 1].LocalSlot : instruction.ResultSlot;
            // Publication owns retirement, store checkpoints and PC advancement
            // for both representations. Nothing observes this private marker
            // before its primitive payload is established below.
            Publish(instruction, NativeInteger);
            _integers[slot] = value;
            _hasIntegers = true;
        }

        private object? Materialize(int slot)
        {
            var value = Values[slot];
            if (ReferenceEquals(value, NativeInteger))
            {
                // Cache the immutable object before any alias can observe it.
                // Machine values use the same minor-storage headroom as the
                // ordinary kernel; larger magnitudes retain its major preflight.
                value = (BigInteger)_integers[slot];
                Values[slot] = value;
            }
            return value;
        }

        private void MaterializeRange(int start, int count)
        {
            if (!_hasIntegers) return;
            for (var i = start; i < start + count; i++) _ = Materialize(i);
        }
    }
}
