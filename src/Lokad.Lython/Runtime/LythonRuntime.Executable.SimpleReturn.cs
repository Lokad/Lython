using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // These bodies cannot expose executable locals, suspend or invoke guest code.
    // Keep the real function context, binding and entry guards; only the transient
    // interpreter and its operand/cache storage are unnecessary.
    internal static bool TryExecuteSimpleReturn(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments arguments,
        LythonSourceSpan entrySpan,
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
            && !TryReadSimpleOperand(codeObject, instructions[0], arguments, context, out left))
            return false;
        Func<object, LythonSourceSpan, ExecutionContext, object>? builtin = null;
        if (kind == ExecutableSimpleReturn.NativeOperation)
        {
            if (!TryReadSimpleOperand(codeObject, instructions[1], arguments, context, out right)) return false;
            // Decide before executing any operation. Protocol-bearing values
            // retain the full executable frame, including actual suspension.
            switch (instructions[2].OpCode)
            {
                case ExecutableOpCode.Binary:
                    if (left is not BigInteger || right is not BigInteger) return false;
                    break;
                case ExecutableOpCode.Subscript:
                    if (left is not PyDict { HasProtocolKeys: false } || right is not PyString) return false;
                    break;
                case ExecutableOpCode.Call:
                    builtin = SimpleNativeBuiltin(left, right);
                    if (builtin is null) return false;
                    break;
                default:
                    throw new InvalidOperationException("Invalid native return operation.");
            }
        }

        var previousFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            var entryException = context.State.CurrentException;
            if (kind == ExecutableSimpleReturn.ReturnNone)
            {
                context.State.CheckExecution(instructions[0].Span);
            }
            else
            {
                // A checkpoint failure is outside operation exception routing,
                // exactly as in the general interpreter's instruction loop.
                context.State.CheckExecution(instructions[0].Span);
                result = LoadSimpleOperand(instructions[0], left, context, entryException);
                if (kind == ExecutableSimpleReturn.NativeOperation)
                {
                    context.State.CheckExecution(instructions[1].Span);
                    var second = LoadSimpleOperand(instructions[1], right, context, entryException);
                    context.State.CheckExecution(instructions[2].Span);
                    try
                    {
                        var operation = instructions[2];
                        result = operation.OpCode switch
                        {
                            ExecutableOpCode.Binary => EvaluateBinaryOperator(BinaryOperatorSyntax.Add, result, second, context, operation.Span),
                            ExecutableOpCode.Subscript => ReadSubscriptValue(result, second, operation.Span, context),
                            ExecutableOpCode.Call => InvokeSingleInput(second, builtin!, codeObject.CallSites[operation.CallSiteIndex].CallSpan, context),
                            _ => throw new InvalidOperationException("Invalid native return operation."),
                        };
                    }
                    catch (LythonRuntimeException error)
                    {
                        RestoreSimpleReturnException(context, entryException, error);
                        throw;
                    }
                }
                context.State.CheckExecution(instructions[^1].Span);
            }
            // No stack temporaries or handler variables exist in these bodies.
            context.State.SetCurrentException(entryException);
            return true;
        }
        finally
        {
            context.LeaveExecutableSlots(previousFrame);
            context.LeaveInterpreterFrame();
        }
    }

    private static bool TryReadSimpleOperand(ExecutableCodeObject codeObject, ExecutableInstruction instruction,
        BoundCallArguments arguments, ExecutionContext context, out object? value)
    {
        if (instruction.OpCode == ExecutableOpCode.LoadConst)
        {
            value = codeObject.Constants[instruction.ConstantIndex];
            return true;
        }
        if (instruction.OpCode is ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName)
        {
            var name = codeObject.Names[instruction.NameIndex];
            // Implicit module lookup is pure. Nested/class/comprehension and
            // nonlocal name rules keep their existing executable resolution.
            if (instruction.OpCode == ExecutableOpCode.LoadName &&
                (context.ParentContext?.ParentContext is not null || context.IsClassBody || context.IsAnnotationScope ||
                 context.ScopeFacts.IsNonlocal(name) || context.ScopeFacts.LocalNames.Contains(name) ||
                 context.InlinedComprehensionLookup is not null || context.TryGetInlinedBinding(name, out _)))
            {
                value = null;
                return false;
            }
            return TryResolveExecutableGlobal(name, context, out value);
        }
        var index = instruction.LocalSlot;
        var bound = (uint)index < (uint)arguments.Assigned.Length;
        value = bound ? arguments.Values[index] : null;
        // Unbound locals must fail through the original LoadLocal operation.
        return bound;
    }

    private static Func<object, LythonSourceSpan, ExecutionContext, object>? SimpleNativeBuiltin(object? target, object? value)
    {
        if (target is not BuiltinCallable builtin) return null;
        return builtin.Name switch
        {
            "int" when value is PyString or PyBytes => IntegerTextOperation,
            "float" when value is PyString or PyBytes => FloatTextOperation,
            "str" when value is BigInteger or PyString => NativeStrOperation,
            "abs" when ExecutionContext.IsAbsoluteBuiltin(target) && (value is BigInteger or double or bool) => AbsoluteOperation,
            "len" when ExecutionContext.IsLengthBuiltin(target) &&
                (value is PyString or PyBytes or PyList or PyTuple or PyDict or PySet or PyRange or PyDeque) => LengthOperation,
            _ => null,
        };
    }

    private static object LoadSimpleOperand(ExecutableInstruction instruction, object? value,
        ExecutionContext context, PyException? entryException)
    {
        if (instruction.OpCode == ExecutableOpCode.LoadLocal) return value!;
        try
        {
            var constant = RuntimeValue(value);
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
        var previousActive = context.State.CurrentException;
        if (previousActive is not null && !ReferenceEquals(error.OriginalPythonException, previousActive))
            error.PythonContext ??= previousActive;
        context.State.SetCurrentException(entryException);
    }
}
