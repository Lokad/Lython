using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // These bodies cannot expose executable locals, suspend or invoke guest code.
    // Keep the real function context, binding and entry guards; only the transient
    // interpreter, local array and operand array are unnecessary.
    internal static bool TryExecuteSimpleReturn(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments arguments,
        int[] argumentSlotMap,
        out object result)
    {
        result = PyNone.Instance;
        var kind = codeObject.SimpleReturn;
        if (kind == ExecutableSimpleReturn.None || context.MirrorLocalStores
            || context.CurrentExecutableFrame is not null || context.ActiveHandlerVariables is { Count: > 0 })
            return false;

        var instructions = codeObject.Blocks[codeObject.EntryBlockIndex].InstructionArray;
        object? left = null, right = null;
        if (kind != ExecutableSimpleReturn.ReturnNone
            && !TryReadSimpleOperand(codeObject, instructions[0], arguments, argumentSlotMap, out left))
            return false;
        if (kind == ExecutableSimpleReturn.IntegerAdd
            && (!TryReadSimpleOperand(codeObject, instructions[1], arguments, argumentSlotMap, out right)
                || left is not BigInteger || right is not BigInteger))
            return false;

        var previousFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(null);
        try
        {
            var entryException = context.Services.CurrentException;
            if (kind == ExecutableSimpleReturn.ReturnNone)
            {
                context.Services.CheckExecution(instructions[0].Span);
            }
            else
            {
                // A checkpoint failure is outside operation exception routing,
                // exactly as in the general interpreter's instruction loop.
                context.Services.CheckExecution(instructions[0].Span);
                result = LoadSimpleOperand(instructions[0], left, context, entryException);
                if (kind == ExecutableSimpleReturn.IntegerAdd)
                {
                    context.Services.CheckExecution(instructions[1].Span);
                    var second = LoadSimpleOperand(instructions[1], right, context, entryException);
                    context.Services.CheckExecution(instructions[2].Span);
                    try
                    {
                        result = EvaluateExecutableBinary(ExecutableBinaryOperator.Add, result, second, instructions[2].Span, context);
                        context.ObserveValue(result, instructions[2].Span);
                    }
                    catch (LythonRuntimeException error)
                    {
                        RestoreSimpleReturnException(context, entryException, error);
                        throw;
                    }
                }
                context.Services.CheckExecution(instructions[^1].Span);
            }
            // No stack temporaries or handler variables exist in these bodies.
            context.Services.SetCurrentException(entryException);
            return true;
        }
        finally
        {
            context.LeaveExecutableSlots(previousFrame);
            context.LeaveInterpreterFrame();
        }
    }

    private static bool TryReadSimpleOperand(ExecutableCodeObject codeObject, ExecutableInstruction instruction,
        BoundCallArguments arguments, int[] argumentSlotMap, out object? value)
    {
        if (instruction.OpCode == ExecutableOpCode.LoadConst)
        {
            value = codeObject.Constants[instruction.ConstantIndex];
            return true;
        }
        var index = Array.IndexOf(argumentSlotMap, instruction.LocalSlot);
        value = index >= 0 ? arguments.Values[index] : null;
        // Unbound locals must fail through the original LoadLocal operation.
        return index >= 0;
    }

    private static object LoadSimpleOperand(ExecutableInstruction instruction, object? value,
        ExecutionContext context, PyException? entryException)
    {
        if (instruction.OpCode == ExecutableOpCode.LoadLocal) return value!;
        try
        {
            var constant = RuntimeValue(value);
            context.ObserveValue(constant, instruction.Span);
            return constant;
        }
        catch (LythonRuntimeException error)
        {
            RestoreSimpleReturnException(context, entryException, error);
            throw;
        }
    }

    private static void RestoreSimpleReturnException(ExecutionContext context, PyException? entryException,
        LythonRuntimeException error)
    {
        var previousActive = context.Services.CurrentException;
        if (previousActive is not null && !ReferenceEquals(error.OriginalPythonException, previousActive))
            error.PythonContext ??= previousActive;
        context.Services.SetCurrentException(entryException);
    }
}
