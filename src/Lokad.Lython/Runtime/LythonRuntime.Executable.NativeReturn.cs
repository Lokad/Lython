using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // These bodies cannot expose executable locals, suspend or invoke guest code.
    // Binding and entry guards remain shared. Module-root definitions can also
    // omit the lexical context: their native body only needs shared state/globals.
    internal static bool TryExecuteNativeReturn(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        ReadOnlySpan<object?> arguments,
        LythonSourceSpan entrySpan,
        out object result)
    {
        result = PyNone.Instance;
        var root = codeObject.NativeReturnRoot;
        if (root is null || context.MirrorLocalStores
            || context.CurrentExecutableFrame is not null || context.ActiveHandlerVariables is { Count: > 0 })
            return false;

        if (!GuardNativeBody(codeObject, arguments, context, out var instructions)) return false;
        result = ExecuteNativeBody(codeObject, context, arguments, entrySpan, instructions);
        return true;
    }

    internal static bool TryExecuteNativeModuleFunction(ExecutableCodeObject code, ExecutionContext module,
        ReadOnlySpan<object?> arguments, string name, LythonSourceSpan span, out object result)
    {
        result = PyNone.Instance;
        if (!GuardNativeBody(code, arguments, module, out var instructions)) return false;
        // Entry failure has no function traceback, exactly as real frame entry.
        module.EnterFunctionCall(span);
        try
        {
            result = ExecuteNativeBody(code, module, arguments, span, instructions);
            return true;
        }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, module, name, span);
            throw;
        }
        finally { module.LeaveFunctionCall(); }
    }

    private static bool GuardNativeBody(ExecutableCodeObject codeObject, ReadOnlySpan<object?> arguments,
        ExecutionContext context, out ExecutableInstruction[] instructions)
    {
        instructions = codeObject.Instructions;
        var root = codeObject.NativeReturnRoot!.Value;
        // All type/callable guards precede work. No retry through the ordinary
        // engine is allowed once any operation (including admission) has begun.
        if (root < 0) return true;
        if (instructions[root].OpCode == ExecutableOpCode.ApplyOperation)
            return codeObject.Constants[instructions[root].ConstantIndex] is ExecutableFinishFormattedString
                ? GuardNativeFormattedReturn(instructions[root].NativeFirst, codeObject, instructions, arguments, context)
                : GuardNativeFormattedValue(root, codeObject, instructions, arguments, context);
        return GuardNativeReturn(root, codeObject, instructions, arguments, context, out _, out _);
    }

    private static object ExecuteNativeBody(ExecutableCodeObject codeObject, ExecutionContext context,
        ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan, ExecutableInstruction[] instructions)
    {
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            var root = codeObject.NativeReturnRoot!.Value;
            object result = PyNone.Instance;
            var entryException = context.State.CurrentException;
            if (root < 0)
            {
                context.State.CheckExecution(instructions[0].Span);
            }
            else
            {
                result = instructions[root].OpCode == ExecutableOpCode.ApplyOperation
                    ? EvaluateNativeFormattedReturn(codeObject, instructions, arguments, context, entryException)
                    : EvaluateNativeReturn(root, codeObject, instructions, arguments, context, entryException);
                context.State.CheckExecution(instructions[^1].Span);
            }
            // No stack temporaries or handler variables exist in these bodies.
            context.State.SetCurrentException(entryException);
            return result;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private enum NativeReturnShape { Other, None, Integer, Float, Boolean, String, Bytes, Dictionary, Sized }

    private static NativeReturnShape ReturnShape(object? value) => value switch
    {
        null or PyNone => NativeReturnShape.None,
        BigInteger => NativeReturnShape.Integer,
        double => NativeReturnShape.Float,
        bool => NativeReturnShape.Boolean,
        PyString => NativeReturnShape.String,
        PyBytes => NativeReturnShape.Bytes,
        PyDict { HasProtocolKeys: false } => NativeReturnShape.Dictionary,
        PyList or PyTuple or PyDict or PySet or PyRange or PyDeque => NativeReturnShape.Sized,
        _ => NativeReturnShape.Other,
    };

    private static bool GuardNativeReturn(int index, ExecutableCodeObject code, ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments,
        ExecutionContext context, out NativeReturnShape shape, out object? value)
    {
        ref readonly var instruction = ref instructions[index];
        var call = instruction.OpCode == ExecutableOpCode.Call ? code.CallSites[instruction.CallSiteIndex] : null;
        var memberIndex = call is null ? -1 : instruction.NativeSecond;
        var secondIndex = call is null ? instruction.NativeSecond : call.NativeArgumentIndex(0);
        var thirdIndex = call is null ? -1 : call.NativeArgumentIndex(1);
        var fourthIndex = call is null ? -1 : call.NativeArgumentIndex(2);
        value = null;
        shape = NativeReturnShape.Other;
        if (instruction.NativeFirst < 0)
        {
            if (!TryReadNativeOperand(code, instruction, arguments, context, out value)) return false;
            shape = ReturnShape(value);
            return true;
        }
        // A builtin target is a resolved operand, not a computed native value.
        // Classifying its value shape would do work whose result is discarded.
        var first = NativeReturnShape.Other;
        var builtinCall = instruction.OpCode == ExecutableOpCode.Call && memberIndex < 0;
        if (builtinCall)
        {
            if (code.CallSites[instruction.CallSiteIndex].ArgumentCount != 1) return false;
            ref readonly var callable = ref instructions[instruction.NativeFirst];
            if (callable.NativeFirst >= 0 || !TryReadNativeOperand(code, callable, arguments, context, out var target)
                || target is not BuiltinCallable) return false;
            ref readonly var input = ref instructions[secondIndex];
            object? valueInput = null;
            var inputShape = NativeReturnShape.Other;
            if (input.NativeFirst < 0)
            {
                if (!TryReadNativeOperand(code, input, arguments, context, out valueInput)) return false;
            }
            else if (!GuardNativeReturn(secondIndex, code, instructions, arguments, context, out inputShape, out _)) return false;
            return SelectNativeUnaryOperation(target, valueInput, inputShape, out shape) is not null
                && shape != NativeReturnShape.Other;
        }
        else if (!GuardNativeReturn(instruction.NativeFirst, code, instructions, arguments, context, out first, out _)) return false;
        var second = NativeReturnShape.None;
        var third = NativeReturnShape.None;
        var fourth = NativeReturnShape.None;
        if (secondIndex >= 0 && !GuardNativeReturn(secondIndex, code, instructions, arguments, context, out second, out _)) return false;
        if (thirdIndex >= 0 && !GuardNativeReturn(thirdIndex, code, instructions, arguments, context, out third, out _)) return false;
        if (fourthIndex >= 0 && !GuardNativeReturn(fourthIndex, code, instructions, arguments, context, out fourth, out _)) return false;
        switch (instruction.OpCode)
        {
            case ExecutableOpCode.Binary:
                shape = first;
                return first == second && first is NativeReturnShape.Integer or NativeReturnShape.String;
            case ExecutableOpCode.Subscript:
                return first == NativeReturnShape.Dictionary && second == NativeReturnShape.String;
            case ExecutableOpCode.Call when first == NativeReturnShape.String:
                var member = code.Names[instructions[memberIndex].NameIndex];
                var count = code.CallSites[instruction.CallSiteIndex].ArgumentCount;
                if (count == 0 && StringMembers.GetNoArgumentsOperation(member) is not null)
                {
                    shape = member is "strip" or "lower" or "upper" ? NativeReturnShape.String : NativeReturnShape.Boolean;
                    return true;
                }
                if (member == "split" && count <= 2 && second is NativeReturnShape.None or NativeReturnShape.String
                    && third is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean)
                {
                    shape = NativeReturnShape.Sized;
                    return true;
                }
                if (member is "startswith" or "endswith" && count == 1 && second == NativeReturnShape.String)
                {
                    shape = NativeReturnShape.Boolean;
                    return true;
                }
                if (member == "replace" && count is 2 or 3 && second == NativeReturnShape.String
                    && third == NativeReturnShape.String && fourth is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean)
                {
                    shape = NativeReturnShape.String;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static object EvaluateNativeReturn(int index, ExecutableCodeObject code, ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments,
        ExecutionContext context, PyException? entryException)
    {
        ref readonly var instruction = ref instructions[index];
        var call = instruction.OpCode == ExecutableOpCode.Call ? code.CallSites[instruction.CallSiteIndex] : null;
        var memberIndex = call is null ? -1 : instruction.NativeSecond;
        var secondIndex = call is null ? instruction.NativeSecond : call.NativeArgumentIndex(0);
        var thirdIndex = call is null ? -1 : call.NativeArgumentIndex(1);
        var fourthIndex = call is null ? -1 : call.NativeArgumentIndex(2);
        object? first = null, second = null, third = null, fourth = null;
        if (instruction.NativeFirst >= 0)
        {
            first = EvaluateNativeReturn(instruction.NativeFirst, code, instructions, arguments, context, entryException);
            if (memberIndex >= 0) context.State.CheckExecution(instructions[memberIndex].Span);
            if (secondIndex >= 0) second = EvaluateNativeReturn(secondIndex, code, instructions, arguments, context, entryException);
            if (thirdIndex >= 0) third = EvaluateNativeReturn(thirdIndex, code, instructions, arguments, context, entryException);
            if (fourthIndex >= 0) fourth = EvaluateNativeReturn(fourthIndex, code, instructions, arguments, context, entryException);
        }
        // As in the instruction loop, checkpoint failures are outside operation
        // exception routing; operation failures restore the caller's exception.
        context.State.CheckExecution(instruction.Span);
        try
        {
            if (instruction.NativeFirst < 0)
            {
                TryReadNativeOperand(code, instruction, arguments, context, out var operand);
                return RuntimeValue(operand);
            }
            if (instruction.OpCode == ExecutableOpCode.Binary)
                return EvaluateBinaryOperator(BinaryOperatorSyntax.Add, first!, second!, context, instruction.Span);
            if (instruction.OpCode == ExecutableOpCode.Subscript)
                return ReadSubscriptValue(first!, second!, instruction.Span, context);
            var site = code.CallSites[instruction.CallSiteIndex];
            if (memberIndex < 0)
                return InvokeSingleInput(second!, SelectNativeUnaryOperation(first, second, NativeReturnShape.Other, out _)!, site.CallSpan, context);
            var receiver = (PyString)first!;
            var member = code.Names[instructions[memberIndex].NameIndex];
            if (site.ArgumentCount == 0 && StringMembers.GetNoArgumentsOperation(member) is { } operation)
                return InvokeSingleInput(receiver, operation, site.CallSpan, context);
            return member switch
            {
                "split" => InvokeStringSplit(receiver, second, third, site.CallSpan, context),
                "replace" => InvokeStringReplace(receiver, second!, third!, fourth, site.CallSpan, context),
                "startswith" or "endswith" => InvokeStringAffix(receiver, second!, site.CallSpan, context, member == "startswith"),
                _ => throw new InvalidOperationException("Invalid native return method."),
            };
        }
        catch (LythonRuntimeException error)
        {
            RestoreNativeReturnException(context, entryException, error);
            throw;
        }
    }

    private static bool TryReadNativeOperand(ExecutableCodeObject codeObject, in ExecutableInstruction instruction,
        ReadOnlySpan<object?> arguments, ExecutionContext context, out object? value)
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
                 codeObject.ScopeFacts.IsNonlocal(name) || codeObject.ScopeFacts.LocalNames.Contains(name) ||
                 context.InlinedComprehensionLookup is not null || context.TryGetInlinedBinding(name, out _)))
            {
                value = null;
                return false;
            }
            return TryResolveExecutableGlobal(name, context, out value);
        }
        var index = instruction.LocalSlot;
        var bound = (uint)index < (uint)arguments.Length;
        value = bound ? arguments[index] : null;
        // Unbound locals must fail through the original LoadLocal operation.
        return bound;
    }

    private static void RestoreNativeReturnException(ExecutionContext context, PyException? entryException,
        LythonRuntimeException error)
    {
        var previousActive = context.State.CurrentException;
        if (previousActive is not null && !ReferenceEquals(error.OriginalPythonException, previousActive))
            error.PythonContext ??= previousActive;
        context.State.SetCurrentException(entryException);
    }
}
