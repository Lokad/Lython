using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private static bool TryExecuteDictionaryAccumulation(ref ExecutableWorkState execution, ExecutionContext context,
            ExecutableCallSite call, int start, PyDict dictionary, PyString key)
        {
            ref readonly var add = ref execution.Code.Instructions[execution.InstructionIndex + 1];
            ref readonly var store = ref execution.Code.Instructions[execution.InstructionIndex + 2];
            // Future inputs are pure locals/constants. A missing or different
            // value leaves the original instructions to preserve read errors,
            // source spans and all dynamic evaluation/assignment behavior.
            if (!ReferenceEquals(execution.Locals[store.SecondInput], dictionary)
                || !ReferenceEquals(execution.Locals[store.ThirdInput], key)) return false;
            var source = add.SecondInput;
            var increment = source < 0 ? execution.Code.Constants[~source] : execution.Locals[source];
            if (increment is not BigInteger integer || integer.GetByteCount() >= 8) return false;
            var fallback = call.ArgumentCount == 2 ? execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
            var storeSpan = (LythonSourceSpan)execution.Code.Constants[store.TargetSpanIndex]!;
            context.EnterInterpreterFrame(call.CallSpan);
            try
            {
                if (!dictionary.TryAccumulateWithoutAdmission(key, fallback, integer, context,
                    call.CallSpan, add.Span, storeSpan)) return false;
            }
            finally { context.LeaveInterpreterFrame(); }
            // Only the plain receiver/target/key/default inputs are retired.
            // Outer iteration and its ownership stay below this result slot.
            execution.Stack.RetireTo(store.LiveExtent);
            execution.InstructionIndex += 2;
            return true;
        }
    }
}
