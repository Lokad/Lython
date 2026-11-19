using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ExecuteCasedSplit(ExecutableCasedSplit recipe, PyString receiver, ExecutionContext context,
        bool consumedByIteration = false)
    {
        context.CheckExecution(recipe.CasingMemberSpan);
        context.CheckExecution(recipe.CasingCallSpan);
        context.CheckExecution(recipe.SplitMemberSpan);
        context.CheckExecution(recipe.SplitCallSpan);
        return consumedByIteration
            ? PyStringOps.PrepareCasedWhitespaceIteration(receiver, recipe.Upper, context, recipe.SplitCallSpan)
            : PyStringOps.SplitCasedWhitespace(receiver, recipe.Upper,
                context.MemoryGovernor, recipe.SplitCallSpan, context);
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
        private static bool TryExecuteCasedSplit(ref ExecutableWorkState execution, ExecutionContext context,
            ExecutionContext? invocation, ExecutableCasedSplit recipe, int start, object target)
        {
            if (!ReferenceEquals(target, recipe.CasingMethod)
                || execution.Stack[start] is not PyString receiver) return false;
            // Only the fused result inherits the split call's consumer. If the
            // exact native chain is declined, lower() has its own ordinary use
            // as the receiver of the following member access.
            var resultCall = execution.Code.CallSites[
                execution.Code.Instructions[execution.InstructionIndex + 2].CallSiteIndex];
            // The immediate chain stays within this basic block. Original
            // operand layout gives both calls the same result position; publish
            // through the final call, including its local-store continuation.
            try
            {
                object result;
                context.EnterInterpreterFrame(recipe.CasingCallSpan);
                try { result = ExecuteCasedSplit(recipe, receiver, context, CallResultConsumedByIteration(in execution, resultCall)); }
                catch (Exception error) when (TryTranslateCallableException(error, recipe.SplitCallSpan, out var translated)) { throw translated; }
                finally { context.LeaveInterpreterFrame(); }
                execution.InstructionIndex += 2;
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
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
