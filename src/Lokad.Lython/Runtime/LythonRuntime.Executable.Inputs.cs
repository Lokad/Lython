using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryExecuteBorrowedDictionaryGet(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction key)
        {
            if (!execution.Locals[key.LocalSlot].TryGetShortUtf8(out var packed)) return false;
            var callIndex = execution.InstructionIndex + key.DictionaryGetCallOffset;
            var instructions = execution.Code.Instructions;
            ref readonly var instruction = ref instructions[callIndex];
            var call = execution.Code.CallSites[instruction.CallSiteIndex];
            var start = instruction.ResultSlot;
            if (execution.Stack.Values[start].Reference is not PyDict { SupportsBorrowedTextKeys: true } dictionary ||
                !ReferenceEquals(execution.Stack.Values[start + call.TargetOffset].Reference, ReceiverMethods.DictionaryGet))
                return false;

            object fallback = PyNone.Instance;
            if (call.ArgumentCount == 2)
            {
                ref readonly var load = ref instructions[callIndex - 1];
                scope.State.CheckExecution(load.Span);
                fallback = RuntimeValue(execution.Code.Constants[load.ConstantIndex]);
            }
            execution.InstructionIndex = callIndex;
            scope.State.CheckExecution(instruction.Span);
            var context = invocation ?? scope;
            try
            {
                context.EnterInterpreterFrame(call.CallSpan);
                object result;
                try
                {
                    Span<byte> buffer = stackalloc byte[8];
                    result = dictionary.ReadBorrowedTextKey(ExecutableSlot.UnpackShortUtf8(packed, buffer),
                        fallback, context, call.CallSpan);
                }
                catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
                { throw translated; }
                finally { context.LeaveInterpreterFrame(); }
                PublishInstructionResult(ref execution, invocation, instruction, result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
        }

        // Genuine async work captures its values; raw type protocols use the
        // existing stack scratch. Context promotion runs no guest/admission hook.
        private static InstructionPause ExecuteInputInstruction(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction, bool asynchronous,
            out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan,
            out ValueTask<ExecutionContext?> nextCall, Span<PrivateGeneratorReduction> reduction)
        {
            pending = default;
            pendingSpan = null;
            nextCall = default;
            if (instruction.OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast)
                return ExecuteSubscriptStore(ref execution, scope, invocation, instruction, asynchronous, out pending, out pendingSpan);
            // A failed leaf read precedes operation cleanup/routing, just as the
            // separate load did. No protocol or admission has run at this point.
            // Descriptor resumption uses the original receiver/key retained in
            // funded protocol slots. Otherwise read prepared positions once.
            var subscript = instruction.OpCode == ExecutableOpCode.Subscript;
            var retained = subscript && invocation?._preparedCallValue is not null;
            var first = retained ? execution.Stack[instruction.ResultSlot]
                : ReadOperand(ref execution, instruction.InputStart, scope, 0);
            long machineIndex = 0;
            var second = instruction.OpCode == ExecutableOpCode.Unary ? null
                : retained ? execution.Stack[instruction.ResultSlot + 1]
                : subscript ? ReadSubscriptKey(execution.Code, execution.Stack.Values, execution.InstructionIndex,
                    first, scope, execution.Captures, out machineIndex)
                : ReadOperand(ref execution, instruction.SecondInput, scope, 1);
            try
            {
                if (!subscript &&
                    TryEvaluateClosedArithmetic(instruction, first, second, scope, out var arithmetic))
                {
                    PublishInstructionResult(ref execution, invocation, instruction, arithmetic);
                    return InstructionPause.Continue;
                }
                if (subscript && instruction.LookupCacheIndex >= 0 &&
                    first is PyDict { HasSharedRecordKeys: true } record && second is PyString field)
                {
                    // Acquire backing before proving the live layout: admission
                    // may reenter the host and mutate this same dictionary.
                    var caches = execution.Stack.GetLookupCaches(execution.Code.LookupCacheCount, instruction.Span);
                    if (record.TryReadRecordValue(field, ref caches[instruction.LookupCacheIndex], scope, instruction.Span,
                        out var fieldValue))
                    {
                        if (reduction.IsEmpty ||
                            !TryReduceRecordComparison(ref execution, scope, instruction, fieldValue, ref reduction[0]))
                            PublishInstructionResult(ref execution, invocation, instruction, fieldValue);
                        return InstructionPause.Continue;
                    }
                }

                if (subscript && !retained &&
                    TryReadCompletedSubscript(first, second, machineIndex, instruction.Span, scope, out var item))
                {
                    PublishInstructionResult(ref execution, invocation, instruction, item);
                    return InstructionPause.Continue;
                }
                if (invocation is null)
                {
                    return InstructionPause.ContextRequired;
                }
                if (subscript &&
                    invocation.TryBeginTypeSubscription(instruction, first, second!, asynchronous, out nextCall))
                {
                    pendingSpan = instruction.Span;
                    return InstructionPause.Call;
                }
                if (!asynchronous)
                {
                    var result = instruction.OpCode switch
                    {
                        ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator, first, second!, scope, instruction.Span),
                        ExecutableOpCode.Augmented => EvaluateAugmentedAssignment(first, second!, instruction.AugmentedOperator, scope, instruction.Span),
                        ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, first, scope, instruction.Span),
                        ExecutableOpCode.Subscript => invocation._preparedCallValue is not null
                            ? invocation.InvokePreparedTypeSubscription(second!, instruction.Span, false).GetAwaiter().GetResult()
                            : ReadSubscriptValue(first, second!, instruction.Span, scope),
                        _ => throw new InvalidOperationException("Unexpected direct input instruction."),
                    };
                    PublishInstructionResult(ref execution, invocation, instruction, result);
                }
                else
                {
                    pending = WithoutJump(invocation.ExecuteCapturedValueOperationAsync(instruction, first, second));
                    if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                    _ = ConsumeOperation(ref pending);
                }
                return InstructionPause.Continue;
            }
            catch
            {
                execution.Stack.RetireTo(instruction.ResultSlot);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InstructionPause ExecuteSubscriptStore(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction, bool asynchronous,
            out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan)
        {
            pending = default;
            pendingSpan = null;
            var first = ReadOperand(ref execution, instruction.InputStart, scope, 0);
            var second = ReadOperand(ref execution, instruction.SecondInput, scope, 1);
            var span = (LythonSourceSpan)execution.Code.Constants[instruction.TargetSpanIndex]!;
            if (instruction.StoresBorrowableTextKey && second is PyDict dictionary &&
                execution.Locals[instruction.ThirdInput].TryGetShortUtf8(out var packed))
            {
                try
                {
                    Span<byte> buffer = stackalloc byte[8];
                    if (dictionary.TryReplaceBorrowedTextKey(ExecutableSlot.UnpackShortUtf8(packed, buffer),
                        first, invocation ?? scope, span))
                    {
                        try { scope.CheckExecution(span); }
                        finally { execution.Stack.RetireTo(instruction.LiveExtent); }
                        return InstructionPause.Continue;
                    }
                }
                catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
            }

            var third = ReadOperand(ref execution, instruction.ThirdInput, scope, 2);
            var receiver = instruction.ValueLast ? first : second;
            var key = instruction.ValueLast ? second : third;
            var value = instruction.ValueLast ? third : first;
            try
            {
                if (invocation is null)
                {
                    if (receiver is not PyDict table || !table.TryWriteKeyWithoutAdmission(key, value, scope, span))
                        return InstructionPause.ContextRequired;
                    try { scope.CheckExecution(span); }
                    finally { execution.Stack.RetireTo(instruction.LiveExtent); }
                }
                else if (!asynchronous)
                {
                    try { SetSubscriptValue(receiver, key, value, span, invocation); }
                    finally { execution.Stack.RetireTo(instruction.LiveExtent); }
                }
                else
                {
                    pending = WithoutJump(invocation.CompleteTargetMutation(
                        SetHeaderSubscriptAsync(receiver, key, value, span, invocation), instruction.LiveExtent));
                    if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                    _ = ConsumeOperation(ref pending);
                }
                return InstructionPause.Continue;
            }
            catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }

        private async ValueTask ExecuteCapturedValueOperationAsync(ExecutableInstruction instruction, object first, object? second)
        {
            try
            {
                object result;
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Binary:
                        result = await EvaluateBinaryOperatorAsync(instruction.BinaryOperator, first, second!, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Augmented:
                        result = await EvaluateAugmentedAssignmentAsync(first, second!, instruction.AugmentedOperator, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Unary:
                        result = await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), first, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Subscript:
                        result = _preparedCallValue is not null
                            ? await InvokePreparedTypeSubscription(second!, instruction.Span, true).ConfigureAwait(false)
                            : await ReadLoweredSubscriptAsync(first, second!, instruction.Span, this).ConfigureAwait(false);
                        break;
                    default: throw new InvalidOperationException("Unexpected captured value instruction.");
                }
                PublishInstructionResult(instruction, result);
            }
            catch { _execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }
    }

    // A null key represents an exact machine index borrowed from the slots.
    // Dynamic receivers receive their original Python key, including bool.
    private static object? ReadSubscriptKey(ExecutableCodeObject code, Span<ExecutableSlot> values,
        int index, object receiver, ExecutionContext scope, ExecutableCaptures captures, out long machineIndex)
    {
        ref readonly var instruction = ref code.Instructions[index];
        var source = instruction.SecondInput;
        machineIndex = 0;
        if (receiver is PyList or PyTuple or PyBytes or PyString &&
            TryReadMachineOperand(code, values, source, scope, out machineIndex, captures))
            return null;
        return ReadExecutableOperand(code, values, source, index, 1, scope, captures);
    }

    private static bool TryReadCompletedSubscript(object receiver, object? key, long machineIndex,
        LythonSourceSpan span, ExecutionContext scope, out ExecutableSlot result)
    {
        result = default;
        if (key is null)
        {
            result = PyIndexing.ReadMachineSequenceIndex(receiver, machineIndex, span, scope);
            return true;
        }
        if (!(key is BigInteger or bool && receiver is PyList or PyTuple or PyBytes or PyString or PyRange ||
              receiver is PyDict dictionary && dictionary.CanReadKeySynchronously(key)))
            return false;
        result.Object = ReadSubscriptValue(receiver, key, span, scope);
        return true;
    }
}
