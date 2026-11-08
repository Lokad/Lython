using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Keep transient guest references outside the long-running dispatch loop.
        // Genuine async work captures its values; raw type protocols use the
        // existing stack scratch. Context promotion runs no guest/admission hook.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InstructionPause ExecuteInputInstruction(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction, bool asynchronous,
            out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan)
        {
            pending = default;
            pendingSpan = null;
            // A failed leaf read precedes operation cleanup/routing, just as the
            // separate load did. No protocol or admission has run at this point.
            // Descriptor resumption uses the original receiver/key retained in
            // funded protocol slots. Otherwise read prepared positions once.
            var retained = instruction.OpCode == ExecutableOpCode.Subscript && invocation?._preparedCallValue is not null;
            var first = retained ? execution.Stack[instruction.ResultSlot]
                : ReadOperand(ref execution, instruction.InputStart, scope, 0);
            var second = instruction.OpCode == ExecutableOpCode.Unary ? null
                : retained ? execution.Stack[instruction.ResultSlot + 1]
                : ReadOperand(ref execution, instruction.SecondInput, scope, 1);
            var third = instruction.OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast
                ? ReadOperand(ref execution, instruction.ThirdInput, scope, 2) : null;
            try
            {
                if (instruction.OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast)
                {
                    var receiver = instruction.ValueLast ? first : second!;
                    var key = instruction.ValueLast ? second! : third!;
                    var value = instruction.ValueLast ? third! : first;
                    var span = (LythonSourceSpan)execution.Code.Constants[instruction.TargetSpanIndex]!;
                    if (invocation is null)
                    {
                        if (receiver is not PyDict table || !table.TryWriteKeyWithoutAdmission(key, value, scope, span))
                        {
                            return InstructionPause.ContextRequired;
                        }
                        try { scope.CheckExecution(span); }
                        finally { execution.Stack.RetireTo(instruction.LiveExtent); }
                        return InstructionPause.Continue;
                    }
                    if (!asynchronous)
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

                if (instruction.OpCode == ExecutableOpCode.Subscript && instruction.LookupCacheIndex >= 0 &&
                    first is PyDict { HasSharedRecordKeys: true } record && second is PyString field)
                {
                    // Acquire backing before proving the live layout: admission
                    // may reenter the host and mutate this same dictionary.
                    var caches = execution.Stack.GetLookupCaches(execution.Code.LookupCacheCount, instruction.Span);
                    if (record.TryReadRecordValue(field, ref caches[instruction.LookupCacheIndex], scope, instruction.Span,
                        out var fieldValue))
                    {
                        PublishInstructionResult(ref execution, invocation, instruction, fieldValue);
                        return InstructionPause.Continue;
                    }
                }

                var immediate = (!asynchronous && invocation is not null) || (instruction.OpCode switch
                {
                    ExecutableOpCode.Binary or ExecutableOpCode.Augmented =>
                        first is BigInteger or double or bool && second is BigInteger or double or bool ||
                        first is PyString && second is PyString,
                    ExecutableOpCode.Unary => first is BigInteger or double or bool,
                    ExecutableOpCode.Subscript => second is BigInteger && first is PyList or PyTuple or PyBytes or PyString or PyRange ||
                        first is PyDict dictionary && dictionary.CanReadKeySynchronously(second!),
                    _ => throw new InvalidOperationException("Unexpected direct input instruction."),
                });
                if (!immediate && invocation is null)
                {
                    return InstructionPause.ContextRequired;
                }
                if (instruction.OpCode == ExecutableOpCode.Subscript && invocation is not null &&
                    invocation.TrySuspendForTypeSubscription(instruction, first, second!))
                {
                    return InstructionPause.Boundary;
                }
                if (immediate)
                {
                    var result = instruction.OpCode switch
                    {
                        ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator, first, second!, scope, instruction.Span),
                        ExecutableOpCode.Augmented => EvaluateAugmentedAssignment(first, second!, instruction.AugmentedOperator, scope, instruction.Span),
                        ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, first, scope, instruction.Span),
                        ExecutableOpCode.Subscript => invocation?._preparedCallValue is not null
                            ? invocation.InvokePreparedTypeSubscription(second!, instruction.Span, false).GetAwaiter().GetResult()
                            : ReadSubscriptValue(first, second!, instruction.Span, scope),
                        _ => throw new InvalidOperationException("Unexpected direct input instruction."),
                    };
                    PublishInstructionResult(ref execution, invocation, instruction, result);
                }
                else
                {
                    pending = WithoutJump(invocation!.ExecuteCapturedValueOperationAsync(instruction, first, second));
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
}
