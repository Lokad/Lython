using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool TryConsumePrivateStringResult(ReceiverMethod method, ExecutableCodeObject code,
        ReadOnlySpan<ExecutableSlot> values, ref int index, ExecutableCallSite producer, int start,
        ExecutionContext context, out ExecutableSlot result)
    {
        result = default;
        if (ReferenceEquals(method, ReceiverMethods.StringSplit))
        {
            if (TryJoinTrimmedSplit(code, values, index, producer, start, context) is not { } joined) return false;
            index += 2;
            result.Object = joined;
            return true;
        }

        if (producer.ArgumentCount != 0 || !ReferenceEquals(method, StringMembers.SplitLinesMethod)
            || values[start].Reference is not PyString source || index + 1 >= code.Instructions.Length)
            return false;

        ref readonly var next = ref code.Instructions[index + 1];
        if (next.OpCode != ExecutableOpCode.Call || next.InputStart < 0) return false;
        var consumer = code.CallSites[next.CallSiteIndex];
        if (consumer.HasReceiver || !consumer.IsPositionalOnly || consumer.ArgumentCount != 1
            || next.InputStart + consumer.TargetOffset >= start
            || next.InputStart + consumer.ArgumentOffset != code.Instructions[index].ResultSlot
            || values[next.InputStart + consumer.TargetOffset].Reference is not { } target
            || !ExecutionContext.IsLengthBuiltin(target))
            return false;

        int length;
        context.EnterInterpreterFrame(producer.CallSpan);
        try
        {
            context.CheckExecution(producer.CallSpan);
            length = PyStringOps.CountSplitLines(source.Utf8Span, context, producer.CallSpan, out _, out _);
        }
        finally { context.LeaveInterpreterFrame(); }

        // The list is private to this exact len call. Preserve its entry
        // checkpoint and admission even though the result is already known.
        context.EnterInterpreterFrame(consumer.CallSpan);
        try { context.CheckExecution(consumer.CallSpan); }
        finally { context.LeaveInterpreterFrame(); }
        result = ExecutableSlot.FromInteger(length);
        index++;
        return true;
    }

    private static PyString? TryJoinTrimmedSplit(ExecutableCodeObject code, ReadOnlySpan<ExecutableSlot> values,
        int index, ExecutableCallSite split, int start, ExecutionContext context)
    {
        if (split.ArgumentCount != 1 || index + 2 >= code.Instructions.Length
            || values[start].Reference is not PyString source
            || values[start + split.ArgumentOffset].Reference is not PyString { ByteLength: > 0 } delimiter)
            return null;

        ref readonly var create = ref code.Instructions[index + 1];
        if (create.OpCode != ExecutableOpCode.ApplyOperation
            || create.InputStart != code.Instructions[index].ResultSlot
            || code.Constants[create.ConstantIndex] is not ExecutableCreateGeneratorExpression { JoinedTrimmedSplit: true })
            return null;

        ref readonly var joinInstruction = ref code.Instructions[index + 2];
        if (joinInstruction.OpCode != ExecutableOpCode.Call) return null;
        var join = code.CallSites[joinInstruction.CallSiteIndex];
        var joinStart = joinInstruction.InputStart;
        if (!join.HasReceiver || !join.IsPositionalOnly || join.ArgumentCount != 1
            || joinStart < 0 || joinStart + join.TargetOffset >= start
            || create.ResultSlot != joinStart + join.ArgumentOffset
            || !ReferenceEquals(values[joinStart + join.TargetOffset].Reference, ReceiverMethods.StringJoin)
            || values[joinStart].Reference is not PyString separator)
            return null;

        // Both receivers and the evaluated delimiter are immutable. The fused
        // operation can cross host admission without re-reading operands.
        context.CheckExecution(split.CallSpan);
        context.EnterInterpreterFrame(join.CallSpan);
        try
        {
            return PyStringOps.JoinTrimmedSplit(source, delimiter, separator, context, join.CallSpan);
        }
        catch (Exception error) when (TryTranslateCallableException(error, join.CallSpan, out var translated))
        {
            throw translated;
        }
        finally { context.LeaveInterpreterFrame(); }
    }
}
