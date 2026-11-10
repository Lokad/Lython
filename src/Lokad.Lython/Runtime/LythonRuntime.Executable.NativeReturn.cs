using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static bool TryExecuteNativeReturn(ExecutableCodeObject codeObject, ExecutionContext context,
        ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan, out object result)
    {
        result = PyNone.Instance;
        var body = codeObject.NativeBody;
        if (body is null || context.MirrorLocalStores || context.CurrentExecutableFrame is not null
            || context.ActiveHandlerVariables is { Count: > 0 })
            return false;
        if (!GuardNativeBody(codeObject, body, arguments, context)) return false;
        result = ExecuteNativeBody(codeObject, body, context, arguments, entrySpan);
        return true;
    }

    internal static bool TryExecuteNativeFunction(ExecutableCodeObject code, ExecutionContext context,
        ReadOnlySpan<object?> arguments, string name, LythonSourceSpan span, out object result,
        bool consumedByIteration = false)
    {
        result = PyNone.Instance;
        var body = code.NativeBody!;
        if (!GuardNativeBody(code, body, arguments, context)) return false;
        context.EnterFunctionCall(span);
        try { result = ExecuteNativeBody(code, body, context, arguments, span, consumedByIteration); return true; }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, context, name, span);
            throw;
        }
        finally { context.LeaveFunctionCall(); }
    }

    // Dynamic checks precede all effects, including admission. A failed proof
    // uses the ordinary executable engine; actual work never falls back/retries.
    private static bool GuardNativeBody(ExecutableCodeObject code, ExecutableNativeBody body,
        ReadOnlySpan<object?> arguments, ExecutionContext context)
        => body.Root < 0 || GuardNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context, out _);

    private static object ExecuteNativeBody(ExecutableCodeObject code, ExecutableNativeBody body,
        ExecutionContext context, ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan,
        bool consumedByIteration = false)
    {
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            object result = PyNone.Instance;
            var entryException = context.State.CurrentException;
            if (body.Root < 0) context.State.CheckExecution(code.Instructions[0].Span);
            else
            {
                result = EvaluateNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context,
                    entryException, consumedByIteration);
                CheckNativeReturnExit(body.Root, body.Values, context);
            }
            context.State.SetCurrentException(entryException);
            return result;
        }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static void CheckNativeReturnExit(int root, NativeReturnValue[] values, ExecutionContext context)
    {
        if (values[root].ReturnSpan is { } span) context.State.CheckExecution(span);
    }

    internal enum NativeReturnShape { Other, None, Integer, Float, Boolean, String, Bytes, Dictionary, Sized }

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

    // One pure fact serves frontend eligibility and the runtime type proof.
    // It authorizes canonical operations, never evaluates values or protocols.
    internal static bool TryGetNativeBinaryShape(BinaryOperatorSyntax op, NativeReturnShape left,
        NativeReturnShape right, out NativeReturnShape result)
    {
        result = NativeReturnShape.Other;
        if (op == BinaryOperatorSyntax.Add && left == NativeReturnShape.String && right == left)
        {
            result = left;
            return true;
        }
        if (left is not (NativeReturnShape.Integer or NativeReturnShape.Float or NativeReturnShape.Boolean)
            || right is not (NativeReturnShape.Integer or NativeReturnShape.Float or NativeReturnShape.Boolean)) return false;
        var integral = left != NativeReturnShape.Float && right != NativeReturnShape.Float;
        result = op switch
        {
            BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or BinaryOperatorSyntax.Multiply
                or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo
                => integral ? NativeReturnShape.Integer : NativeReturnShape.Float,
            BinaryOperatorSyntax.Divide => NativeReturnShape.Float,
            BinaryOperatorSyntax.BitwiseOr or BinaryOperatorSyntax.BitwiseXor or BinaryOperatorSyntax.BitwiseAnd when integral
                => left == NativeReturnShape.Boolean && right == left ? NativeReturnShape.Boolean : NativeReturnShape.Integer,
            BinaryOperatorSyntax.LeftShift or BinaryOperatorSyntax.RightShift when integral => NativeReturnShape.Integer,
            BinaryOperatorSyntax.Equal or BinaryOperatorSyntax.NotEqual or BinaryOperatorSyntax.Less
                or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual
                => NativeReturnShape.Boolean,
            _ => NativeReturnShape.Other,
        };
        return result != NativeReturnShape.Other;
    }

    private static bool TryReadNativeInput(int source, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, out object? value)
    {
        if (source < 0) { value = code.Constants[~source]; return true; }
        if (source < code.FirstTemporarySlot)
        {
            var bound = (uint)source < (uint)arguments.Length;
            value = bound ? arguments[source] : null;
            return bound;
        }
        return TryReadNativeOperand(code, values[source - code.FirstTemporarySlot].Instruction, arguments, context, out value);
    }

    private static bool GuardNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, out NativeReturnShape shape)
    {
        shape = NativeReturnShape.Other;
        if (index < code.FirstTemporarySlot)
        {
            if (!TryReadNativeInput(index, code, values, arguments, context, out var inputValue)) return false;
            shape = ReturnShape(inputValue);
            return true;
        }
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        switch (value.Operation)
        {
            case NativeReturnOperation.None:
                shape = NativeReturnShape.None;
                return true;
            case NativeReturnOperation.EmptySequence:
                shape = NativeReturnShape.Sized;
                return true;
            case NativeReturnOperation.Conditional:
                context.State.CheckExecution(value.Instruction.Span);
                if (!GuardNativeReturn(value.First, code, values, arguments, context, out var condition)
                    || condition == NativeReturnShape.Other
                    || !GuardNativeReturn(value.Second, code, values, arguments, context, out var consequent)
                    || !GuardNativeReturn(value.Third, code, values, arguments, context, out var alternative))
                    return false;
                shape = consequent == alternative ? consequent : NativeReturnShape.Other;
                return true;
            case NativeReturnOperation.Read:
                if (!TryReadNativeOperand(code, value.Instruction, arguments, context, out var input)) return false;
                shape = ReturnShape(input);
                return true;
            case NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue:
                shape = NativeReturnShape.String;
                return GuardNativeFormattedValue(value.First, code, values, arguments, context);
            case NativeReturnOperation.BuiltinUnary:
                if (!TryReadNativeInput(value.First, code, values, arguments, context, out var target)
                    || target is not BuiltinCallable) return false;
                var call = code.CallSites[value.Instruction.CallSiteIndex];
                var argumentIndex = call.NativeArgumentSource(0);
                object? actual = null;
                var inputShape = NativeReturnShape.Other;
                if (argumentIndex < code.FirstTemporarySlot || values[argumentIndex - code.FirstTemporarySlot].Operation == NativeReturnOperation.Read)
                {
                    if (!TryReadNativeInput(argumentIndex, code, values, arguments, context, out actual)) return false;
                }
                else if (!GuardNativeReturn(argumentIndex, code, values, arguments, context, out inputShape)) return false;
                return SelectNativeUnaryOperation(target, actual, inputShape, out shape) is not null && shape != NativeReturnShape.Other;
        }

        if (!GuardNativeReturn(value.First, code, values, arguments, context, out var first)) return false;
        switch (value.Operation)
        {
            case NativeReturnOperation.Binary:
                if (!GuardNativeReturn(value.Second, code, values, arguments, context, out var right)) return false;
                return TryGetNativeBinaryShape(value.Instruction.BinaryOperator, first, right, out shape);
            case NativeReturnOperation.Subscript:
                return GuardNativeReturn(value.Second, code, values, arguments, context, out var key)
                    && first == NativeReturnShape.Dictionary && key == NativeReturnShape.String;
            case NativeReturnOperation.StringMethod or NativeReturnOperation.BytesMethod
                or NativeReturnOperation.BooleanMethod or NativeReturnOperation.ListMethod:
                shape = value.Operation switch
                {
                    NativeReturnOperation.StringMethod => NativeReturnShape.String,
                    NativeReturnOperation.BytesMethod => NativeReturnShape.Bytes,
                    NativeReturnOperation.BooleanMethod => NativeReturnShape.Boolean,
                    _ => NativeReturnShape.Sized,
                };
                return first == NativeReturnShape.String;
            case NativeReturnOperation.CasedSplit:
                shape = NativeReturnShape.Sized;
                return first == NativeReturnShape.String;
        }

        var site = code.CallSites[value.Instruction.CallSiteIndex];
        var second = NativeReturnShape.None;
        var third = NativeReturnShape.None;
        var fourth = NativeReturnShape.None;
        if (site.NativeArgumentSource(0) is not int.MaxValue and var secondIndex
            && !GuardNativeReturn(secondIndex, code, values, arguments, context, out second)) return false;
        if (site.NativeArgumentSource(1) is not int.MaxValue and var thirdIndex
            && !GuardNativeReturn(thirdIndex, code, values, arguments, context, out third)) return false;
        if (site.NativeArgumentSource(2) is not int.MaxValue and var fourthIndex
            && !GuardNativeReturn(fourthIndex, code, values, arguments, context, out fourth)) return false;
        if (first != NativeReturnShape.String) return false;
        switch (value.Operation)
        {
            case NativeReturnOperation.Split:
                shape = NativeReturnShape.Sized;
                return second is NativeReturnShape.None or NativeReturnShape.String
                    && third is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean;
            case NativeReturnOperation.Replace:
                shape = NativeReturnShape.String;
                return second == NativeReturnShape.String && third == NativeReturnShape.String
                    && fourth is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Boolean;
            case NativeReturnOperation.StartsWith or NativeReturnOperation.EndsWith:
                shape = NativeReturnShape.Boolean;
                return second == NativeReturnShape.String;
            default: throw new InvalidOperationException("Unknown prepared native proof.");
        }
    }

    private static bool GuardNativeFormattedValue(int index, ExecutableCodeObject code,
        NativeReturnValue[] values, ReadOnlySpan<object?> arguments, ExecutionContext context)
    {
        if (index >= code.FirstTemporarySlot)
        {
            ref readonly var value = ref values[index - code.FirstTemporarySlot];
            if (value.Operation is NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue)
                return GuardNativeFormattedValue(value.First, code, values, arguments, context);
        }
        // Only this guard needs the actual field type. Do not reuse its value
        // after later admission/effects; execution performs the original read.
        var shape = TryReadNativeFormattedValue(index, code, values, arguments, context, out var field)
            ? ReturnShape(field)
            : GuardNativeReturn(index, code, values, arguments, context, out var computed) ? computed : NativeReturnShape.Other;
        return shape is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Float
            or NativeReturnShape.Boolean or NativeReturnShape.String or NativeReturnShape.Bytes;
    }

    private static bool TryReadNativeFormattedValue(int index, ExecutableCodeObject code,
        NativeReturnValue[] values, ReadOnlySpan<object?> arguments, ExecutionContext context, out object? result)
    {
        if (index < code.FirstTemporarySlot)
            return TryReadNativeInput(index, code, values, arguments, context, out result);
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        if (value.Operation == NativeReturnOperation.Read)
            return TryReadNativeOperand(code, value.Instruction, arguments, context, out result);
        if (value.Operation == NativeReturnOperation.Subscript
            && TryReadNativeFormattedValue(value.First, code, values, arguments, context, out var target)
            && target is PyDict dictionary
            && TryReadNativeFormattedValue(value.Second, code, values, arguments, context, out var key)
            && key is PyString text)
            return dictionary.TryReadNativeStringValue(text, context, value.Instruction.Span, out result);
        result = null;
        return false;
    }

    private static object EvaluateNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException,
        bool consumedByIteration = false)
    {
        // Bound direct inputs are pure. Operations and real roots retain their
        // cancellation/refusal checks without a recursive leaf dispatch.
        if (index < code.FirstTemporarySlot)
            return RuntimeValue(index < 0 ? code.Constants[~index] : arguments[index]);
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        var instruction = value.Instruction;
        if (value.Operation == NativeReturnOperation.Conditional)
        {
            var condition = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            context.State.CheckExecution(instruction.Span);
            var selected = PyTruthiness.IsTruthy(condition) ? value.Second : value.Third;
            var result = EvaluateNativeReturn(selected, code, values, arguments, context, entryException, consumedByIteration);
            CheckNativeReturnExit(selected - code.FirstTemporarySlot, values, context);
            return result;
        }
        object? first = null, second = null, third = null, fourth = null;
        if (value.Operation is not (NativeReturnOperation.Read or NativeReturnOperation.None or NativeReturnOperation.EmptySequence))
        {
            first = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            if (value.Operation == NativeReturnOperation.CasedSplit)
            {
                try { return ExecuteCasedSplit(code.CallSites[instruction.CallSiteIndex].CasedSplit!, (PyString)first,
                    context, consumedByIteration); }
                catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
            }
            if (value.Operation is NativeReturnOperation.Binary or NativeReturnOperation.Subscript)
                second = EvaluateNativeReturn(value.Second, code, values, arguments, context, entryException);
            else if (value.Operation is >= NativeReturnOperation.BuiltinUnary and <= NativeReturnOperation.EndsWith)
            {
                if (value.MemberSpan is { } memberSpan) context.State.CheckExecution(memberSpan);
                var call = code.CallSites[instruction.CallSiteIndex];
                if (call.NativeArgumentSource(0) is not int.MaxValue and var a) second = EvaluateNativeReturn(a, code, values, arguments, context, entryException);
                if (call.NativeArgumentSource(1) is not int.MaxValue and var b) third = EvaluateNativeReturn(b, code, values, arguments, context, entryException);
                if (call.NativeArgumentSource(2) is not int.MaxValue and var c) fourth = EvaluateNativeReturn(c, code, values, arguments, context, entryException);
            }
        }
        context.State.CheckExecution(instruction.Span);
        try
        {
            switch (value.Operation)
            {
                case NativeReturnOperation.None:
                    return PyNone.Instance;
                case NativeReturnOperation.EmptySequence:
                    return instruction.OpCode == ExecutableOpCode.MakeTuple ? PyTuple.Empty
                        : CreateEmptyNativeList(instruction.Span, context);
                case NativeReturnOperation.Read:
                    TryReadNativeOperand(code, instruction, arguments, context, out var operand);
                    return RuntimeValue(operand);
                case NativeReturnOperation.Binary:
                    return EvaluateBinaryOperator(instruction.BinaryOperator, first!, second!, context, instruction.Span);
                case NativeReturnOperation.Subscript:
                    return ReadSubscriptValue(first!, second!, instruction.Span, context);
                case NativeReturnOperation.ConvertFormat:
                    return FormatInterpolatedStringPart(first!, instruction.FormattedConversion, null, context, instruction.Span);
                case NativeReturnOperation.FormatValue:
                    var format = (ExecutableFormatField)code.Constants[instruction.ConstantIndex]!;
                    return FormatInterpolatedStringPart(first!, null, format.FormatSpecifier, context, instruction.Span, format.PreparedFormat);
            }
            var site = code.CallSites[instruction.CallSiteIndex];
            if (value.Operation == NativeReturnOperation.BuiltinUnary)
                return InvokeSingleInput(second!, SelectNativeUnaryOperation(first, second, NativeReturnShape.Other, out _)!, site.CallSpan, context);
            var receiver = (PyString)first!;
            if (value.Method is { } method)
                return InvokeReceiverMethod(method, receiver, site.CallSpan, context);
            return value.Operation switch
            {
                NativeReturnOperation.Split => InvokeStringSplit(receiver, second, third, site.CallSpan, context),
                NativeReturnOperation.Replace => InvokeStringReplace(receiver, second!, third!, fourth, site.CallSpan, context),
                NativeReturnOperation.StartsWith or NativeReturnOperation.EndsWith =>
                    InvokeStringAffix(receiver, second!, site.CallSpan, context, value.Operation == NativeReturnOperation.StartsWith),
                _ => throw new InvalidOperationException("Unknown prepared native value."),
            };
        }
        catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
    }

    // Zero operands have no storage owner, and RetireTo(0) is a no-op.
    // Keep this allocation helper's empty storage out of recursive evaluation.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static PyList CreateEmptyNativeList(LythonSourceSpan span, ExecutionContext context)
    {
        var empty = default(ExecutableValueStorage);
        return CreateListFromStack(ref empty, 0, 0, span, context);
    }

    private static bool TryReadNativeOperand(ExecutableCodeObject codeObject, in ExecutableInstruction instruction,
        ReadOnlySpan<object?> arguments, ExecutionContext context, out object? value)
    {
        if (instruction.OpCode == ExecutableOpCode.LoadConst)
        {
            value = codeObject.Constants[instruction.ConstantIndex];
            return true;
        }
        if (instruction.OpCode == ExecutableOpCode.LoadGlobal)
        {
            var name = codeObject.Names[instruction.NameIndex];
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
