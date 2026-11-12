using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private static bool TryExecuteDictionaryLoop(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction head)
        {
            // The ordinary ForNext owns wider/pending stack retirement. A
            // native source advances here only after that ownership is settled.
            if (execution.Stack.LiveExtent != head.ResultSlot || execution.Stack.HasPendingDisposals) return false;
            var headIndex = execution.InstructionIndex;
            var callIndex = head.DictionaryLoopCallIndex;
            var call = execution.Code.CallSites[execution.Code.Instructions[callIndex].CallSiteIndex];
            ref readonly var member = ref execution.Code.Instructions[headIndex + 2];
            ref readonly var add = ref execution.Code.Instructions[callIndex + 1];
            ref readonly var store = ref execution.Code.Instructions[callIndex + 2];
            var fallback = execution.Code.Constants[execution.Code.Instructions[headIndex + 4].ConstantIndex]!;
            var increment = (BigInteger)execution.Code.Constants[~add.SecondInput]!;
            var storeSpan = (LythonSourceSpan)execution.Code.Constants[store.TargetSpanIndex]!;
            var totalIndex = callIndex + 3;
            var hasTotal = execution.Code.Instructions[totalIndex].OpCode == ExecutableOpCode.Augmented;
            ref readonly var total = ref execution.Code.Instructions[totalIndex];
            var totalIncrement = hasTotal ? (BigInteger)execution.Code.Constants[~total.SecondInput]! : BigInteger.Zero;
            // No callback can observe these uncaptured slots inside the native
            // segment. Observable namespaces retain their ordinary stores.
            var privateBindings = !execution.Code.RequiresLocalVariableMirroring
                && invocation?.MirrorLocalStores != true && invocation?.GlobalNamespace is null;
            var deferTotal = hasTotal && privateBindings;
            var cursor = privateBindings
                ? execution.Stack[head.InputStart] as PyStringOps.CasedWhitespaceIteration : null;
            if (cursor is not null && !ReferenceEquals(cursor.MemoryGovernor, scope.MemoryGovernor)) cursor = null;
            if (!TryReadDictionaryWord(ref execution, cursor, head.InputStart, out var word, out var hasWord)) return false;
            if (!hasWord)
            {
                execution.Stack.RetireTo(head.InputStart);
                execution.InstructionIndex = head.TargetIndex;
                return true;
            }
            var wordChanged = false;
            PyStringOps.CompactWord pendingWord = default;
            var totalChanged = false;
            long pendingTotal = 0;
            var exhausted = false;
            try
            {
                while (true)
                {
                    execution.InstructionIndex = headIndex;
                    var compact = word.Value is null;
                    if (compact)
                    {
                        // Keep the original binding check, even though its value
                        // stays private until a continuation can observe it.
                        execution.Stack.CheckExecution(execution.Code.Instructions[headIndex + 1].Span);
                        pendingWord = word;
                        wordChanged = true;
                    }
                    else
                    {
                        PublishInstructionResult(ref execution, invocation, head, word.Value!);
                        wordChanged = false;
                    }
                    // Binding may admit namespace storage and reenter. Read the
                    // actual locals afterwards, and stop a refused command first.
                    scope.CheckExecution(member.Span);
                    var dictionary = execution.Locals[store.SecondInput] as PyDict;
                    var key = compact ? null : execution.Locals[store.ThirdInput] as PyString;
                    var priorTotal = hasTotal ? execution.Locals[total.InputStart] : null;
                    if (dictionary is null || !compact && key is null ||
                        hasTotal && (priorTotal is not BigInteger integer || integer.GetByteCount() >= 8))
                    {
                        execution.InstructionIndex = headIndex + 2;
                        return true;
                    }

                    execution.InstructionIndex = callIndex;
                    scope.EnterInterpreterFrame(call.CallSpan);
                    bool updated;
                    try
                    {
                        updated = compact
                            ? dictionary.TryAccumulateInlineWord(PyStringOps.CompactWordBytes(ref word), fallback, increment, scope,
                                call.CallSpan, add.Span, storeSpan)
                            : dictionary.TryAccumulateWithoutAdmission(key!, fallback, increment, scope,
                                call.CallSpan, add.Span, storeSpan);
                    }
                    finally { scope.LeaveInterpreterFrame(); }
                    if (!updated)
                    {
                        // Only the loop binding happened. The original body now
                        // performs any required admission/protocol, exactly once.
                        execution.InstructionIndex = headIndex + 2;
                        return true;
                    }
                    execution.Stack.RetireTo(store.LiveExtent);
                    if (hasTotal)
                    {
                        execution.InstructionIndex = totalIndex;
                        scope.CheckExecution(total.Span);
                        if (deferTotal)
                        {
                            // Both operands fit seven signed bytes. Stop this
                            // segment before that bound changes; the ordinary
                            // arithmetic path keeps exact promotion and admission.
                            var value = (totalChanged ? pendingTotal : (long)(BigInteger)priorTotal!) + (long)totalIncrement;
                            scope.CheckExecution(execution.Code.Instructions[totalIndex + 1].Span);
                            pendingTotal = value;
                            totalChanged = true;
                            if (value < -(1L << 55) || value >= (1L << 55))
                            {
                                execution.InstructionIndex = totalIndex + 2;
                                return true;
                            }
                        }
                        else
                        {
                            var value = AddExactIntegers((BigInteger)priorTotal!, totalIncrement, scope.MemoryGovernor, total.Span);
                            PublishInstructionResult(ref execution, invocation, total, value);
                        }
                    }
                    execution.InstructionIndex = headIndex;
                    scope.CheckExecution(head.Span);
                    if (!TryReadDictionaryWord(ref execution, cursor, head.InputStart, out var nextWord, out hasWord))
                        throw new InvalidOperationException("A native loop lost its private iteration source.");
                    if (hasWord) { word = nextWord; continue; }
                    exhausted = true;
                    execution.InstructionIndex = head.TargetIndex;
                    return true;
                }
            }
            finally
            {
                // Publish before any continuation, callback or error handler.
                // This private scalar store cannot admit or invoke guest code;
                // it must not replace a cancellation or first refusal.
                try
                {
                    if (wordChanged) execution.Locals[store.ThirdInput] = cursor!.Materialize(ref pendingWord);
                }
                finally
                {
                    if (totalChanged) execution.Locals[total.InputStart] = new BigInteger(pendingTotal);
                    // Returning the prepared ranges can refund storage and
                    // reenter. Publish both Python bindings before that point.
                    if (exhausted) execution.Stack.RetireTo(head.InputStart);
                }
            }
        }

        private static bool TryReadDictionaryWord(ref ExecutableWorkState execution,
            PyStringOps.CasedWhitespaceIteration? cursor, int slot, out PyStringOps.CompactWord word, out bool hasWord)
        {
            if (cursor is not null) { hasWord = cursor.TryReadWord(out word); return true; }
            word = default;
            if (!execution.Stack.TryAdvanceNativeIteration(slot, out var next)) { hasWord = false; return false; }
            hasWord = next.HasValue;
            if (hasWord) word.Value = next.Value;
            return true;
        }

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
