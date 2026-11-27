using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableCreateFunction? PrepareDirectDefinitionReturn(CancellationToken cancellationToken,
        out int definitionPairFirstCreation)
    {
        definitionPairFirstCreation = 0;
        var createIndex = Instructions.Length - 2;
        if (!UsesOnlyLexicalSlots || EntryIndex != 0 || ExceptionRegions.Count != 0 ||
            createIndex < 0 || Instructions[^1].OpCode != ExecutableOpCode.Return ||
            Instructions[^1].InputStart != Instructions[createIndex].ResultSlot) return null;

        if (Instructions[createIndex].OpCode == ExecutableOpCode.MakeTuple)
        {
            definitionPairFirstCreation = FindClosedDefinitionPair(createIndex, cancellationToken);
            if (definitionPairFirstCreation == 0 && HasClosedCapturedDefinitionPair(cancellationToken))
                definitionPairFirstCreation = CapturedDefinitionPair;
            return null;
        }

        if (Instructions[createIndex].OpCode != ExecutableOpCode.ApplyOperation ||
            Constants[Instructions[createIndex].ConstantIndex] is not ExecutableCreateFunction
            {
                IsLambda: false, DecoratorCount: 0, IsGenerator: false, DeferredAnnotations.Length: 0,
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
        return definition is { InputCount: 0, LiteralPositionalDefaults: null, EagerAnnotationReads: null, PostponedAnnotations: null } &&
            capture is { CanCaptureByValue: true, Slots: [0] } &&
            ReferenceEquals(capture.ParentLocalSlots, LocalNameToSlot) ? definition : null;
    }

    private int FindClosedDefinitionPair(int tupleIndex, CancellationToken token)
    {
        if (ClosureNames.Count != 0 || CapturedLocalSlots.Count != 0 ||
            Instructions[tupleIndex] is not { ItemCount: 2, HasResultConsumer: false } tuple ||
            !TryReadClosedDefinition(0, false, pair: true, token, out var first) ||
            first + 2 >= tupleIndex ||
            !TryReadDefinitionStore(first, out var left) ||
            !TryReadClosedDefinition(first + 2, false, pair: true, token, out var second) ||
            second + 4 != tupleIndex || !TryReadDefinitionStore(second, out var right) || left == right)
            return 0;

        ref readonly var leftRead = ref Instructions[tupleIndex - 2];
        ref readonly var rightRead = ref Instructions[tupleIndex - 1];
        return leftRead is { OpCode: ExecutableOpCode.PreparedTupleReturn } &&
            leftRead.PreparedTupleIndex == tupleIndex && leftRead.LocalSlot == left && leftRead.ResultSlot == tuple.InputStart &&
            rightRead is { OpCode: ExecutableOpCode.LoadLocal } &&
            rightRead.LocalSlot == right && rightRead.ResultSlot == tuple.InputStart + 1 ? first : 0;
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
                IsLambda: false, InputCount: 0, LiteralPositionalDefaults: null, IsGenerator: false, DeferredAnnotations.Length: 0, TypeParameters: null,
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
                IsLambda: false, InputCount: 0, LiteralPositionalDefaults: null, IsGenerator: false, DeferredAnnotations.Length: 0, TypeParameters: null,
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
            Constants[create.ConstantIndex] is not ExecutableCreateFunction
                { Kind: ExecutableDefinitionKind.Lambda, InputCount: 0, LiteralPositionalDefaults: null, IsGenerator: false } lambda ||
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
        if (Instructions[startIndex] is not { OpCode: ExecutableOpCode.StartFunctionDefaults } start ||
            Constants[start.ConstantIndex] is not ExecutableCreateFunction
            {
                Kind: not ExecutableDefinitionKind.NamespaceLambda, DecoratorCount: 0, IsGenerator: false,
                DeferredAnnotations.Length: 0, TypeParameters: null, DirectKeywordDefault: false, AnnotationOperandCount: 0, LiteralPositionalDefaults: null,
                CodeObject: { UsesOnlyLexicalSlots: true, ClosureNames.Count: 0, ScopeFacts.RequiresClassCell: false },
                ParameterLayout: { Defaults.Length: > 0 } layout,
            } definition || start.LiveExtent != start.ResultSlot + layout.DefaultStorageCount) return false;
        if (fixedKeyword && definition.LiteralKeywordDefaults is not null) return false;

        var index = startIndex + 1;
        var evaluated = definition.LiteralKeywordDefaults is null ? layout.Defaults.Length : layout.PositionalDefaultCount;
        for (var i = 0; i < evaluated; i++)
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
            create.InputStart != start.ResultSlot || create.ConstantIndex != start.ConstantIndex) return false;
        creationIndex = index;
        return true;

        // The argument, immutable literals and the immediately preceding
        // integer are the only live inputs during a default expression.
        bool IsInput(int source, int previous) => source == 0 || (source >= 0 ? source == previous :
            (uint)~source < (uint)Constants.Length && Constants[~source] is BigInteger);
    }

    private bool HasClosedOperandReturn(CancellationToken cancellationToken)
    {
        if (Instructions.Length == 0 || EntryIndex != 0 || ExceptionRegions.Count != 0 || CapturedLocalSlots.Count != 0)
            return false;

        // Returned conditional expressions already have their own terminal
        // instructions. An exact condition can choose between the two ranges
        // with the same scalar evaluator used for an unconditional return.
        ref readonly var first = ref Instructions[0];
        if (first.OpCode == ExecutableOpCode.LoadCallMember)
            return HasClosedStringAffixReturn();
        if (first.OpCode == ExecutableOpCode.LoadGlobal)
            return HasClosedFieldCallReturn();
        if (first.OpCode == ExecutableOpCode.JumpIfFalse)
        {
            var alternative = first.TargetIndex;
            return alternative > 2 && alternative < Instructions.Length &&
                Instructions[1] is { OpCode: ExecutableOpCode.Jump, TargetIndex: 2 } && IsInput(first.InputStart) &&
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

    private bool HasClosedStringAffixReturn()
    {
        if (Instructions.Length != 4) return false;
        ref readonly var member = ref Instructions[0];
        ref readonly var prefix = ref Instructions[1];
        ref readonly var call = ref Instructions[2];
        ref readonly var returned = ref Instructions[3];
        if (Names[member.NameIndex] is not ("startswith" or "endswith") ||
            (uint)member.InputStart >= (uint)FirstTemporarySlot ||
            prefix.OpCode != ExecutableOpCode.LoadConst || Constants[prefix.ConstantIndex] is not PyString ||
            call.OpCode != ExecutableOpCode.Call || call.HasForwardedCallOperands || call.InputStart != member.ResultSlot ||
            returned.OpCode != ExecutableOpCode.Return || returned.InputStart != call.ResultSlot)
            return false;

        var site = CallSites[call.CallSiteIndex];
        return site is { HasReceiver: true, IsPositionalOnly: true, ArgumentCount: 1 } &&
            prefix.ResultSlot == call.InputStart + site.ArgumentOffset;
    }

    private bool HasClosedFieldCallReturn()
    {
        if (Instructions.Length != 4) return false;
        ref readonly var target = ref Instructions[0];
        ref readonly var field = ref Instructions[1];
        ref readonly var call = ref Instructions[2];
        ref readonly var returned = ref Instructions[3];
        if (target.HasResultConsumer || field.HasResultConsumer || call.HasResultConsumer ||
            field.OpCode != ExecutableOpCode.Subscript || (uint)field.InputStart >= (uint)FirstTemporarySlot ||
            (uint)~field.SecondInput >= (uint)Constants.Length || Constants[~field.SecondInput] is not PyString ||
            call.OpCode != ExecutableOpCode.Call || call.HasForwardedCallOperands ||
            returned.OpCode != ExecutableOpCode.Return || returned.InputStart != call.ResultSlot) return false;

        var site = CallSites[call.CallSiteIndex];
        return site is { HasReceiver: false, IsPositionalOnly: true, ArgumentCount: 1, UnpackedReturnCount: 0 } &&
            target.ResultSlot == call.InputStart + site.TargetOffset && field.ResultSlot == call.InputStart + site.ArgumentOffset;
    }

    // Complete a native segment when emission reaches its store or loop
    // backedge. All earlier slots and protected-region offsets are final.
    internal void PrepareNativeTail(int index, ExecutableCallSite[] sites)
    {
        ref readonly var tail = ref Instructions[index];
        if (index < 2) return;
        switch (tail.OpCode)
        {
            case ExecutableOpCode.StoreClosure:
                var loadIndex = index - 2;
                ref readonly var load = ref Instructions[loadIndex];
                ref readonly var arithmetic = ref Instructions[index - 1];
                if (load.OpCode == ExecutableOpCode.LoadClosure &&
                    CanPrepareClosureArithmetic(loadIndex, load, arithmetic, tail))
                    Instructions[loadIndex] = load.WithClosureArithmetic(~arithmetic.SecondInput);
                break;
            case ExecutableOpCode.StoreSubscript:
                PrepareDictionaryUpdate(index, tail, sites);
                break;
            case ExecutableOpCode.Jump:
                PrepareDictionaryLoop(index, tail.TargetIndex);
                break;
            case ExecutableOpCode.JumpIfFalse:
                PrepareBufferedPrefixSearch(index, tail, sites);
                break;
        }
    }

    private void PrepareBufferedPrefixSearch(int tailIndex, in ExecutableInstruction tail, ExecutableCallSite[] sites)
    {
        var headIndex = tail.TargetIndex;
        if (headIndex != tailIndex - 5 || headIndex < 0 || RequiresLocalVariableMirroring) return;
        ref readonly var head = ref Instructions[headIndex];
        if (head.OpCode != ExecutableOpCode.ForNext || !head.HasResultConsumer) return;

        ref readonly var line = ref Instructions[headIndex + 1];
        ref readonly var increment = ref Instructions[headIndex + 2];
        ref readonly var counter = ref Instructions[headIndex + 3];
        ref readonly var call = ref Instructions[headIndex + 4];
        if (line.OpCode != ExecutableOpCode.StoreLocal || counter.OpCode != ExecutableOpCode.StoreLocal ||
            !IsUncapturedLocal(line.LocalSlot) || !IsUncapturedLocal(counter.LocalSlot) || line.LocalSlot == counter.LocalSlot ||
            increment.OpCode != ExecutableOpCode.Augmented || increment.AugmentedOperator != AugmentedAssignmentOperatorSyntax.Add ||
            increment.InputStart != counter.LocalSlot || (uint)~increment.SecondInput >= (uint)Constants.Length ||
            Constants[~increment.SecondInput] is not BigInteger { IsOne: true } || !increment.HasResultConsumer ||
            line.InputStart != head.ResultSlot || line.LiveExtent != head.ResultSlot || counter.InputStart != increment.ResultSlot ||
            call is not { OpCode: ExecutableOpCode.Call, HasForwardedCallOperands: true, HasResultConsumer: false } ||
            call.SecondInput != line.LocalSlot || !IsUncapturedLocal(call.InputStart) ||
            call.InputStart == line.LocalSlot || call.InputStart == counter.LocalSlot || tail.InputStart != call.ResultSlot ||
            sites[call.CallSiteIndex] is not { IsPositionalOnly: true, ArgumentCount: 1, HasReceiver: false, UnpackedReturnCount: 0 } ||
            CrossesExceptionBoundary(headIndex, tailIndex)) return;

        Instructions[headIndex] = head.WithBufferedPrefixSearch(counter.LocalSlot);
    }

    private void PrepareDictionaryUpdate(int index, in ExecutableInstruction store, ExecutableCallSite[] sites)
    {
        // A native get/add/store can reuse one table probe. The emitted
        // instructions remain the pre-effect path for dynamic values.
        if (index >= 5)
        {
            var getIndex = index - 5;
            ref readonly var get = ref Instructions[getIndex];
            if (get is { OpCode: ExecutableOpCode.Call, HasResultConsumer: false } &&
                Instructions[getIndex + 1].OpCode == ExecutableOpCode.LoadGlobal &&
                CanPrepareConvertedDictionaryUpdate(getIndex, get))
            {
                if (CanReadConvertedDictionaryOperands(getIndex, get, store))
                    Instructions[getIndex - 3] = Instructions[getIndex - 3].WithConvertedDictionaryGet();
                else sites[get.CallSiteIndex] = sites[get.CallSiteIndex] with { AccumulatesDictionary = true };
                return;
            }
        }

        var callIndex = index - 2;
        ref readonly var call = ref Instructions[callIndex];
        ref readonly var add = ref Instructions[index - 1];
        if (call.OpCode != ExecutableOpCode.Call || call.HasResultConsumer
            || add.OpCode != ExecutableOpCode.Binary || add.BinaryOperator != BinaryOperatorSyntax.Add || add.HasResultConsumer
            || add.InputStart != call.ResultSlot || add.ResultSlot != call.ResultSlot || add.LiveExtent != call.LiveExtent
            || store.InputStart != call.ResultSlot || store.LiveExtent != call.ResultSlot
            || !IsUncapturedLocal(store.SecondInput) || !IsUncapturedLocal(store.ThirdInput)
            || !(add.SecondInput < 0 || IsUncapturedLocal(add.SecondInput))) return;
        var site = sites[call.CallSiteIndex];
        if (!site.HasReceiver || !site.IsPositionalOnly || site.ArgumentCount is not (1 or 2)) return;
        if (CrossesExceptionBoundary(callIndex, index)) return;
        sites[call.CallSiteIndex] = site with { AccumulatesDictionary = true };
    }

    private void PrepareDictionaryLoop(int tailIndex, int headIndex)
    {
        // One complete histogram body, optionally followed by a total.
        // Other sources retain the general loop, including suspension.
        var callIndex = headIndex + 5;
        var tail = tailIndex - callIndex;
        if (tail is not (3 or 5)) return;
        ref readonly var head = ref Instructions[headIndex];
        ref readonly var call = ref Instructions[callIndex];
        ref readonly var add = ref Instructions[callIndex + 1];
        ref readonly var store = ref Instructions[callIndex + 2];
        if (head.OpCode != ExecutableOpCode.ForNext || !head.HasResultConsumer ||
            call.OpCode != ExecutableOpCode.Call || store.OpCode != ExecutableOpCode.StoreSubscript ||
            CallSites[call.CallSiteIndex] is not { AccumulatesDictionary: true, ArgumentCount: 2 } site ||
            store.SecondInput == store.ThirdInput) return;

        // Deferred word/total publication requires independent bindings.
        ref readonly var binding = ref Instructions[headIndex + 1];
        ref readonly var member = ref Instructions[headIndex + 2];
        ref readonly var key = ref Instructions[headIndex + 3];
        ref readonly var fallback = ref Instructions[headIndex + 4];
        if (binding.OpCode != ExecutableOpCode.StoreLocal || binding.LocalSlot != store.ThirdInput
            || binding.InputStart != head.ResultSlot || binding.LiveExtent != head.ResultSlot
            || head.LiveExtent != head.ResultSlot + 1 || call.InputStart != head.ResultSlot
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

        if (tail == 5)
        {
            ref readonly var total = ref Instructions[callIndex + 3];
            ref readonly var totalStore = ref Instructions[callIndex + 4];
            if (total.OpCode != ExecutableOpCode.Augmented
                || total.AugmentedOperator != AugmentedAssignmentOperatorSyntax.Add || !total.HasResultConsumer
                || !IsUncapturedLocal(total.InputStart) || total.SecondInput >= 0
                || total.InputStart == store.ThirdInput || total.InputStart == store.SecondInput
                || (uint)~total.SecondInput >= (uint)Constants.Length
                || Constants[~total.SecondInput] is not BigInteger totalIncrement || totalIncrement.GetByteCount() >= 8
                || totalStore.OpCode != ExecutableOpCode.StoreLocal || totalStore.LocalSlot != total.InputStart
                || total.ResultSlot != head.ResultSlot || totalStore.LiveExtent != head.ResultSlot) return;
        }
        if (CrossesExceptionBoundary(headIndex, tailIndex)) return;
        Instructions[headIndex] = head.WithDictionaryLoop(callIndex);
    }

    private bool IsUncapturedLocal(int slot)
        => (uint)slot < (uint)FirstTemporarySlot && !CapturedLocalSlots.Contains(slot);

    private bool CanReadConvertedDictionaryOperands(int index, in ExecutableInstruction get, in ExecutableInstruction store)
    {
        // Other captured locals or nested callables do not affect these two
        // uncaptured operands. A frame with mirrored bindings uses the call.
        if (index < 3 || !IsFunctionScope || RequiresLocalVariableMirroring) return false;
        ref readonly var member = ref Instructions[index - 3];
        ref readonly var key = ref Instructions[index - 2];
        ref readonly var fallback = ref Instructions[index - 1];
        var firstArgument = get.ResultSlot + CallSites[get.CallSiteIndex].ArgumentOffset;
        return member is { OpCode: ExecutableOpCode.LoadCallMember, HasResultConsumer: false } &&
            Names[member.NameIndex] == "get" && member.InputStart == store.SecondInput &&
            member.ResultSlot == get.ResultSlot && member.LiveExtent == firstArgument &&
            key is { OpCode: ExecutableOpCode.LoadLocal, HasResultConsumer: false } && key.LocalSlot == store.ThirdInput &&
            key.ResultSlot == firstArgument && key.LiveExtent == firstArgument + 1 &&
            fallback is { OpCode: ExecutableOpCode.LoadConst, HasResultConsumer: false } &&
            Constants[fallback.ConstantIndex] is BigInteger value && value.GetByteCount() < 8 &&
            fallback.ResultSlot == firstArgument + 1 && fallback.LiveExtent == firstArgument + 2 &&
            !CrossesExceptionBoundary(index - 3, index);
    }

    private bool CanPrepareConvertedDictionaryUpdate(int index, in ExecutableInstruction get)
    {
        if (CallSites[get.CallSiteIndex] is not { HasReceiver: true, IsPositionalOnly: true, ArgumentCount: 2 } ||
            get.HasForwardedCallOperands) return false;
        ref readonly var target = ref Instructions[index + 1];
        ref readonly var field = ref Instructions[index + 2];
        ref readonly var convert = ref Instructions[index + 3];
        ref readonly var add = ref Instructions[index + 4];
        ref readonly var store = ref Instructions[index + 5];
        if (target.HasResultConsumer || field.HasResultConsumer || convert.HasResultConsumer || add.HasResultConsumer ||
            field.OpCode != ExecutableOpCode.Subscript || !IsUncapturedLocal(field.InputStart) ||
            (uint)~field.SecondInput >= (uint)Constants.Length || Constants[~field.SecondInput] is not PyString ||
            convert.OpCode != ExecutableOpCode.Call || convert.HasForwardedCallOperands ||
            add.OpCode != ExecutableOpCode.Binary || add.BinaryOperator != BinaryOperatorSyntax.Add ||
            store.OpCode != ExecutableOpCode.StoreSubscript || !IsUncapturedLocal(store.SecondInput) ||
            !IsUncapturedLocal(store.ThirdInput)) return false;

        var right = get.ResultSlot + 1;
        var conversion = CallSites[convert.CallSiteIndex];
        return conversion is { HasReceiver: false, IsPositionalOnly: true, ArgumentCount: 1, UnpackedReturnCount: 0 } &&
            get.LiveExtent == right && convert.ResultSlot == right && target.ResultSlot == right + conversion.TargetOffset &&
            field.ResultSlot == right + conversion.ArgumentOffset && convert.LiveExtent == right + 1 &&
            add.InputStart == get.ResultSlot && add.SecondInput == right && add.ResultSlot == get.ResultSlot &&
            add.LiveExtent == right && store.InputStart == get.ResultSlot && store.LiveExtent == get.ResultSlot &&
            !CrossesExceptionBoundary(index, index + 5);
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
