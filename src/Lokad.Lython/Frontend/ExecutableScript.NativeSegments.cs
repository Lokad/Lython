using System.Numerics;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableCreateFunction? PrepareDirectDefinitionReturn()
    {
        if (!UsesOnlyLexicalSlots || EntryIndex != 0 || ExceptionRegions.Count != 0 ||
            Instructions.Length != 2 || Instructions[0].OpCode != ExecutableOpCode.ApplyOperation ||
            Instructions[1].OpCode != ExecutableOpCode.Return || Instructions[1].InputStart != Instructions[0].ResultSlot ||
            Constants[Instructions[0].ConstantIndex] is not ExecutableCreateFunction
            {
                InputCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0,
                TypeParameters: null, EagerAnnotationReads: null, PostponedAnnotations: null,
            } definition) return null;

        // The declaration captures the factory's only bound argument by value.
        // Its code still owns live function metadata and allocates a fresh callable.
        var body = definition.CodeObject;
        var capture = body.CaptureSources;
        return body.UsesOnlyLexicalSlots && !body.ScopeFacts.RequiresClassCell &&
            capture is { CanCaptureByValue: true, Slots: [0] } &&
            ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot) ? definition : null;
    }

    private bool HasClosedIntegerReturn(CancellationToken cancellationToken)
    {
        if (Instructions.Length < 2 || EntryIndex != 0 || ExceptionRegions.Count != 0 ||
            CapturedLocalSlots.Count != 0 || Instructions[^1].OpCode != ExecutableOpCode.Return ||
            Instructions[^1].InputStart != Instructions[^2].ResultSlot) return false;

        // The complete body uses only the existing integer chain and wraps
        // one new result. Runtime arity verifies that all locals are arguments.
        var carry = Instructions[0].ResultSlot;
        for (var index = 0; index < Instructions.Length - 1; index++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, index);
            ref readonly var instruction = ref Instructions[index];
            if (instruction.OpCode != ExecutableOpCode.Binary || instruction.ResultSlot != carry ||
                instruction.HasResultConsumer ||
                instruction.BinaryOperator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or
                    BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo))
                return false;

            var left = instruction.InputStart;
            var right = instruction.SecondInput;
            if (index == 0)
            {
                if (!IsInput(left) || !IsInput(right)) return false;
            }
            else if (!Instructions[index - 1].HasIntegerContinuation ||
                !(left == carry && IsInput(right) || right == carry && IsInput(left))) return false;
        }
        return true;

        // A forwarded global occupies the same encoded range as constants,
        // after the last constant. Only immutable literal operands qualify.
        bool IsInput(int slot) => slot >= 0 ? slot < FirstTemporarySlot : ~slot < Constants.Length;
    }

    // A native get/add/store can reuse one table probe. Original instructions
    // remain the pre-effect path for dynamic values, growth and other methods.
    private void PrepareNativeSegments(ExecutableCallSite[] sites)
    {
        for (var index = 0; index + 2 < Instructions.Length; index++)
        {
            ref readonly var call = ref Instructions[index];
            ref readonly var add = ref Instructions[index + 1];
            ref readonly var store = ref Instructions[index + 2];
            if (call.OpCode == ExecutableOpCode.LoadClosure)
            {
                if (CanPrepareClosureArithmetic(index, call, add, store))
                    Instructions[index] = call.WithClosureArithmetic(~add.SecondInput);
                continue;
            }
            if (call.OpCode != ExecutableOpCode.Call || call.HasResultConsumer
                || add.OpCode != ExecutableOpCode.Binary || add.BinaryOperator != BinaryOperatorSyntax.Add || add.HasResultConsumer
                || store.OpCode != ExecutableOpCode.StoreSubscript
                || add.InputStart != call.ResultSlot || add.ResultSlot != call.ResultSlot || add.LiveExtent != call.LiveExtent
                || store.InputStart != call.ResultSlot || store.LiveExtent != call.ResultSlot
                || !IsUncapturedLocal(store.SecondInput) || !IsUncapturedLocal(store.ThirdInput)
                || !(add.SecondInput < 0 || IsUncapturedLocal(add.SecondInput))) continue;
            var site = sites[call.CallSiteIndex];
            if (!site.HasReceiver || !site.IsPositionalOnly || site.ArgumentCount is not (1 or 2)) continue;
            if (CrossesExceptionBoundary(index, index + 2)) continue;
            sites[call.CallSiteIndex] = site with { AccumulatesDictionary = true };
            PrepareDictionaryLoop(index, site, add, store);
        }

        bool IsUncapturedLocal(int slot) => slot >= 0 && slot < FirstTemporarySlot && !CapturedLocalSlots.Contains(slot);

        void PrepareDictionaryLoop(int callIndex, ExecutableCallSite site,
            in ExecutableInstruction add, in ExecutableInstruction store)
        {
            // One complete histogram body, optionally followed by a total.
            // Only native iteration executes the segment; all other sources
            // keep their existing ForNext/body, including actual suspension.
            var headIndex = callIndex - 5;
            // Deferred word/total publication requires independent bindings.
            // Otherwise an assignment in the original body changes a receiver
            // or operand before a later operation observes it.
            if (headIndex < 0 || site.ArgumentCount != 2 || store.SecondInput == store.ThirdInput) return;
            ref readonly var head = ref Instructions[headIndex];
            ref readonly var binding = ref Instructions[headIndex + 1];
            ref readonly var member = ref Instructions[headIndex + 2];
            ref readonly var key = ref Instructions[headIndex + 3];
            ref readonly var fallback = ref Instructions[headIndex + 4];
            if (head.OpCode != ExecutableOpCode.ForNext || !head.HasResultConsumer
                || binding.OpCode != ExecutableOpCode.StoreLocal || binding.LocalSlot != store.ThirdInput
                || binding.InputStart != head.ResultSlot || binding.LiveExtent != head.ResultSlot
                || head.LiveExtent != head.ResultSlot + 1 || Instructions[callIndex].InputStart != head.ResultSlot
                || member.OpCode != ExecutableOpCode.LoadCallMember || member.InputStart != store.SecondInput
                || Names[member.NameIndex] != "get" || member.ResultSlot != head.ResultSlot
                || key.OpCode != ExecutableOpCode.LoadLocal || key.LocalSlot != binding.LocalSlot
                || key.ResultSlot != head.ResultSlot + site.ArgumentOffset
                || fallback.OpCode != ExecutableOpCode.LoadConst
                || fallback.ResultSlot != key.ResultSlot + 1
                || Constants[fallback.ConstantIndex] is not BigInteger
                || add.SecondInput >= 0 || (uint)~add.SecondInput >= (uint)Constants.Length
                || Constants[~add.SecondInput] is not BigInteger increment
                || increment.GetByteCount() >= 8 || store.LiveExtent != head.ResultSlot) return;

            var tailIndex = callIndex + 3;
            if (tailIndex + 2 < Instructions.Length && Instructions[tailIndex].OpCode == ExecutableOpCode.Augmented)
            {
                ref readonly var total = ref Instructions[tailIndex];
                ref readonly var totalStore = ref Instructions[tailIndex + 1];
                if (total.AugmentedOperator != AugmentedAssignmentOperatorSyntax.Add || !total.HasResultConsumer
                    || !IsUncapturedLocal(total.InputStart) || total.SecondInput >= 0
                    || total.InputStart == store.ThirdInput || total.InputStart == store.SecondInput
                    || (uint)~total.SecondInput >= (uint)Constants.Length
                    || Constants[~total.SecondInput] is not BigInteger totalIncrement || totalIncrement.GetByteCount() >= 8
                    || totalStore.OpCode != ExecutableOpCode.StoreLocal || totalStore.LocalSlot != total.InputStart
                    || total.ResultSlot != head.ResultSlot || totalStore.LiveExtent != head.ResultSlot) return;
                tailIndex += 2;
            }
            if (tailIndex >= Instructions.Length || Instructions[tailIndex].OpCode != ExecutableOpCode.Jump
                || Instructions[tailIndex].TargetIndex != headIndex) return;
            if (CrossesExceptionBoundary(headIndex, tailIndex)) return;
            Instructions[headIndex] = head.WithDictionaryLoop(callIndex);
        }

    }

    private bool CanPrepareClosureArithmetic(int index, in ExecutableInstruction load,
        in ExecutableInstruction arithmetic, in ExecutableInstruction store)
        => !RequiresLocalVariableMirroring && !load.HasResultConsumer
            && load.LiveExtent == load.ResultSlot + 1
            && arithmetic.OpCode == ExecutableOpCode.Augmented && !arithmetic.HasResultConsumer
            && arithmetic.AugmentedOperator is AugmentedAssignmentOperatorSyntax.Add
                or AugmentedAssignmentOperatorSyntax.Subtract or AugmentedAssignmentOperatorSyntax.Multiply
                or AugmentedAssignmentOperatorSyntax.FloorDivide or AugmentedAssignmentOperatorSyntax.Modulo
            && arithmetic.InputStart == load.ResultSlot && arithmetic.ResultSlot == load.ResultSlot
            && arithmetic.LiveExtent == load.LiveExtent && arithmetic.SecondInput < 0
            && (uint)~arithmetic.SecondInput < (uint)Constants.Length
            && Constants[~arithmetic.SecondInput] is BigInteger operand && operand.GetByteCount() <= sizeof(long)
            && store.OpCode == ExecutableOpCode.StoreClosure && store.ClosureSlot == load.ClosureSlot
            && store.InputStart == load.ResultSlot && store.LiveExtent == load.ResultSlot
            && !CrossesExceptionBoundary(index, index + 2);
}
