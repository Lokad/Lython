using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableCreateFunction? PrepareDirectDefinitionReturn(CancellationToken cancellationToken,
        out DirectDefinitionPair definitionPair)
    {
        definitionPair = DirectDefinitionPair.None;
        var createIndex = Instructions.Length - 2;
        if (!UsesOnlyLexicalSlots || EntryIndex != 0 || ExceptionRegions.Count != 0 ||
            createIndex < 0 || Instructions[^1].OpCode != ExecutableOpCode.Return ||
            Instructions[^1].InputStart != Instructions[createIndex].ResultSlot) return null;

        if (Instructions[createIndex].OpCode == ExecutableOpCode.MakeTuple)
        {
            if (HasClosedDefinitionPair(createIndex, cancellationToken)) definitionPair = DirectDefinitionPair.Defaults;
            else if (HasClosedCapturedDefinitionPair(cancellationToken)) definitionPair = DirectDefinitionPair.Captures;
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

    private bool HasClosedCapturedDefinitionPair(CancellationToken token)
    {
        // The prepared suffix supplies the ordered pair, with a private
        // right-hand invocation borrowing the unchanged outer argument.
        var nested = Instructions.Length - 7;
        var creation = nested - 2;
        if (creation < 3 || ClosureNames.Count != 0 ||
            Instructions[nested] is not { OpCode: ExecutableOpCode.ApplyOperation, ImmediatelyCalledDefinitionCapture: 0 } ||
            Constants[Instructions[creation].ConstantIndex] is not ExecutableCreateFunction
            {
                InputCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0, TypeParameters: null,
                EagerAnnotationReads: null, PostponedAnnotations: null,
            } left ||
            left.CodeObject is not { UsesOnlyLexicalSlots: true, ScopeFacts.RequiresClassCell: false,
                CaptureSources: { Slots.Length: > 0 } capture } ||
            !ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot)) return false;

        // Use the existing cell constructor with only the slots actually
        // captured by the result. No mutable guest binding can be forwarded.
        var needed = 0;
        foreach (var slot in capture.Slots)
        {
            if (slot <= 0 || slot >= ClosedCaptureCapacity || (needed & (1 << slot)) != 0) return false;
            needed |= 1 << slot;
        }
        var assigned = 0;
        var index = 0;
        while (index < creation)
        {
            PythonTokenization.CheckCancellation(token, index);
            if (Instructions[index++] is not { OpCode: ExecutableOpCode.LoadLocal, LocalSlot: 0, HasResultConsumer: false } load ||
                load.ResultSlot != FirstTemporarySlot) return false;

            var prior = load.ResultSlot;
            var computed = false;
            while (index + 1 < creation && Instructions[index].OpCode == ExecutableOpCode.Binary)
            {
                PythonTokenization.CheckCancellation(token, index);
                ref readonly var binary = ref Instructions[index++];
                var stored = Instructions[index].OpCode == ExecutableOpCode.StoreLocal;
                if (binary.BinaryOperator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or
                    BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo) &&
                    !(stored && LythonRuntime.IsScalarComparison(binary.BinaryOperator)) ||
                    !ReadsClosedValue(binary.InputStart, prior) || !ReadsClosedValue(binary.SecondInput, prior) ||
                    binary.ResultSlot < FirstTemporarySlot || binary.HasResultConsumer != stored) return false;
                prior = binary.ResultSlot;
                computed = true;
            }
            if (!computed || index >= creation ||
                Instructions[index++] is not { OpCode: ExecutableOpCode.StoreLocal, StoresCapturedLocal: true } store ||
                store.InputStart != prior || store.LocalSlot <= 0 || store.LocalSlot >= ClosedCaptureCapacity) return false;
            var bit = 1 << store.LocalSlot;
            if ((assigned & bit) != 0) return false;
            assigned |= bit;
        }
        return assigned == needed;

        bool ReadsClosedValue(int source, int prior) => source >= 0 ? source == prior :
            (uint)~source < (uint)Constants.Length && Constants[~source] is BigInteger;
    }

    // The zero-argument definition is private to a terminal pair. The native
    // definition boundary may run its body before materializing either its
    // callable or argument workspace. The original tape is also the exact
    // continuation for non-integer arguments and general execution.
    private void PrepareImmediatelyCalledDefinition(CancellationToken token)
    {
        var start = Instructions.Length - 7;
        if (!UsesOnlyLexicalSlots || EntryIndex != 0 || ExceptionRegions.Count != 0 || start < 2 ||
            Instructions[start] is not { OpCode: ExecutableOpCode.ApplyOperation } create ||
            Constants[create.ConstantIndex] is not ExecutableCreateFunction
            {
                InputCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0, TypeParameters: null,
                EagerAnnotationReads: null, PostponedAnnotations: null, ParameterLayout.ParameterCount: 0,
            } definition ||
            !TryReadDefinitionStore(start, out var target) || CapturedLocalSlots.Contains(target) ||
            Instructions[start - 2].OpCode != ExecutableOpCode.ApplyOperation ||
            !TryReadDefinitionStore(start - 2, out var left) || CapturedLocalSlots.Contains(left) || left == target)
            return;

        ref readonly var first = ref Instructions[start + 2];
        ref readonly var load = ref Instructions[start + 3];
        ref readonly var invoke = ref Instructions[start + 4];
        ref readonly var pair = ref Instructions[start + 5];
        if (first is not { OpCode: ExecutableOpCode.LoadLocal, HasResultConsumer: false } || first.LocalSlot != left ||
            load is not { OpCode: ExecutableOpCode.LoadLocal, HasResultConsumer: false } || load.LocalSlot != target ||
            invoke is not { OpCode: ExecutableOpCode.Call, HasResultConsumer: false } ||
            CallSites[invoke.CallSiteIndex] is not { ArgumentCount: 0, HasReceiver: false, UnpackedReturnCount: 0 } ||
            invoke.InputStart != load.ResultSlot || invoke.ResultSlot != first.ResultSlot + 1 ||
            pair is not { OpCode: ExecutableOpCode.MakeTuple, ItemCount: 2, HasResultConsumer: false } ||
            pair.InputStart != first.ResultSlot || Instructions[^1].OpCode != ExecutableOpCode.Return ||
            Instructions[^1].InputStart != pair.ResultSlot) return;

        // A straight-line prefix rules out incoming edges to the skipped
        // slots. The only references to this initialized private binding
        // in the suffix are the immediately adjacent load and call.
        for (var i = 0; i < start; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            if (Instructions[i].OpCode is not (ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadConst or
                ExecutableOpCode.Binary or ExecutableOpCode.StoreLocal or ExecutableOpCode.ApplyOperation))
                return;
        }

        var body = definition.CodeObject;
        if (!body.CanRunNativeInstructions || !body.UsesOnlyLexicalSlots || body.ScopeFacts.RequiresClassCell ||
            body.EntryIndex != 0 || body.ExceptionRegions.Count != 0 || body.ClosureNames.Count != 1 ||
            body.CaptureSources is not { CanCaptureByValue: true, Slots: [>= 0] } capture ||
            !ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot) || !body.ReturnsLambdaFromClosedCapture(token))
            return;
        Instructions[start] = create.WithImmediatelyCalledDefinition(capture.Slots[0]);
    }

    private bool ReturnsLambdaFromClosedCapture(CancellationToken token)
    {
        var storeIndex = Instructions.Length - 3;
        if (storeIndex < 1 ||
            Instructions[storeIndex] is not { OpCode: ExecutableOpCode.StoreLocal, StoresCapturedLocal: true } store ||
            Instructions[^2] is not { OpCode: ExecutableOpCode.ApplyOperation, HasResultConsumer: false } create ||
            create.InputStart != store.LiveExtent ||
            Constants[create.ConstantIndex] is not ExecutableCreateLambda { InputCount: 0,
                Binding: { NamespaceClosure: false, IsGenerator: false } } lambda ||
            !lambda.CodeObject.UsesOnlyLexicalSlots || lambda.CodeObject.ScopeFacts.RequiresClassCell ||
            lambda.CodeObject.CaptureSources is not { Slots.Length: 1 } capture ||
            !ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot) || capture.Slots[0] != store.LocalSlot ||
            Instructions[^1].OpCode != ExecutableOpCode.Return || Instructions[^1].InputStart != create.ResultSlot)
            return false;

        var previous = -1;
        for (var i = 0; i < storeIndex; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            ref readonly var instruction = ref Instructions[i];
            if (instruction.OpCode != ExecutableOpCode.Binary ||
                instruction.BinaryOperator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or
                    BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo) ||
                !IsKnown(instruction.InputStart, previous) || !IsKnown(instruction.SecondInput, previous) ||
                instruction.ResultSlot < FirstTemporarySlot ||
                instruction.HasResultConsumer != (i == storeIndex - 1)) return false;
            previous = instruction.ResultSlot;
        }
        return store.InputStart == previous;

        bool IsKnown(int source, int prior) => source >= 0 ? source == prior :
            ~source == CaptureOperandBase || ((uint)~source < (uint)Constants.Length && Constants[~source] is BigInteger);
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
