using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ExecuteCasedSplit(ExecutableCodeObject code, int index, PyString receiver,
        ExecutionContext context, bool returnConsumedByIteration)
    {
        var recipe = (ExecutableCasedSplit)code.CallSites[code.Instructions[index + 1].CallSiteIndex].SplitPrefix!;
        var resultCall = code.CallSites[code.Instructions[index + 3].CallSiteIndex];
        context.EnterInterpreterFrame(recipe.CasingCallSpan);
        try
        {
            context.CheckExecution(recipe.CasingMemberSpan);
            context.CheckExecution(recipe.CasingCallSpan);
            context.CheckExecution(recipe.SplitMemberSpan);
            context.CheckExecution(recipe.SplitCallSpan);
            return resultCall.ConsumedByIteration || returnConsumedByIteration && resultCall.ReturnedDirectly
                ? PyStringOps.PrepareCasedWhitespaceIteration(receiver, recipe.Upper, context, recipe.SplitCallSpan)
                : PyStringOps.SplitCasedWhitespace(receiver, recipe.Upper,
                    context.MemoryGovernor, recipe.SplitCallSpan, context);
        }
        catch (Exception error) when (TryTranslateCallableException(error, recipe.SplitCallSpan, out var translated)) { throw translated; }
        finally { context.LeaveInterpreterFrame(); }
    }

    private static bool TryReadAsciiTrim(ExecutableCallSite call, object target, ReadOnlySpan<ExecutableSlot> values,
        int start, [NotNullWhen(true)] out PyString? text, out byte trim)
    {
        text = null;
        trim = default;
        if (!ReferenceEquals(target, StringMembers.RStripMethod) ||
            values[start].Reference is not PyString input ||
            values[start + call.ArgumentOffset].Reference is not PyString { ByteLength: 1 } chars) return false;
        text = input;
        trim = chars.Utf8Span[0];
        return true;
    }

    private static PyList? ExecuteTrimmedSplit(ExecutableTrimmedSplit recipe, PyString text, byte trim,
        LythonSourceSpan trimSpan, LythonSourceSpan splitSpan, Span<ExecutableSlot> unpacked, ExecutionContext context)
    {
        int end;
        context.EnterInterpreterFrame(trimSpan);
        try { end = PyStringOps.FindAsciiRightTrimEnd(text, trim, context, trimSpan); }
        finally { context.LeaveInterpreterFrame(); }

        context.CheckExecution(recipe.MemberSpan);
        context.CheckExecution(recipe.SeparatorSpan);
        context.EnterInterpreterFrame(splitSpan);
        try { return PyStringOps.SplitUtf8Prefix(text, end, recipe.Separator, unpacked, context, splitSpan); }
        finally { context.LeaveInterpreterFrame(); }
    }

    internal sealed partial class ExecutionContext
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryExecuteCasedSplitMember(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            // The fallback can reread the original operand. This guard neither
            // invokes a method nor changes the receiver's slot.
            if (ReadOperand(ref execution, instruction.InputStart, scope, 0) is not PyString receiver) return false;
            // The immediate chain stays within this basic block. Original
            // operand layout gives both calls the same result position; publish
            // through the final call, including its local-store continuation.
            try
            {
                var result = ExecuteCasedSplit(execution.Code, execution.InstructionIndex, receiver, invocation ?? scope,
                    execution.ReturnConsumedByIteration);
                execution.InstructionIndex += 3;
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }

        private static bool TryExecuteTrimmedSplit(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, ExecutableTrimmedSplit recipe, ExecutableCallSite call, int start, object target)
        {
            if (!TryReadAsciiTrim(call, target, execution.Stack.Values, start, out var text, out var trim)) return false;
            var finalIndex = execution.InstructionIndex + 3;
            ref readonly var final = ref execution.Code.Instructions[finalIndex];
            if (final.InputStart != start) return false;
            var split = execution.Code.CallSites[final.CallSiteIndex];
            var unpacked = PrepareUnpackedCallReturn(ref execution, split, start, out var originalExtent);
            var list = ExecuteTrimmedSplit(recipe, text, trim, call.CallSpan, split.CallSpan, unpacked, invocation ?? scope);
            execution.InstructionIndex = finalIndex;
            PublishSplitResult(ref execution, invocation, scope, start, unpacked.Length, originalExtent, list);
            return true;
        }
    }
}
