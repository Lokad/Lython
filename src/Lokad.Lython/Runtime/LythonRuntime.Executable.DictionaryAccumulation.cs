using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private const long HistogramTotalLimit = 1L << 55;

        private static bool TryReadHistogramTotal(in ExecutableSlot slot, out long value)
            => slot.TryGetInteger(out value) && value >= -HistogramTotalLimit && value < HistogramTotalLimit;

        private static bool TryExecuteDictionaryLoop(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction head)
        {
            // This segment can keep the word binding private only while all
            // updates are callback-free and the cursor owns its prepared bytes.
            if (execution.Stack.LiveExtent != head.ResultSlot || execution.Stack.HasPendingDisposals) return false;
            var privateBindings = !execution.Code.RequiresLocalVariableMirroring &&
                invocation?.MirrorLocalStores != true && invocation?.GlobalNamespace is null;
            if (!privateBindings ||
                execution.Stack[head.InputStart] is not PyStringOps.CasedWhitespaceIteration cursor ||
                !ReferenceEquals(cursor.MemoryGovernor, scope.MemoryGovernor))
                return TryExecuteOrdinaryDictionaryLoop(ref execution, scope, invocation, head, privateBindings);

            var headIndex = execution.InstructionIndex;
            var callIndex = head.DictionaryLoopCallIndex;
            var call = execution.Code.CallSites[execution.Code.Instructions[callIndex].CallSiteIndex];
            ref readonly var member = ref execution.Code.Instructions[headIndex + 2];
            ref readonly var add = ref execution.Code.Instructions[callIndex + 1];
            ref readonly var store = ref execution.Code.Instructions[callIndex + 2];
            var fallback = execution.Code.Constants[execution.Code.Instructions[headIndex + 4].ConstantIndex]!;
            var increment = (BigInteger)execution.Code.Constants[~add.SecondInput]!;

            if (execution.Locals[store.SecondInput].Object is not PyDict dictionary ||
                !dictionary.CanAccumulateBorrowedWords(scope)) return false;
            var totalIndex = callIndex + 3;
            ref readonly var total = ref execution.Code.Instructions[totalIndex];
            var hasTotal = total.OpCode == ExecutableOpCode.Augmented;
            long pendingTotal = 0;
            long totalIncrement = 0;
            if (hasTotal)
            {
                if (!TryReadHistogramTotal(execution.Locals[total.InputStart], out pendingTotal))
                    return false;
                totalIncrement = (long)(BigInteger)execution.Code.Constants[~total.SecondInput]!;
            }

            ReadOnlySpan<byte> pendingBytes = default;
            PyString? pendingWord = null;
            var totalChanged = false;
            var frameEntered = false;
            var exhausted = false;
            try
            {
                while (cursor.TryReadWord(out var bytes, out var word))
                {
                    pendingBytes = bytes;
                    pendingWord = word;
                    if (!frameEntered)
                    {
                        scope.CheckExecution(member.Span);
                        execution.InstructionIndex = callIndex;
                        scope.EnterInterpreterFrame(call.CallSpan);
                        frameEntered = true;
                    }
                    else execution.InstructionIndex = callIndex;

                    bool updated;
                    if (word is null)
                    {
                        updated = dictionary.TryAccumulateBorrowedWord(bytes, fallback, increment, scope, call.CallSpan,
                            out var appendHash);
                        if (appendHash is uint hash)
                        {
                            scope.CheckExecution(call.CallSpan);
                            pendingWord = cursor.Materialize(bytes);
                            updated = dictionary.TryAppendBorrowedWord(pendingWord, hash, fallback, increment);
                        }
                    }
                    else updated = dictionary.TryAccumulateWithoutAdmission(word, fallback, increment, scope, call.CallSpan);
                    if (!updated)
                    {
                        // The ordinary body resumes with the Python word
                        // already published, before insertion or a protocol.
                        execution.InstructionIndex = headIndex + 2;
                        return true;
                    }

                    if (hasTotal)
                    {
                        pendingTotal += totalIncrement;
                        totalChanged = true;
                        if (pendingTotal < -HistogramTotalLimit || pendingTotal >= HistogramTotalLimit)
                        {
                            execution.InstructionIndex = totalIndex + 2;
                            return true;
                        }
                    }

                    execution.InstructionIndex = headIndex;
                    scope.CheckExecution(head.Span);
                }

                exhausted = true;
                execution.InstructionIndex = head.TargetIndex;
                return true;
            }
            finally
            {
                try { if (frameEntered) scope.LeaveInterpreterFrame(); }
                finally
                {
                    try
                    {
                        // The active cursor's storage stays available until
                        // the prepaid binding is restored, including on unwind.
                        if (pendingWord is not null || !pendingBytes.IsEmpty)
                            execution.Locals[store.ThirdInput].Object = pendingWord ?? cursor.Materialize(pendingBytes);
                    }
                    finally
                    {
                        if (totalChanged) execution.Locals[total.InputStart].SetInteger(pendingTotal);
                        if (exhausted) execution.Stack.RetireTo(head.InputStart);
                    }
                }
            }
        }

        private static bool TryExecuteOrdinaryDictionaryLoop(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction head, bool privateBindings)
        {
            var headIndex = execution.InstructionIndex;
            var callIndex = head.DictionaryLoopCallIndex;
            var call = execution.Code.CallSites[execution.Code.Instructions[callIndex].CallSiteIndex];
            var memberSpan = execution.Code.Instructions[headIndex + 2].Span;
            ref readonly var add = ref execution.Code.Instructions[callIndex + 1];
            ref readonly var store = ref execution.Code.Instructions[callIndex + 2];
            var fallback = execution.Code.Constants[execution.Code.Instructions[headIndex + 4].ConstantIndex]!;
            var increment = (BigInteger)execution.Code.Constants[~add.SecondInput]!;
            ref readonly var total = ref execution.Code.Instructions[callIndex + 3];
            var hasTotal = total.OpCode == ExecutableOpCode.Augmented;
            var totalIncrement = hasTotal ? (BigInteger)execution.Code.Constants[~total.SecondInput]! : BigInteger.Zero;

            if (!execution.Stack.TryAdvanceNativeIteration(head.InputStart, out var hasWord, out var next)) return false;
            var totalChanged = false;
            long pendingTotal = 0;
            var exhausted = false;
            try
            {
                while (hasWord)
                {
                    PublishInstructionResult(ref execution, invocation, head, next.Object!);
                    // An observable store can reenter. Read the current
                    // dictionary and operands only after publishing the word.
                    scope.CheckExecution(memberSpan);
                    var dictionary = execution.Locals[store.SecondInput].Object as PyDict;
                    var key = execution.Locals[store.ThirdInput].Object as PyString;
                    long priorTotal = 0;
                    if (dictionary is null || key is null ||
                        hasTotal && !TryReadHistogramTotal(execution.Locals[total.InputStart], out priorTotal))
                    {
                        execution.InstructionIndex = headIndex + 2;
                        return true;
                    }

                    execution.InstructionIndex = callIndex;
                    scope.EnterInterpreterFrame(call.CallSpan);
                    bool updated;
                    try { updated = dictionary.TryAccumulateWithoutAdmission(key, fallback, increment, scope, call.CallSpan); }
                    finally { scope.LeaveInterpreterFrame(); }
                    if (!updated)
                    {
                        execution.InstructionIndex = headIndex + 2;
                        return true;
                    }

                    execution.Stack.RetireTo(store.LiveExtent);
                    if (hasTotal)
                    {
                        execution.InstructionIndex = callIndex + 3;
                        scope.CheckExecution(total.Span);
                        if (privateBindings)
                        {
                            pendingTotal = (totalChanged ? pendingTotal : priorTotal) + (long)totalIncrement;
                            totalChanged = true;
                            if (pendingTotal < -HistogramTotalLimit || pendingTotal >= HistogramTotalLimit)
                            {
                                execution.InstructionIndex = callIndex + 5;
                                return true;
                            }
                        }
                        else
                        {
                            var value = AddExactIntegers(new BigInteger(priorTotal), totalIncrement, scope.MemoryGovernor, total.Span);
                            PublishInstructionResult(ref execution, invocation, total, value);
                        }
                    }

                    execution.InstructionIndex = headIndex;
                    scope.CheckExecution(head.Span);
                    if (!execution.Stack.TryAdvanceNativeIteration(head.InputStart, out hasWord, out next))
                        throw new InvalidOperationException("A native loop lost its private iteration source.");
                }
                exhausted = true;
                execution.InstructionIndex = head.TargetIndex;
                return true;
            }
            finally
            {
                if (totalChanged) execution.Locals[total.InputStart].SetInteger(pendingTotal);
                if (exhausted) execution.Stack.RetireTo(head.InputStart);
            }
        }

        private static bool TryExecuteDictionaryAccumulation(ref ExecutableWorkState execution, ExecutionContext context,
            ExecutableCallSite call, int start, PyDict dictionary, PyString key)
        {
            ref readonly var add = ref execution.Code.Instructions[execution.InstructionIndex + 1];
            ref readonly var store = ref execution.Code.Instructions[execution.InstructionIndex + 2];
            // Future inputs are pure locals/constants. A missing or different
            // value leaves the original instructions to preserve read errors,
            // source spans and all dynamic evaluation/assignment behavior.
            if (!ReferenceEquals(execution.Locals[store.SecondInput].Object, dictionary)
                || !ReferenceEquals(execution.Locals[store.ThirdInput].Object, key)) return false;
            var source = add.SecondInput;
            if (source < 0 && (uint)~source >= (uint)execution.Code.Constants.Length) return false;
            var increment = source < 0 ? execution.Code.Constants[~source] : execution.Locals[source].Object;
            if (increment is not BigInteger integer || integer.GetByteCount() >= 8) return false;
            var fallback = call.ArgumentCount == 2 ? execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
            context.EnterInterpreterFrame(call.CallSpan);
            try
            {
                if (!dictionary.TryAccumulateWithoutAdmission(key, fallback, integer, context,
                    call.CallSpan)) return false;
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
