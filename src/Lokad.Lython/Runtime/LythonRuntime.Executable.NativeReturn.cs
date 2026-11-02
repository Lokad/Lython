using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // These bodies cannot expose executable locals, suspend or invoke guest code.
    // Keep the real function context, binding and entry guards; only the transient
    // interpreter and its operand/cache storage are unnecessary.
    internal static bool TryExecuteNativeReturn(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        BoundCallArguments arguments,
        LythonSourceSpan entrySpan,
        out object result)
    {
        result = PyNone.Instance;
        var nodes = codeObject.NativeReturn;
        if (nodes is null || context.MirrorLocalStores
            || context.CurrentExecutableFrame is not null || context.ActiveHandlerVariables is { Count: > 0 })
            return false;

        var instructions = codeObject.Blocks[codeObject.EntryBlockIndex].InstructionArray;
        // All type/callable guards precede work. No retry through the ordinary
        // engine is allowed once any operation (including admission) has begun.
        if (nodes.Length != 0 && !GuardNativeReturn(nodes.Length - 1, codeObject, instructions, arguments, context, out _, out _))
            return false;

        var previousFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            var entryException = context.State.CurrentException;
            if (nodes.Length == 0)
            {
                context.State.CheckExecution(instructions[0].Span);
            }
            else
            {
                result = EvaluateNativeReturn(nodes.Length - 1, codeObject, instructions, arguments, context, entryException);
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

    private static bool GuardNativeReturn(int index, ExecutableCodeObject code, ExecutableInstruction[] instructions, BoundCallArguments arguments,
        ExecutionContext context, out NativeReturnShape shape, out object? value)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        value = null;
        shape = NativeReturnShape.Other;
        if (node.First < 0)
        {
            if (!TryReadNativeOperand(code, instruction, arguments, context, out value)) return false;
            shape = ReturnShape(value);
            return true;
        }
        // A builtin target is a resolved operand, not a computed native value.
        // Classifying its value shape would do work whose result is discarded.
        var first = NativeReturnShape.Other;
        var builtinCall = instruction.OpCode == ExecutableOpCode.Call && node.MemberInstruction < 0;
        if (builtinCall)
        {
            if (code.CallSites[instruction.CallSiteIndex].ArgumentCount != 1) return false;
            var callable = code.NativeReturn[node.First];
            if (callable.First >= 0 || !TryReadNativeOperand(code, instructions[callable.Instruction], arguments, context, out var target)
                || target is not BuiltinCallable) return false;
            var input = code.NativeReturn[node.Second];
            object? valueInput = null;
            var inputShape = NativeReturnShape.Other;
            if (input.First < 0)
            {
                if (!TryReadNativeOperand(code, instructions[input.Instruction], arguments, context, out valueInput)) return false;
            }
            else if (!GuardNativeReturn(node.Second, code, instructions, arguments, context, out inputShape, out _)) return false;
            return SelectNativeUnaryOperation(target, valueInput, inputShape, out shape) is not null
                && shape != NativeReturnShape.Other;
        }
        else if (!GuardNativeReturn(node.First, code, instructions, arguments, context, out first, out _)) return false;
        var second = NativeReturnShape.None;
        var third = NativeReturnShape.None;
        var fourth = NativeReturnShape.None;
        if (node.Second >= 0 && !GuardNativeReturn(node.Second, code, instructions, arguments, context, out second, out _)) return false;
        if (node.Third >= 0 && !GuardNativeReturn(node.Third, code, instructions, arguments, context, out third, out _)) return false;
        if (node.Fourth >= 0 && !GuardNativeReturn(node.Fourth, code, instructions, arguments, context, out fourth, out _)) return false;
        switch (instruction.OpCode)
        {
            case ExecutableOpCode.Binary:
                shape = first;
                return first == second && first is NativeReturnShape.Integer or NativeReturnShape.String;
            case ExecutableOpCode.Subscript:
                return first == NativeReturnShape.Dictionary && second == NativeReturnShape.String;
            case ExecutableOpCode.Call when first == NativeReturnShape.String:
                var member = code.Names[instructions[node.MemberInstruction].NameIndex];
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

    private static object EvaluateNativeReturn(int index, ExecutableCodeObject code, ExecutableInstruction[] instructions, BoundCallArguments arguments,
        ExecutionContext context, PyException? entryException)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        object? first = null, second = null, third = null, fourth = null;
        if (node.First >= 0)
        {
            first = EvaluateNativeReturn(node.First, code, instructions, arguments, context, entryException);
            if (node.MemberInstruction >= 0) context.State.CheckExecution(instructions[node.MemberInstruction].Span);
            if (node.Second >= 0) second = EvaluateNativeReturn(node.Second, code, instructions, arguments, context, entryException);
            if (node.Third >= 0) third = EvaluateNativeReturn(node.Third, code, instructions, arguments, context, entryException);
            if (node.Fourth >= 0) fourth = EvaluateNativeReturn(node.Fourth, code, instructions, arguments, context, entryException);
        }
        // As in the instruction loop, checkpoint failures are outside operation
        // exception routing; operation failures restore the caller's exception.
        context.State.CheckExecution(instruction.Span);
        try
        {
            if (node.First < 0)
            {
                TryReadNativeOperand(code, instruction, arguments, context, out var operand);
                return RuntimeValue(operand);
            }
            if (instruction.OpCode == ExecutableOpCode.Binary)
                return EvaluateBinaryOperator(BinaryOperatorSyntax.Add, first!, second!, context, instruction.Span);
            if (instruction.OpCode == ExecutableOpCode.Subscript)
                return ReadSubscriptValue(first!, second!, instruction.Span, context);
            var site = code.CallSites[instruction.CallSiteIndex];
            if (node.MemberInstruction < 0)
                return InvokeSingleInput(second!, SelectNativeUnaryOperation(first, second, NativeReturnShape.Other, out _)!, site.CallSpan, context);
            var receiver = (PyString)first!;
            var member = code.Names[instructions[node.MemberInstruction].NameIndex];
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

    private static bool TryReadNativeOperand(ExecutableCodeObject codeObject, ExecutableInstruction instruction,
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

    private static void RestoreNativeReturnException(ExecutionContext context, PyException? entryException,
        LythonRuntimeException error)
    {
        var previousActive = context.State.CurrentException;
        if (previousActive is not null && !ReferenceEquals(error.OriginalPythonException, previousActive))
            error.PythonContext ??= previousActive;
        context.State.SetCurrentException(entryException);
    }
}
