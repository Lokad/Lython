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

    internal sealed partial class ExecutionContext
    {
        private static bool TryExecuteCasedSplit(ref ExecutableWorkState execution, ExecutionContext context,
            ExecutionContext? invocation, ExecutableCallSite call, int start, object target)
        {
            if (call.CasedSplit is not { } recipe || !ReferenceEquals(target, recipe.CasingMethod)
                || execution.Stack[start] is not PyString receiver) return false;
            // The immediate chain stays within this basic block. Original
            // operand layout gives both calls the same result position; publish
            // through the final call, including its local-store continuation.
            try
            {
                object result;
                context.EnterInterpreterFrame(recipe.CasingCallSpan);
                try { result = ExecuteCasedSplit(recipe, receiver, context, call.ConsumedByIteration); }
                catch (Exception error) when (TryTranslateCallableException(error, recipe.SplitCallSpan, out var translated)) { throw translated; }
                finally { context.LeaveInterpreterFrame(); }
                execution.InstructionIndex += 2;
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
        }
    }
}
