using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool TryEvaluateIntegerInstruction(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int index, ExecutionContext context, out int end, out ExecutableSlot value, ExecutableCaptures captures)
    {
        end = index;
        value = default;
        ref readonly var instruction = ref code.Instructions[index];
        if (instruction.OpCode is not (ExecutableOpCode.Binary or ExecutableOpCode.Augmented)
            || !TryReadMachineOperand(code, values, instruction.InputStart, context, out var left, captures)
            || !TryReadMachineOperand(code, values, instruction.SecondInput, context, out var right, captures)) return false;
        if (instruction.OpCode == ExecutableOpCode.Binary)
        {
            if (IsScalarComparison(instruction.BinaryOperator))
            {
                context.CheckExecution(instruction.Span);
                value.Object = RuntimeValue(CompareOrderedResult(instruction.BinaryOperator, left.CompareTo(right)));
                return true;
            }
            if (instruction.HasIntegerContinuation &&
                TryIntegerChain(code, values, index, left, right, context, out var last, out var chained, captures))
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryEvaluateIntegerCallUpdate(ExecutableCodeObject code, Span<ExecutableSlot> values,
        ref int index, int liveExtent, ExecutionContext scope, out ExecutableSlot result)
    {
        result = default;
        ref readonly var load = ref code.Instructions[index];
        if (liveExtent != load.ResultSlot || !values[load.LocalSlot].TryGetInteger(out var previous)) return false;

        var current = index + 1;
        var callIndex = load.IntegerUpdateCallIndex;
        ref readonly var target = ref code.Instructions[current];
        object? callable;
        scope.CheckExecution(target.Span);
        if (target.OpCode == ExecutableOpCode.LoadLocal) callable = values[target.LocalSlot].Reference;
        else if (!TryResolveExecutableGlobal(code, target.NameIndex, scope, out callable)) return false;
        var call = code.CallSites[code.Instructions[callIndex].CallSiteIndex];
        if (callable is not PyExecutableFunction { CodeObject.HasDirectOperandReturn: true } function ||
            !function.TrySelectNativeCall(call, null, out var complete) ||
            !complete && !function.HasPreparedOmittedArguments(call.ArgumentCount)) return false;

        var start = target.LiveExtent;
        var arguments = values.Slice(start, call.ArgumentCount);
        try
        {
            for (current++; current < callIndex; current++)
            {
                ref readonly var argument = ref code.Instructions[current];
                scope.CheckExecution(argument.Span);
                long value;
                if (argument.OpCode == ExecutableOpCode.Binary)
                {
                    if (!TryEvaluatePreparedBinary(code, values, argument, scope, out value)) return false;
                }
                else
                {
                    var source = argument.OpCode == ExecutableOpCode.LoadLocal ? argument.LocalSlot : ~argument.ConstantIndex;
                    if (!TryReadMachineOperand(code, values, source, scope, out value)) return false;
                }
                values[argument.ResultSlot].SetInteger(value);
            }

            // Nothing in the arguments or the selected body can publish
            // their identity or mutate the early source slots. If an exact
            // operation declines, the ordinary tape still has those values.
            scope.CheckExecution(code.Instructions[callIndex].Span);
            scope.EnterInterpreterFrame(call.CallSpan);
            ExecutableSlot returned;
            try
            {
                var completed = complete
                    ? function.TryStartSuppliedOperandReturn(arguments, null, call.CallSpan, out returned)
                    : function.TryStartPreparedOperandReturn(call, arguments, scope, call.CallSpan, out returned);
                if (!completed) return false;
            }
            finally { scope.LeaveInterpreterFrame(); }
            current++;
            scope.CheckExecution(code.Instructions[current].Span);
            if (!returned.TryGetInteger(out var adjustment) ||
                !TryMachineBinary(BinaryOperatorSyntax.Add, previous, adjustment, out var updated)) return false;
            index = current;
            result.SetInteger(updated);
            return true;
        }
        catch
        {
            index = current;
            throw;
        }
        finally { arguments.Clear(); }
    }

    private static bool TryEvaluatePreparedBinary(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        in ExecutableInstruction instruction, ExecutionContext scope, out long value)
    {
        value = 0;
        return TryReadMachineOperand(code, values, instruction.InputStart, scope, out var left) &&
            TryReadMachineOperand(code, values, instruction.SecondInput, scope, out var right) &&
            TryMachineBinary(instruction.BinaryOperator, left, right, out value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPrepareFixedTupleReturn(ExecutableCodeObject code, Span<ExecutableSlot> values,
        ref int index, int liveExtent, int unpackCount, ExecutionContext scope)
    {
        var tupleIndex = code.Instructions[index].PreparedTupleIndex;
        ref readonly var tuple = ref code.Instructions[tupleIndex];
        if (unpackCount != tuple.ItemCount || liveExtent != tuple.InputStart) return false;

        var items = values.Slice(liveExtent, unpackCount);
        var completed = false;
        long scalarBytes = 0;
        try
        {
            for (var i = 0; i < items.Length; i++)
            {
                ref readonly var producer = ref code.Instructions[index + i];
                scope.CheckExecution(producer.Span);
                ExecutableSlot item;
                if (producer.OpCode == ExecutableOpCode.Binary)
                {
                    if (!TryEvaluatePreparedBinary(code, values, producer, scope, out var result)) return false;
                    item = ExecutableSlot.FromInteger(result);
                }
                else
                {
                    item = values[producer.LocalSlot];
                    if (!item.HasMaterializedValue && !item.TryGetInteger(out _)) return false;
                    // Preserve an already published identity or an existing
                    // tuple borrow. A fresh local has one returned position.
                }
                items[i] = item;
                scalarBytes = checked(scalarBytes + item.ScalarStorageBytes);
            }
            // Until admission succeeds the slots are private to this helper.
            // Keep the tuple's source position for a refusal or cancellation.
            index = tupleIndex;
            scope.State.CheckExecution(tuple.Span);
            AdmitUnpackedReturnScalars(scalarBytes, scope, tuple.Span);
            return completed = true;
        }
        finally
        {
            if (!completed) items.Clear();
        }
    }

    internal ref partial struct NativeExecution
    {
        private bool TryIntegerOperation()
        {
            if (!TryEvaluateIntegerInstruction(Code, Values, Index, _scope, out var end, out var result, _cells)) return false;
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
