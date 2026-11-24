using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableCreateFunction? PrepareDirectDefinitionReturn(CancellationToken cancellationToken,
        out bool definitionPair)
    {
        definitionPair = false;
        var createIndex = Instructions.Length - 2;
        if (!UsesOnlyLexicalSlots || EntryIndex != 0 || ExceptionRegions.Count != 0 ||
            createIndex < 0 || Instructions[^1].OpCode != ExecutableOpCode.Return ||
            Instructions[^1].InputStart != Instructions[createIndex].ResultSlot) return null;

        if (Instructions[createIndex].OpCode == ExecutableOpCode.MakeTuple)
        {
            definitionPair = HasClosedDefinitionPair(createIndex, cancellationToken);
            return null;
        }

        if (Instructions[createIndex].OpCode != ExecutableOpCode.ApplyOperation ||
            Constants[Instructions[createIndex].ConstantIndex] is not ExecutableCreateFunction
            {
                DecoratorCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0,
                TypeParameters: null, DirectKeywordDefault: false,
            } definition) return null;

        var body = definition.CodeObject;
        if (!body.UsesOnlyLexicalSlots || body.ScopeFacts.RequiresClassCell) return null;
        if (createIndex != 0)
            return definition.InputCount == 2 && definition.ParameterLayout is { PositionalDefaultCount: 1, Defaults.Length: 2 }
                && ClosureNames.Count <= 1 && TryReadClosedDefinition(0, ClosureNames.Count != 0, pair: false,
                    cancellationToken, out var emitted)
                && emitted == createIndex ? definition : null;

        // The declaration captures the factory's only bound argument by value.
        // Its code still owns live function metadata and allocates a fresh callable.
        var capture = body.CaptureSources;
        return definition is { InputCount: 0, EagerAnnotationReads: null, PostponedAnnotations: null } &&
            capture is { CanCaptureByValue: true, Slots: [0] } &&
            ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot) ? definition : null;
    }

    private bool HasClosedDefinitionPair(int tupleIndex, CancellationToken token)
    {
        if (ClosureNames.Count != 0 || CapturedLocalSlots.Count != 0 ||
            Instructions[tupleIndex] is not { ItemCount: 2, HasResultConsumer: false } tuple ||
            !TryReadClosedDefinition(0, false, pair: true, token, out var first) ||
            first + 2 >= tupleIndex ||
            !TryReadDefinitionStore(first, out var left) ||
            !TryReadClosedDefinition(first + 2, false, pair: true, token, out var second) ||
            second + 4 != tupleIndex || !TryReadDefinitionStore(second, out var right) || left == right)
            return false;

        ref readonly var leftRead = ref Instructions[tupleIndex - 2];
        ref readonly var rightRead = ref Instructions[tupleIndex - 1];
        return leftRead is { OpCode: ExecutableOpCode.PreparedTupleReturn } &&
            leftRead.PreparedTupleIndex == tupleIndex && leftRead.LocalSlot == left && leftRead.ResultSlot == tuple.InputStart &&
            rightRead is { OpCode: ExecutableOpCode.LoadLocal } &&
            rightRead.LocalSlot == right && rightRead.ResultSlot == tuple.InputStart + 1;
    }

    private bool TryReadDefinitionStore(int index, out int local)
    {
        local = -1;
        if (index + 1 >= Instructions.Length ||
            Instructions[index + 1] is not { OpCode: ExecutableOpCode.StoreLocal, StoresCapturedLocal: false } store ||
            !Instructions[index].HasResultConsumer || store.InputStart != Instructions[index].ResultSlot || store.LocalSlot == 0)
            return false;
        local = store.LocalSlot;
        return true;
    }

    private bool TryReadClosedDefinition(int startIndex, bool fixedKeyword, bool pair, CancellationToken token, out int creationIndex)
    {
        creationIndex = -1;
        if (Instructions[startIndex] is not { OpCode: ExecutableOpCode.StartFunctionDefaults, StartsKeywordDefaultsOnly: false } start ||
            Constants[start.ConstantIndex] is not FunctionParameterLayout { Defaults.Length: > 0 } layout ||
            start.LiveExtent != start.ResultSlot + layout.DefaultStorageCount) return false;

        var index = startIndex + 1;
        for (var i = 0; i < layout.Defaults.Length; i++)
        {
            PythonTokenization.CheckCancellation(token, index);
            if (index >= Instructions.Length) return false;
            var positional = i < layout.PositionalDefaultCount;
            var result = -1;
            ref readonly var read = ref Instructions[index];
            if (fixedKeyword && !positional)
            {
                if (read is not { OpCode: ExecutableOpCode.LoadClosure, ClosureSlot: 0, HasResultConsumer: true }) return false;
                result = read.ResultSlot;
                index++;
            }
            else if (pair && read.OpCode == ExecutableOpCode.LoadConst)
            {
                if (!read.HasResultConsumer || Constants[read.ConstantIndex] is not BigInteger) return false;
                result = read.ResultSlot;
                index++;
            }
            else
            {
                while (index + 1 < Instructions.Length && Instructions[index].OpCode == ExecutableOpCode.Binary)
                {
                    PythonTokenization.CheckCancellation(token, index);
                    ref readonly var binary = ref Instructions[index++];
                    var last = Instructions[index].OpCode == ExecutableOpCode.StoreFunctionDefault;
                    if (binary.BinaryOperator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or
                        BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo) &&
                        !(pair && last && LythonRuntime.IsScalarComparison(binary.BinaryOperator)) ||
                        !IsInput(binary.InputStart, result) || !IsInput(binary.SecondInput, result) ||
                        binary.ResultSlot < start.LiveExtent || binary.HasResultConsumer != last)
                        return false;
                    result = binary.ResultSlot;
                }
            }

            if (result < start.LiveExtent || index >= Instructions.Length) return false;
            ref readonly var store = ref Instructions[index++];
            var destination = positional ? start.ResultSlot : start.LiveExtent - 1;
            var valueIndex = positional ? i : layout.KeywordDefaultAt(layout.Defaults[i].KeywordIndex).RecordPosition;
            if (store.OpCode != ExecutableOpCode.StoreFunctionDefault || store.DefaultValueIndex != valueIndex ||
                store.InputStart != destination || store.ResultSlot != result) return false;
        }

        if (index >= Instructions.Length || Instructions[index] is not { OpCode: ExecutableOpCode.ApplyOperation } create ||
            create.InputStart != start.ResultSlot) return false;
        var body = Constants[create.ConstantIndex] switch
        {
            ExecutableCreateFunction { DecoratorCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0,
                TypeParameters: null, DirectKeywordDefault: false, AnnotationOperandCount: 0 } definition
                when ReferenceEquals(definition.ParameterLayout, layout) => definition.CodeObject,
            ExecutableCreateLambda { Binding: { IsGenerator: false, NamespaceClosure: false } binding }
                when ReferenceEquals(binding.Layout, layout) => binding.CodeObject,
            _ => null,
        };
        if (body is not { UsesOnlyLexicalSlots: true, ClosureNames.Count: 0, ScopeFacts.RequiresClassCell: false }) return false;
        creationIndex = index;
        return true;

        // The argument, immutable literals and the immediately preceding
        // integer are the only live inputs during a default expression.
        bool IsInput(int source, int previous) => source == 0 || (source >= 0 ? source == previous :
            (uint)~source < (uint)Constants.Length && Constants[~source] is BigInteger);
    }

    private bool HasClosedIntegerReturn(CancellationToken cancellationToken)
    {
        if (Instructions.Length < 2 || EntryIndex != 0 || ExceptionRegions.Count != 0 || CapturedLocalSlots.Count != 0)
            return false;

        // Returned conditional expressions already have their own terminal
        // instructions. An exact condition can choose between the two ranges
        // with the same scalar evaluator used for an unconditional return.
        ref readonly var first = ref Instructions[0];
        if (first.OpCode == ExecutableOpCode.JumpIfFalse)
        {
            var alternative = first.TargetIndex;
            return Instructions[1] is { OpCode: ExecutableOpCode.Jump, TargetIndex: 2 } &&
                alternative > 2 && alternative < Instructions.Length && IsInput(first.InputStart) &&
                HasClosedPath(2, alternative - 1) && HasClosedPath(alternative, Instructions.Length - 1);
        }
        return HasClosedPath(0, Instructions.Length - 1);

        bool HasClosedPath(int start, int end)
        {
            ref readonly var returned = ref Instructions[end];
            if (returned.OpCode != ExecutableOpCode.Return) return false;
            if (start == end) return IsInput(returned.InputStart);
            if (returned.InputStart != Instructions[end - 1].ResultSlot) return false;

            // The full selected body uses the existing chain and publishes
            // a single arithmetic result. Runtime arity checks its locals.
            var carry = Instructions[start].ResultSlot;
            for (var index = start; index < end; index++)
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
                if (index == start)
                {
                    if (!IsInput(left) || !IsInput(right)) return false;
                }
                else if (!Instructions[index - 1].HasIntegerContinuation ||
                    !(left == carry && IsInput(right) || right == carry && IsInput(left))) return false;
            }
            return true;
        }

        // A capture reads the function's live cell. An ordinary global
        // stays on the main instruction path.
        bool IsInput(int slot) => slot >= 0 ? slot < FirstTemporarySlot
            : ~slot < Constants.Length || ~slot >= CaptureOperandBase;
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
