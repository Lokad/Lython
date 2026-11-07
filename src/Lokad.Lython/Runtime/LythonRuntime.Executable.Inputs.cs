using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal readonly struct ExecutableInputs(object first, object? second = null, object? third = null)
    {
        internal readonly object First = first;
        internal readonly object? Second = second;
        internal readonly object? Third = third;
    }

    internal sealed partial class ExecutionContext
    {
        private static ExecutableInputs ReadInstructionInputs(ref ExecutableWorkState execution,
            in ExecutableInstruction instruction, ExecutionContext scope, ExecutionContext? invocation)
        {
            // A descriptor getter already retained the original type/key in
            // the existing funded protocol slots. Its callback may rebind locals;
            // the resumed lookup must consume these snapshots, not those locals.
            if (instruction.OpCode == ExecutableOpCode.Subscript && invocation?._preparedCallValue is not null)
                return new(execution.Stack[instruction.ResultSlot], execution.Stack[instruction.ResultSlot + 1]);
            return new(ReadOperand(ref execution, instruction.InputStart, scope, 0),
                instruction.ValueInputCount > 1 ? ReadOperand(ref execution, instruction.SecondInput, scope, 1) : null,
                instruction.ValueInputCount == 3 ? ReadOperand(ref execution, instruction.ThirdInput, scope, 2) : null);
        }

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
            var inputs = ReadInstructionInputs(ref execution, instruction, scope, invocation);
            try
            {
                if (instruction.OpCode is ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast)
                {
                    var receiver = instruction.ValueLast ? inputs.First : inputs.Second!;
                    var key = instruction.ValueLast ? inputs.Second! : inputs.Third!;
                    var value = instruction.ValueLast ? inputs.Third! : inputs.First;
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

                var immediate = (!asynchronous && invocation is not null) || (instruction.OpCode switch
                {
                    ExecutableOpCode.Binary or ExecutableOpCode.Augmented =>
                        inputs.First is BigInteger or double or bool && inputs.Second is BigInteger or double or bool ||
                        inputs.First is PyString && inputs.Second is PyString,
                    ExecutableOpCode.Unary => inputs.First is BigInteger or double or bool,
                    ExecutableOpCode.Subscript => inputs.Second is BigInteger && inputs.First is PyList or PyTuple or PyBytes or PyString or PyRange ||
                        inputs.First is PyDict dictionary && dictionary.CanReadKeySynchronously(inputs.Second!),
                    _ => throw new InvalidOperationException("Unexpected direct input instruction."),
                });
                if (!immediate && invocation is null)
                {
                    return InstructionPause.ContextRequired;
                }
                if (instruction.OpCode == ExecutableOpCode.Subscript && invocation is not null &&
                    invocation.TrySuspendForTypeSubscription(instruction, inputs))
                {
                    return InstructionPause.Boundary;
                }
                if (immediate)
                {
                    var result = instruction.OpCode switch
                    {
                        ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator, inputs.First, inputs.Second!, scope, instruction.Span),
                        ExecutableOpCode.Augmented => EvaluateAugmentedAssignment(inputs.First, inputs.Second!, instruction.AugmentedOperator, scope, instruction.Span),
                        ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, inputs.First, scope, instruction.Span),
                        ExecutableOpCode.Subscript => invocation?._preparedCallValue is not null
                            ? invocation.InvokePreparedTypeSubscription(inputs.Second!, instruction.Span, false).GetAwaiter().GetResult()
                            : ReadSubscriptValue(inputs.First, inputs.Second!, instruction.Span, scope),
                        _ => throw new InvalidOperationException("Unexpected direct input instruction."),
                    };
                    PublishInstructionResult(ref execution, invocation, instruction, result);
                }
                else
                {
                    pending = WithoutJump(invocation!.ExecuteCapturedValueOperationAsync(instruction, inputs));
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

        private async ValueTask ExecuteCapturedValueOperationAsync(ExecutableInstruction instruction, ExecutableInputs inputs)
        {
            try
            {
                object result;
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Binary:
                        result = await EvaluateBinaryOperatorAsync(instruction.BinaryOperator, inputs.First, inputs.Second!, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Augmented:
                        result = await EvaluateAugmentedAssignmentAsync(inputs.First, inputs.Second!, instruction.AugmentedOperator, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Unary:
                        result = await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), inputs.First, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Subscript:
                        result = _preparedCallValue is not null
                            ? await InvokePreparedTypeSubscription(inputs.Second!, instruction.Span, true).ConfigureAwait(false)
                            : await ReadLoweredSubscriptAsync(inputs.First, inputs.Second!, instruction.Span, this).ConfigureAwait(false);
                        break;
                    default: throw new InvalidOperationException("Unexpected captured value instruction.");
                }
                PublishInstructionResult(instruction, result);
            }
            catch { _execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }
    }
}
