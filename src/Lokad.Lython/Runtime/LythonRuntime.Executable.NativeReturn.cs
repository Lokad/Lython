using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One non-owning input convention for native proof/evaluation. Parameters
    // keep their real bounds; cells remain live in the definition-owned array.
    // The readonly stack view cannot escape or cross a suspension boundary.
    private readonly ref struct NativeInputs(ReadOnlySpan<object?> values, IReadOnlyList<ExecutableCell>? cells)
    {
        internal readonly ReadOnlySpan<object?> Values = values;
        internal readonly IReadOnlyList<ExecutableCell>? Cells = cells;
    }

    internal static bool TryExecuteNativeReturn(ExecutableCodeObject codeObject, ExecutionContext context,
        ReadOnlySpan<object?> arguments, LythonSourceSpan entrySpan, out object result, bool consumedByIteration = false)
    {
        result = PyNone.Instance;
        var body = codeObject.NativeBody;
        if (body is null || body.ClosureInputCount != 0 || context.MirrorLocalStores || context.CurrentExecutableFrame is not null
            || context.ActiveHandlerVariables is { Count: > 0 })
            return false;
        var inputs = new NativeInputs(arguments, null);
        if (!GuardNativeBody(codeObject, body, inputs, context)) return false;
        result = ExecuteNativeBody(codeObject, body, context, inputs, entrySpan, consumedByIteration);
        return true;
    }

    internal static bool TryExecuteNativeFunction(ExecutableCodeObject code, ExecutionContext context,
        ReadOnlySpan<object?> arguments, IReadOnlyList<ExecutableCell> cells, string name, LythonSourceSpan span, out object result,
        bool consumedByIteration = false)
    {
        result = PyNone.Instance;
        var body = code.NativeBody!;
        var inputs = new NativeInputs(arguments, cells);
        if (!GuardNativeBody(code, body, inputs, context)) return false;
        context.EnterFunctionCall(span);
        try { result = ExecuteNativeBody(code, body, context, inputs, span, consumedByIteration); return true; }
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
        in NativeInputs arguments, ExecutionContext context)
        => (body.ClosureInputCount == 0 || arguments.Cells?.Count == body.ClosureInputCount) && (body.Root < 0 || GuardNativeReturn(body.Root + code.FirstTemporarySlot, code, body.Values, arguments, context, out _));

    private static object ExecuteNativeBody(ExecutableCodeObject code, ExecutableNativeBody body,
        ExecutionContext context, in NativeInputs arguments, LythonSourceSpan entrySpan,
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
        in NativeInputs arguments, ExecutionContext context, out object? value)
    {
        if (source < 0) { value = code.Constants[~source]; return true; }
        if (source < code.FirstTemporarySlot)
        {
            var bound = (uint)source < (uint)arguments.Values.Length;
            value = bound ? arguments.Values[source] : null;
            return bound;
        }
        return TryReadNativeOperand(code, values[source - code.FirstTemporarySlot], arguments, context, out value);
    }

    private static bool GuardNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        in NativeInputs arguments, ExecutionContext context, out NativeReturnShape shape)
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
            case NativeReturnOperation.EmptyList or NativeReturnOperation.EmptyTuple:
                shape = NativeReturnShape.Sized;
                return true;
            case NativeReturnOperation.Conditional:
                context.State.CheckExecution(value.Span);
                if (!GuardNativeReturn(value.First, code, values, arguments, context, out var condition)
                    || condition == NativeReturnShape.Other
                    || !GuardNativeReturn(value.Second, code, values, arguments, context, out var consequent)
                    || !GuardNativeReturn(value.Operand, code, values, arguments, context, out var alternative))
                    return false;
                shape = consequent == alternative ? consequent : NativeReturnShape.Other;
                return true;
            case >= NativeReturnOperation.ReadConstant and <= NativeReturnOperation.ReadClosure:
                if (!TryReadNativeOperand(code, value, arguments, context, out var input)) return false;
                shape = ReturnShape(input);
                return true;
            case NativeReturnOperation.StoreClosure:
                var slot = value.Operand;
                return TryReadNativeCell(code, arguments, slot, out var cell)
                    && cell.CanStoreThroughOwner
                    && GuardNativeReturn(value.First, code, values, arguments, context, out _)
                    && GuardNativeReturn(value.Second, code, values, arguments, context, out shape);
            case NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue:
                shape = NativeReturnShape.String;
                return GuardNativeFormattedValue(value.First, code, values, arguments, context);
            case NativeReturnOperation.BuiltinUnary:
                if (!TryReadNativeInput(value.First, code, values, arguments, context, out var target)
                    || target is not BuiltinCallable) return false;
                var call = code.CallSites[value.Operand];
                var argumentIndex = call.NativeArgumentSource(0);
                object? actual = null;
                var inputShape = NativeReturnShape.Other;
                if (argumentIndex < code.FirstTemporarySlot || values[argumentIndex - code.FirstTemporarySlot].IsRead)
                {
                    if (!TryReadNativeInput(argumentIndex, code, values, arguments, context, out actual)) return false;
                }
                else if (!GuardNativeReturn(argumentIndex, code, values, arguments, context, out inputShape)) return false;
                return SelectNativeUnaryOperation(target, actual, inputShape, out shape) is not null && shape != NativeReturnShape.Other;
        }

        if (!GuardNativeReturn(value.First, code, values, arguments, context, out var first)) return false;
        switch (value.Operation)
        {
            case NativeReturnOperation.Binary or NativeReturnOperation.Augmented:
                if (!GuardNativeReturn(value.Second, code, values, arguments, context, out var right)) return false;
                if (value.Operation == NativeReturnOperation.Augmented &&
                    (first is not (NativeReturnShape.Integer or NativeReturnShape.Float or NativeReturnShape.Boolean)
                    || right is not (NativeReturnShape.Integer or NativeReturnShape.Float or NativeReturnShape.Boolean))) return false;
                return TryGetNativeBinaryShape(value.Operation == NativeReturnOperation.Augmented
                    ? AugmentedBinaryOperator((AugmentedAssignmentOperatorSyntax)value.Operand) : (BinaryOperatorSyntax)value.Operand,
                    first, right, out shape);
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

        var site = code.CallSites[value.Operand];
        var second = NativeReturnShape.None;
        var third = NativeReturnShape.None;
        var fourth = NativeReturnShape.None;
        if (site.NativeArgumentSource(0) is not int.MaxValue and var secondIndex
            && !GuardNativeReturn(secondIndex, code, values, arguments, context, out second)) return false;
        if (site.NativeArgumentSource(1) is not int.MaxValue and var thirdIndex
            && !GuardNativeReturn(thirdIndex, code, values, arguments, context, out third)) return false;
        if (site.NativeArgumentSource(2) is not int.MaxValue and var fourthIndex
            && !GuardNativeReturn(fourthIndex, code, values, arguments, context, out fourth)) return false;
        return TryGetNativeStringCallShape(value.Operation, first, second, third, fourth, out shape);
    }

    private static bool GuardNativeFormattedValue(int index, ExecutableCodeObject code,
        NativeReturnValue[] values, in NativeInputs arguments, ExecutionContext context)
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
        NativeReturnValue[] values, in NativeInputs arguments, ExecutionContext context, out object? result)
    {
        if (index < code.FirstTemporarySlot)
            return TryReadNativeInput(index, code, values, arguments, context, out result);
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        if (value.IsRead)
            return TryReadNativeOperand(code, value, arguments, context, out result);
        if (value.Operation == NativeReturnOperation.Subscript
            && TryReadNativeFormattedValue(value.First, code, values, arguments, context, out var target)
            && target is PyDict dictionary
            && TryReadNativeFormattedValue(value.Second, code, values, arguments, context, out var key)
            && key is PyString text)
            return dictionary.TryReadNativeStringValue(text, context, value.Span, out result);
        result = null;
        return false;
    }

    private static object EvaluateNativeReturn(int index, ExecutableCodeObject code, NativeReturnValue[] values,
        in NativeInputs arguments, ExecutionContext context, PyException? entryException,
        bool consumedByIteration = false)
    {
        // Bound direct inputs are pure. Operations and real roots retain their
        // cancellation/refusal checks without a recursive leaf dispatch.
        if (index < code.FirstTemporarySlot)
            return RuntimeValue(index < 0 ? code.Constants[~index] : arguments.Values[index]);
        ref readonly var value = ref values[index - code.FirstTemporarySlot];
        if (value.Operation == NativeReturnOperation.StoreClosure)
        {
            var stored = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            context.State.CheckExecution(value.Span);
            arguments.Cells![value.Operand].Value = stored;
            var result = EvaluateNativeReturn(value.Second, code, values, arguments, context, entryException,
                consumedByIteration);
            CheckNativeReturnExit(value.Second - code.FirstTemporarySlot, values, context);
            return result;
        }
        if (value.Operation == NativeReturnOperation.Conditional)
        {
            var condition = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            context.State.CheckExecution(value.Span);
            var selected = PyTruthiness.IsTruthy(condition) ? value.Second : value.Operand;
            var result = EvaluateNativeReturn(selected, code, values, arguments, context, entryException, consumedByIteration);
            CheckNativeReturnExit(selected - code.FirstTemporarySlot, values, context);
            return result;
        }
        object? first = null, second = null, third = null, fourth = null;
        if (!value.IsRead && value.Operation is not (NativeReturnOperation.None or NativeReturnOperation.EmptyList or NativeReturnOperation.EmptyTuple))
        {
            first = EvaluateNativeReturn(value.First, code, values, arguments, context, entryException);
            if (value.Operation == NativeReturnOperation.CasedSplit)
            {
                try { return ExecuteCasedSplit(code.CallSites[value.Operand].CasedSplit!, (PyString)first,
                    context, consumedByIteration); }
                catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
            }
            if (value.Operation is NativeReturnOperation.Binary or NativeReturnOperation.Augmented or NativeReturnOperation.Subscript)
                second = EvaluateNativeReturn(value.Second, code, values, arguments, context, entryException);
            else if (value.Operation is >= NativeReturnOperation.BuiltinUnary and <= NativeReturnOperation.EndsWith)
            {
                if (value.MemberSpan is { } memberSpan) context.State.CheckExecution(memberSpan);
                var call = code.CallSites[value.Operand];
                if (call.NativeArgumentSource(0) is not int.MaxValue and var a) second = EvaluateNativeReturn(a, code, values, arguments, context, entryException);
                if (call.NativeArgumentSource(1) is not int.MaxValue and var b) third = EvaluateNativeReturn(b, code, values, arguments, context, entryException);
                if (call.NativeArgumentSource(2) is not int.MaxValue and var c) fourth = EvaluateNativeReturn(c, code, values, arguments, context, entryException);
            }
        }
        context.State.CheckExecution(value.Span);
        try
        {
            switch (value.Operation)
            {
                case NativeReturnOperation.None:
                    return PyNone.Instance;
                case NativeReturnOperation.EmptyList or NativeReturnOperation.EmptyTuple:
                    return value.Operation == NativeReturnOperation.EmptyTuple ? PyTuple.Empty
                        : CreateEmptyNativeList(value.Span, context);
                case >= NativeReturnOperation.ReadConstant and <= NativeReturnOperation.ReadClosure:
                    TryReadNativeOperand(code, value, arguments, context, out var operand);
                    return RuntimeValue(operand);
                case NativeReturnOperation.Binary:
                    return EvaluateBinaryOperator((BinaryOperatorSyntax)value.Operand, first!, second!, context, value.Span);
                case NativeReturnOperation.Augmented:
                    // The whole-body proof accepts only exact builtin numeric
                    // operands. Preserve the existing in-place operator errors
                    // without repeating protocol selection after that proof.
                    return EvaluateAugmentedBuiltin(first!, second!, (AugmentedAssignmentOperatorSyntax)value.Operand, context, value.Span);
                case NativeReturnOperation.Subscript:
                    return ReadSubscriptValue(first!, second!, value.Span, context);
                case NativeReturnOperation.ConvertFormat:
                    return FormatInterpolatedStringPart(first!, (char)value.Operand, null, context, value.Span);
                case NativeReturnOperation.FormatValue:
                    var format = (ExecutableFormatField)code.Constants[value.Operand]!;
                    return FormatInterpolatedStringPart(first!, null, format.FormatSpecifier, context, value.Span, format.PreparedFormat);
            }
            var site = code.CallSites[value.Operand];
            if (value.Operation == NativeReturnOperation.BuiltinUnary)
                return InvokeSingleInput(second!, SelectNativeUnaryOperation(first, second, NativeReturnShape.Other, out _)!, site.CallSpan, context);
            var receiver = (PyString)first!;
            if (value.Method is { } method)
                return InvokeReceiverMethod(method, receiver, site.CallSpan, context);
            return InvokeNativeStringOperation(value.Operation, receiver, second, third, fourth, site.CallSpan, context);
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

    private static bool TryReadNativeOperand(ExecutableCodeObject codeObject, in NativeReturnValue source,
        in NativeInputs arguments, ExecutionContext context, out object? value)
    {
        if (source.Operation == NativeReturnOperation.ReadConstant)
        {
            value = codeObject.Constants[source.Operand];
            return true;
        }
        if (source.Operation == NativeReturnOperation.ReadGlobal)
        {
            var name = codeObject.Names[source.Operand];
            return TryResolveExecutableGlobal(name, context, out value);
        }
        if (source.Operation == NativeReturnOperation.ReadClosure)
        {
            var slot = source.Operand;
            value = TryReadNativeCell(codeObject, arguments, slot, out var cell) ? cell.Value : null;
            return value is not null;
        }
        if (source.Operation != NativeReturnOperation.ReadLocal)
            throw new InvalidOperationException($"Unexpected native input: {source.Operation}");
        var index = source.Operand;
        var bound = (uint)index < (uint)arguments.Values.Length;
        value = bound ? arguments.Values[index] : null;
        // Unbound locals must fail through the original LoadLocal operation.
        return bound;
    }

    // The compiled function owns a fixed cell collection, validated before any
    // proof/effects. Values remain live; no parameter slot can impersonate a cell.
    private static bool TryReadNativeCell(ExecutableCodeObject code, in NativeInputs arguments,
        int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
    {
        cell = (uint)slot < (uint)code.NativeBody!.ClosureInputCount ? arguments.Cells?[slot] : null;
        return cell is not null;
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
