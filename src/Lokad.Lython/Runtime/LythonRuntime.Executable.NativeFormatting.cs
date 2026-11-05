using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool GuardNativeFormattedBody(ExecutableCodeObject code,
        NativeReturnValue[] values, ReadOnlySpan<object?> arguments, ExecutionContext context)
    {
        // Compilation proves one root builder and orders every append. Only
        // the fields need dynamic proof; do not reconstruct the builder spine.
        foreach (ref readonly var value in values.AsSpan())
            if (value.Operation == NativeReturnOperation.AppendFormatField
                && !GuardNativeFormattedValue(value.Second, code, values, arguments, context)) return false;
        return true;
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
            return dictionary.TryReadNativeStringValue(text, out result);
        result = null;
        return false;
    }

    private static object EvaluateNativeFormattedReturn(ExecutableCodeObject code, NativeReturnValue[] values,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        ref readonly var root = ref values[code.NativeBody!.Root];
        context.State.CheckExecution(code.Instructions[0].Span);
        var start = (ExecutableStartFormattedString)code.Constants[code.Instructions[0].ConstantIndex]!;
        var output = new GovernedByteBuilder(context.MemoryGovernor, code.Instructions[0].Span, start.InitialCapacity,
            context.State.CancellationToken);
        try
        {
            // Native preparation permits one builder, no nested builder, and
            // one final result. Every append is already in original code order.
            foreach (ref readonly var value in values.AsSpan())
            {
                if (value.Operation is not (NativeReturnOperation.AppendFormatText or NativeReturnOperation.AppendFormatField)) continue;
                var fieldValue = value.Operation == NativeReturnOperation.AppendFormatField
                    ? EvaluateNativeReturn(value.Second, code, values, arguments, context, entryException) : null;
                ref readonly var instruction = ref code.Instructions[value.SourceInstruction];
                context.State.CheckExecution(instruction.Span);
                try
                {
                    if (value.Operation == NativeReturnOperation.AppendFormatText)
                        output.Append(((ExecutableAppendFormattedText)code.Constants[instruction.ConstantIndex]!).Text);
                    else
                    {
                        var field = (ExecutableFormatField)code.Constants[instruction.ConstantIndex]!;
                        AppendInterpolatedStringPart(output, fieldValue!, null, field.FormatSpecifier, context, instruction.Span, field.PreparedFormat);
                    }
                }
                catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
            }
            context.State.CheckExecution(code.Instructions[root.SourceInstruction].Span);
            try { return output.ToPyStringAndRelease(); }
            catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
        }
        finally { output.Release(); }
    }

}
