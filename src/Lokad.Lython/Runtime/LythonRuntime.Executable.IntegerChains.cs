using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static bool TryEvaluateDirectOperandReturn<T>(ExecutableCodeObject code, T operands,
        ExecutionContext scope, ExecutableCaptures captures, out ExecutableSlot value)
        where T : IIntegerOperands, allows ref struct
    {
        value = default;
        var start = 0;
        ref readonly var head = ref code.Instructions[0];
        if (head.OpCode == ExecutableOpCode.JumpIfFalse)
        {
            scope.CheckExecution(head.Span);
            if (ReadDirectInput(code, operands, captures, head.InputStart).Reference is not bool condition) return false;
            if (condition) scope.CheckExecution(code.Instructions[1].Span);
            start = condition ? code.Instructions[1].TargetIndex : head.TargetIndex;
        }
        ref readonly var first = ref code.Instructions[start];
        scope.CheckExecution(first.Span);
        if (first.OpCode == ExecutableOpCode.Return)
        {
            // Return an existing value without creating a callee workspace.
            // Preserve its identity, including the owner of a borrowed integer.
            value = ReadDirectInput(code, operands, captures, first.InputStart);
            return value.HasMaterializedValue || value.TryGetInteger(out _);
        }
        if (!operands.TryRead(code, first.InputStart, scope, out var left) ||
            !operands.TryRead(code, first.SecondInput, scope, out var right) ||
            !TryIntegerChain(code, operands, start, left, right, scope, out var end, out var result)) return false;
        System.Diagnostics.Debug.Assert(code.Instructions[end + 1].OpCode == ExecutableOpCode.Return);
        scope.CheckExecution(code.Instructions[end + 1].Span);
        value.SetInteger(result);
        return true;
    }

    private static ExecutableSlot ReadDirectInput<T>(ExecutableCodeObject code, T operands, ExecutableCaptures captures, int input)
        where T : IIntegerOperands, allows ref struct
    {
        if (input >= 0) return operands.ReadLocal(input);
        var index = ~input;
        if (index < code.Constants.Length) return ExecutableSlot.FromObject(RuntimeValue(code.Constants[index]));
        return captures.TryRead(index - code.CaptureOperandBase, out var value) ? ExecutableSlot.FromObject(value) : default;
    }

    internal interface IIntegerOperands
    {
        bool TryRead(ExecutableCodeObject code, int slot, ExecutionContext scope, out long value);
        ExecutableSlot ReadLocal(int slot);
    }

    internal readonly ref struct SlotIntegerOperands(Span<ExecutableSlot> slots, ExecutableCaptures captures,
        bool borrowedArgument = false) : IIntegerOperands
    {
        private readonly Span<ExecutableSlot> _slots = slots;

        public bool TryRead(ExecutableCodeObject code, int slot, ExecutionContext scope, out long value)
            => TryReadMachineOperand(code, _slots, slot, scope, out value, captures);

        public ExecutableSlot ReadLocal(int slot) => borrowedArgument ? ExecutableSlot.FromObject(_slots[slot].Object) : _slots[slot];
    }

    internal readonly ref struct BoundIntegerOperands(object receiver, ReadOnlySpan<ExecutableSlot> arguments, ExecutableCaptures captures) : IIntegerOperands
    {
        private readonly ReadOnlySpan<ExecutableSlot> _arguments = arguments;

        public bool TryRead(ExecutableCodeObject code, int slot, ExecutionContext scope, out long value)
            => slot == 0 ? TryMachineInteger(receiver, out value)
                : TryReadMachineOperand(code, _arguments, slot > 0 ? slot - 1 : slot, scope, out value, captures);

        public ExecutableSlot ReadLocal(int slot) => slot == 0 ? ExecutableSlot.FromObject(receiver) : _arguments[slot - 1];
    }

    internal readonly ref struct CallbackIntegerOperands(ReadOnlySpan<CallArgumentValue> arguments, object? receiver,
        ExecutableCaptures captures) : IIntegerOperands
    {
        private readonly ReadOnlySpan<CallArgumentValue> _arguments = arguments;

        public bool TryRead(ExecutableCodeObject code, int slot, ExecutionContext scope, out long value)
            => slot >= 0 ? TryMachineInteger(Argument(slot), out value)
                : TryReadMachineOperand(code, default, slot, scope, out value, captures);

        public ExecutableSlot ReadLocal(int slot) => ExecutableSlot.FromObject(Argument(slot));

        private object Argument(int slot) => receiver is null ? _arguments[slot].Value
            : slot == 0 ? receiver : _arguments[slot - 1].Value;
    }

    internal readonly ref struct DefaultIntegerOperands<T>(T supplied, int suppliedCount, FunctionBindingPlan plan)
        : IIntegerOperands where T : IIntegerOperands, allows ref struct
    {
        private readonly T _supplied = supplied;

        public bool TryRead(ExecutableCodeObject code, int slot, ExecutionContext scope, out long value)
            => slot >= suppliedCount
                ? plan.TryReadPreparedDefaultInteger(slot, out value)
                : _supplied.TryRead(code, slot, scope, out value);

        public ExecutableSlot ReadLocal(int slot)
            => slot >= suppliedCount ? plan.ReadPreparedDefault(slot) : _supplied.ReadLocal(slot);
    }

    private static bool TryExecuteClosureArithmetic(ExecutableCodeObject code, ExecutableCaptures cells,
        int index, int liveExtent, ExecutionContext context)
    {
        ref readonly var load = ref code.Instructions[index];
        // A clean statement extent has no input retirement or pending user.
        if (!load.HasClosureArithmetic || liveExtent != load.ResultSlot
            || cells.GetCell(load.ClosureSlot) is not { } cell) return false;
        ref readonly var arithmetic = ref code.Instructions[index + 1];
        return cell.TryApplyIntegerArithmetic(AugmentedBinaryOperator(arithmetic.AugmentedOperator),
            (long)(BigInteger)code.Constants[load.ClosureArithmeticConstant]!, context,
            arithmetic.Span, code.Instructions[index + 2].Span);
    }

    // Both executable forms follow the same closed arithmetic sequence. Only the
    // final result is published through the caller's ordinary lifetime machinery.
    private static bool TryIntegerChain(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int index, long left, long right, ExecutionContext scope,
        out int end, out long result, ExecutableCaptures captures)
        => TryIntegerChain(code, new SlotIntegerOperands(values, captures), index, left, right, scope, out end, out result);

    private static bool TryIntegerChain<T>(ExecutableCodeObject code, T operands,
        int index, long left, long right, ExecutionContext scope, out int end, out long result)
        where T : IIntegerOperands, allows ref struct
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
            if (!operands.TryRead(code, otherSlot, scope, out var operand)) return false;
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
