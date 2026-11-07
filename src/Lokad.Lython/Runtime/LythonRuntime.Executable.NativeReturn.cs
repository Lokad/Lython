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

    internal static bool TryExecuteNativeModuleFunction(ExecutableCodeObject code, ExecutionContext module,
        ReadOnlySpan<object?> arguments, string name, LythonSourceSpan span, out object result)
    {
        result = PyNone.Instance;
        var body = code.NativeBody!;
        if (!GuardNativeBody(code, body, arguments, module)) return false;
        module.EnterFunctionCall(span);
        try { result = ExecuteNativeBody(code, body, module, arguments, span); return true; }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, module, name, span);
            throw;
        }
        finally { module.LeaveFunctionCall(); }
    }

    // Dynamic checks precede all effects, including admission. A failed proof
    // uses the ordinary executable engine; actual work never falls back/retries.
    private static bool GuardNativeBody(ExecutableCodeObject code, ExecutableNativeBody body,
        ReadOnlySpan<object?> arguments, ExecutionContext context)
        => body.Root < 0 || GuardNativeReturn(body.Root, code, body.Values, arguments, context, out _);

    private static object ExecuteNativeBody(ExecutableCodeObject code, ExecutableNativeBody body,
        ExecutionContext context, ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan)
    {
        context.EnterInterpreterFrame(entrySpan);
        try
        {
            object result = PyNone.Instance;
            var entryException = context.State.CurrentException;
            if (body.Root < 0) context.State.CheckExecution(code.Instructions[0].Span);
            else
            {
                result = EvaluateNativeReturn(body.Root, code, body.Values, arguments, context, entryException);
                context.State.CheckExecution(code.Instructions[^1].Span);
            }
            context.State.SetCurrentException(entryException);
            return result;
        }
        finally { context.LeaveInterpreterFrame(); }
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

    private static bool GuardNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, out NativeReturnShape shape)
    {
        ref readonly var value = ref values[index];
        shape = NativeReturnShape.Other;
        switch (value.Operation)
        {
            case NativeReturnOperation.Read:
                if (!TryReadNativeOperand(code, code.Instructions[value.SourceInstruction], arguments, context, out var input)) return false;
                shape = ReturnShape(input);
                return true;
            case NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue:
                shape = NativeReturnShape.String;
                return GuardNativeFormattedValue(value.First, code, values, arguments, context);
            case NativeReturnOperation.BuiltinUnary:
                ref readonly var targetValue = ref values[value.First];
                if (!TryReadNativeOperand(code, code.Instructions[targetValue.SourceInstruction], arguments, context, out var target)
                    || target is not BuiltinCallable) return false;
                var call = code.CallSites[code.Instructions[value.SourceInstruction].CallSiteIndex];
                var argumentIndex = call.NativeArgumentIndex(0);
                ref readonly var argument = ref values[argumentIndex];
                object? actual = null;
                var inputShape = NativeReturnShape.Other;
                if (argument.Operation == NativeReturnOperation.Read)
                {
                    if (!TryReadNativeOperand(code, code.Instructions[argument.SourceInstruction], arguments, context, out actual)) return false;
                }
                else if (!GuardNativeReturn(argumentIndex, code, values, arguments, context, out inputShape)) return false;
                return SelectNativeUnaryOperation(target, actual, inputShape, out shape) is not null && shape != NativeReturnShape.Other;
        }

        if (!GuardNativeReturn(value.First, code, values, arguments, context, out var first)) return false;
        switch (value.Operation)
        {
            case NativeReturnOperation.Add:
                if (!GuardNativeReturn(value.Second, code, values, arguments, context, out var right)) return false;
                shape = first;
                return first == right && first is NativeReturnShape.Integer or NativeReturnShape.String;
            case NativeReturnOperation.Subscript:
                return GuardNativeReturn(value.Second, code, values, arguments, context, out var key)
                    && first == NativeReturnShape.Dictionary && key == NativeReturnShape.String;
            case >= NativeReturnOperation.Strip and <= NativeReturnOperation.IsSpace:
                shape = value.Operation <= NativeReturnOperation.Upper ? NativeReturnShape.String : NativeReturnShape.Boolean;
                return first == NativeReturnShape.String;
            case NativeReturnOperation.SplitLowerWhitespace or NativeReturnOperation.SplitUpperWhitespace:
                shape = NativeReturnShape.Sized;
                return first == NativeReturnShape.String;
        }

        var site = code.CallSites[code.Instructions[value.SourceInstruction].CallSiteIndex];
        var second = NativeReturnShape.None;
        var third = NativeReturnShape.None;
        var fourth = NativeReturnShape.None;
        if (site.NativeArgumentIndex(0) is >= 0 and var secondIndex
            && !GuardNativeReturn(secondIndex, code, values, arguments, context, out second)) return false;
        if (site.NativeArgumentIndex(1) is >= 0 and var thirdIndex
            && !GuardNativeReturn(thirdIndex, code, values, arguments, context, out third)) return false;
        if (site.NativeArgumentIndex(2) is >= 0 and var fourthIndex
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
        ref readonly var value = ref values[index];
        if (value.Operation is NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue)
            return GuardNativeFormattedValue(value.First, code, values, arguments, context);
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
        ref readonly var value = ref values[index];
        if (value.Operation == NativeReturnOperation.Read)
            return TryReadNativeOperand(code, code.Instructions[value.SourceInstruction], arguments, context, out result);
        if (value.Operation == NativeReturnOperation.Subscript
            && TryReadNativeFormattedValue(value.First, code, values, arguments, context, out var target)
            && target is PyDict dictionary
            && TryReadNativeFormattedValue(value.Second, code, values, arguments, context, out var key)
            && key is PyString text)
            return dictionary.TryReadNativeStringValue(text, context, code.Instructions[value.SourceInstruction].Span, out result);
        result = null;
        return false;
    }

    private static object EvaluateNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        ref readonly var value = ref values[index];
        ref readonly var instruction = ref code.Instructions[value.SourceInstruction];
        object? first = null, second = null, third = null, fourth = null;
        if (value.Operation != NativeReturnOperation.Read)
        {
            if (value.Operation is NativeReturnOperation.SplitLowerWhitespace or NativeReturnOperation.SplitUpperWhitespace)
            {
                ref readonly var producer = ref values[value.First];
                first = EvaluateNativeReturn(producer.First, code, values, arguments, context, entryException);
                context.State.CheckExecution(code.Instructions[producer.Second].Span);
                context.State.CheckExecution(code.CallSites[code.Instructions[producer.SourceInstruction].CallSiteIndex].CallSpan);
            }
            else first = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            if (value.Operation is NativeReturnOperation.Add or NativeReturnOperation.Subscript)
                second = EvaluateNativeReturn(value.Second, code, values, arguments, context, entryException);
            else if (value.Operation is >= NativeReturnOperation.BuiltinUnary and <= NativeReturnOperation.SplitUpperWhitespace)
            {
                if (value.Second >= 0) context.State.CheckExecution(code.Instructions[value.Second].Span);
                var call = code.CallSites[instruction.CallSiteIndex];
                if (call.NativeArgumentIndex(0) is >= 0 and var a) second = EvaluateNativeReturn(a, code, values, arguments, context, entryException);
                if (call.NativeArgumentIndex(1) is >= 0 and var b) third = EvaluateNativeReturn(b, code, values, arguments, context, entryException);
                if (call.NativeArgumentIndex(2) is >= 0 and var c) fourth = EvaluateNativeReturn(c, code, values, arguments, context, entryException);
            }
        }
        context.State.CheckExecution(instruction.Span);
        try
        {
            switch (value.Operation)
            {
                case NativeReturnOperation.Read:
                    TryReadNativeOperand(code, instruction, arguments, context, out var operand);
                    return RuntimeValue(operand);
                case NativeReturnOperation.Add:
                    return EvaluateBinaryOperator(BinaryOperatorSyntax.Add, first!, second!, context, instruction.Span);
                case NativeReturnOperation.Subscript:
                    return ReadSubscriptValue(first!, second!, instruction.Span, context);
                case NativeReturnOperation.ConvertFormat:
                    var conversion = (ExecutableConvertFormattedValue)code.Constants[instruction.ConstantIndex]!;
                    return FormatInterpolatedStringPart(first!, conversion.Conversion, null, context, instruction.Span);
                case NativeReturnOperation.FormatValue:
                    var format = (ExecutableFormatField)code.Constants[instruction.ConstantIndex]!;
                    return FormatInterpolatedStringPart(first!, null, format.FormatSpecifier, context, instruction.Span, format.PreparedFormat);
            }
            var site = code.CallSites[instruction.CallSiteIndex];
            if (value.Operation is NativeReturnOperation.SplitLowerWhitespace or NativeReturnOperation.SplitUpperWhitespace)
                return PyStringOps.SplitCasedWhitespace((PyString)first!, value.Operation == NativeReturnOperation.SplitUpperWhitespace,
                    context.MemoryGovernor, site.CallSpan, context);
            if (value.Operation == NativeReturnOperation.BuiltinUnary)
                return InvokeSingleInput(second!, SelectNativeUnaryOperation(first, second, NativeReturnShape.Other, out _)!, site.CallSpan, context);
            var receiver = (PyString)first!;
            if (value.Operation is >= NativeReturnOperation.Strip and <= NativeReturnOperation.IsSpace)
                return InvokeSingleInput(receiver, StringMembers.GetNoArgumentsOperation(value.Operation), site.CallSpan, context);
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
