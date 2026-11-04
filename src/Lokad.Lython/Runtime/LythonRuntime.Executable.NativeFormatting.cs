using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Builder operations use the existing native-return instruction links. Value
    // expressions still use its scalar evaluator; no second formatting language.
    private static bool GuardNativeFormattedReturn(int index, ExecutableCodeObject code,
        ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments, ExecutionContext context)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        if (instruction.OpCode != ExecutableOpCode.ApplyOperation) return false;
        return code.Constants[instruction.ConstantIndex] switch
        {
            ExecutableStartFormattedString => true,
            ExecutableAppendFormattedText => GuardNativeFormattedReturn(node.First, code, instructions, arguments, context),
            ExecutableFormatField { Append: true } => GuardNativeFormattedReturn(node.First, code, instructions, arguments, context)
                && GuardNativeFormattedValue(node.Second, code, instructions, arguments, context),
            _ => false,
        };
    }

    private static bool GuardNativeFormattedValue(int index, ExecutableCodeObject code,
        ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments, ExecutionContext context)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        if (instruction.OpCode == ExecutableOpCode.ApplyOperation)
            return code.Constants[instruction.ConstantIndex] is ExecutableConvertFormattedValue
                or ExecutableFormatField { Append: false }
                && GuardNativeFormattedValue(node.First, code, instructions, arguments, context);

        // Only this formatting proof needs the actual field type. Ordinary
        // native subscript returns retain their existing single lookup.
        var shape = TryReadNativeFormattedValue(index, code, instructions, arguments, context, out var value)
            ? ReturnShape(value)
            : GuardNativeReturn(index, code, instructions, arguments, context, out var computed, out _) ? computed : NativeReturnShape.Other;
        return shape is NativeReturnShape.None or NativeReturnShape.Integer or NativeReturnShape.Float
            or NativeReturnShape.Boolean or NativeReturnShape.String or NativeReturnShape.Bytes;
    }

    private static bool TryReadNativeFormattedValue(int index, ExecutableCodeObject code,
        ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments, ExecutionContext context, out object? value)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        if (node.First < 0) return TryReadNativeOperand(code, instruction, arguments, context, out value);
        if (instruction.OpCode == ExecutableOpCode.Subscript
            && TryReadNativeFormattedValue(node.First, code, instructions, arguments, context, out var target)
            && target is PyDict dictionary
            && TryReadNativeFormattedValue(node.Second, code, instructions, arguments, context, out var key)
            && key is PyString text)
            return dictionary.TryReadNativeStringValue(text, out value);
        value = null;
        return false;
    }

    private static object EvaluateNativeFormattedReturn(ExecutableCodeObject code, ExecutableInstruction[] instructions,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        var root = code.NativeReturn!.Length - 1;
        var instruction = instructions[code.NativeReturn[root].Instruction];
        if (code.Constants[instruction.ConstantIndex] is not ExecutableFinishFormattedString)
            return EvaluateNativeFormattedValue(root, code, instructions, arguments, context, entryException);

        // Construction happens at the original start instruction, before any
        // field work. Its sole working owner also handles catchable failures.
        context.State.CheckExecution(instructions[0].Span);
        var output = new GovernedByteBuilder(context.MemoryGovernor, instructions[0].Span);
        try
        {
            AppendNativeFormattedReturn(code.NativeReturn[root].First, output, code, instructions, arguments, context, entryException);
            context.State.CheckExecution(instruction.Span);
            try { return output.ToPyStringAndRelease(); }
            catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
        }
        finally { output.Release(); }
    }

    private static void AppendNativeFormattedReturn(int index, GovernedByteBuilder output, ExecutableCodeObject code,
        ExecutableInstruction[] instructions, ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        var operation = code.Constants[instruction.ConstantIndex];
        if (operation is ExecutableStartFormattedString) return;
        AppendNativeFormattedReturn(node.First, output, code, instructions, arguments, context, entryException);
        var value = node.Second < 0 ? null : EvaluateNativeFormattedValue(node.Second, code, instructions, arguments, context, entryException);
        context.State.CheckExecution(instruction.Span);
        try
        {
            if (operation is ExecutableAppendFormattedText text) output.Append(text.Text);
            else
            {
                var field = (ExecutableFormatField)operation!;
                AppendInterpolatedStringPart(output, value!, null, field.FormatSpecifier, context, instruction.Span, field.PreparedFormat);
            }
        }
        catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
    }

    private static object EvaluateNativeFormattedValue(int index, ExecutableCodeObject code, ExecutableInstruction[] instructions,
        ReadOnlySpan<object?> arguments, ExecutionContext context, PyException? entryException)
    {
        var node = code.NativeReturn![index];
        var instruction = instructions[node.Instruction];
        if (instruction.OpCode != ExecutableOpCode.ApplyOperation)
            return EvaluateNativeReturn(index, code, instructions, arguments, context, entryException);
        var value = EvaluateNativeFormattedValue(node.First, code, instructions, arguments, context, entryException);
        context.State.CheckExecution(instruction.Span);
        try
        {
            return code.Constants[instruction.ConstantIndex] switch
            {
                ExecutableConvertFormattedValue conversion => FormatInterpolatedStringPart(value, conversion.Conversion, null, context, instruction.Span),
                ExecutableFormatField field => FormatInterpolatedStringPart(value, null, field.FormatSpecifier, context, instruction.Span, field.PreparedFormat),
                _ => throw new InvalidOperationException("Invalid native formatted value."),
            };
        }
        catch (LythonRuntimeException error) { RestoreNativeReturnException(context, entryException, error); throw; }
    }
}
