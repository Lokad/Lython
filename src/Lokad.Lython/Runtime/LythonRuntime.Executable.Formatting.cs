using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool IsImmediateFormattedValue(object value)
        => value is PyNone or BigInteger or double or bool or PyString or PyBytes;

    private static string? GetExecutableFormatSpecifier(ExecutableFormatField field, object? dynamicSpecifier)
        => field.DynamicSpecifier ? ((PyString)dynamicSpecifier!).AsString() : field.FormatSpecifier;

    // The two storage views consume their own operands before entering this one
    // native operation. Protocol values transfer to the ordinary frame first.
    private static object? ExecuteBuiltinFormattedInstruction(in ExecutableInstruction instruction,
        GovernedByteBuilder? output, object? value, string? specifier, ExecutableFormatField? field,
        ExecutionContext context)
    {
        switch (instruction.OpCode)
        {
            case ExecutableOpCode.StartFormattedString:
                return new GovernedByteBuilder(context.MemoryGovernor, instruction.Span,
                    instruction.FormattedInitialCapacity, context.State.CancellationToken);
            case ExecutableOpCode.AppendFormattedText:
                output!.Append((PyString)value!);
                return null;
            case ExecutableOpCode.FinishFormattedString:
                return output!.ToPyStringAndRelease();
            case ExecutableOpCode.ConvertFormattedValue:
                return FormatInterpolatedStringPart(value!, instruction.FormattedConversion, null, context, instruction.Span);
            case ExecutableOpCode.FormatField:
                if (!field!.Append)
                    return FormatInterpolatedStringPart(value!, null, specifier, context, instruction.Span, field.PreparedFormat);
                AppendInterpolatedStringPart(output!, value!, null, specifier, context, instruction.Span, field.PreparedFormat);
                return null;
            default: throw new InvalidOperationException("Unknown formatted instruction.");
        }
    }
}
