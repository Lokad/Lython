using System.Numerics;

namespace Lokad.Lython.Frontend;

// Operand shapes are a compiler invariant. Guest iteration, expansion and
// suspension change values, never the number of executable operand slots.
internal sealed record ExecutableStackLayout(int MaximumDepth, int?[] EntryDepths, int FirstTemporarySlot,
    int LastOwnedTemporarySlot)
{
    internal static ExecutableInstruction[] Linearize(IReadOnlyList<List<ExecutableInstruction>> blocks,
        ExecutableStackLayout layout, int[] jumpTargets, CancellationToken token, out int[] offsets)
    {
        offsets = new int[blocks.Count + 1];
        for (var i = 0; i < blocks.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            offsets[i + 1] = offsets[i];
            if (layout.EntryDepths[i].HasValue)
                offsets[i + 1] = checked(offsets[i] + blocks[i].Count + (Completes(blocks[i]) ? 1 : 0));
        }
        var instructions = new ExecutableInstruction[offsets[^1]];
        for (var i = 0; i < blocks.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            if (!layout.EntryDepths[i].HasValue) continue;
            var source = blocks[i];
            for (var j = 0; j < source.Count; j++)
            {
                PythonTokenization.CheckCancellation(token, j);
                var instruction = source[j];
                instructions[offsets[i] + j] = instruction.OpCode switch
                {
                    ExecutableOpCode.AbruptJump => instruction.WithTargetIndex(offsets[instruction.TargetIndex]),
                    ExecutableOpCode.ForNext when j + 1 < source.Count && CanUnpackIteration(instruction, source[j + 1])
                        => instruction.WithTargetIndex(offsets[jumpTargets[instruction.TargetIndex]]).WithFixedUnpack(),
                    ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse or
                        ExecutableOpCode.ForNext or ExecutableOpCode.EndFinally => instruction.WithTargetIndex(offsets[jumpTargets[instruction.TargetIndex]]),
                    ExecutableOpCode.MatchCase => instruction.WithFailureIndex(offsets[jumpTargets[instruction.FailureIndex]]),
                    _ => instruction
                };
            }
            // Stored blocks never fell through. Preserve implicit completion
            // distinctly from a guest top-level return (not legal in imports).
            if (Completes(blocks[i]))
            {
                var extent = source.Count == 0 ? checked(layout.FirstTemporarySlot + (layout.EntryDepths[i] ?? 0)) : source[^1].LiveExtent;
                instructions[offsets[i + 1] - 1] = ExecutableInstruction.Complete(
                    source.Count == 0 ? new LythonSourceSpan(0, 0, 0, 0) : source[^1].Span)
                    .WithStorageSlots(extent, extent, extent);
            }
        }
        return instructions;

        static bool CanUnpackIteration(ExecutableInstruction iteration, ExecutableInstruction next)
            => !iteration.HasResultConsumer && next.OpCode == ExecutableOpCode.UnpackValues
                && next.InputStart == iteration.ResultSlot && next.ResultSlot == iteration.ResultSlot
                && next.UnpackLayout is { HasStarredTarget: false, TargetCount: > 0 };

        static bool Completes(List<ExecutableInstruction> block) => block.Count == 0 ||
            block[^1].OpCode is not (ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or
                ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException);
    }

    internal static ExecutableStackLayout Prepare(IReadOnlyList<List<ExecutableInstruction>> blocks,
        IReadOnlyList<object?> constants, IReadOnlyList<ExecutableCallSite> calls,
        ExecutableExceptionRegion[] regions, int[] jumpTargets,
        int entryBlock, int initialDepth, int firstTemporarySlot, CancellationToken token)
    {
        if (initialDepth < 0 || (uint)entryBlock >= (uint)blocks.Count)
            throw new InvalidOperationException("Invalid executable entry operand depth.");
        var depths = new int?[blocks.Count];
        Queue<int>? pending = null;
        var maximum = initialDepth;
        // Generator expressions arrive with their acquired outer cursor. Other
        // guest values never expose the interpreter's private temporary wrappers.
        var lastOwnedTemporarySlot = initialDepth == 0 ? -1 : checked(firstTemporarySlot + initialDepth - 1);
        depths[entryBlock] = initialDepth;
        var blockIndex = entryBlock;
        do
        {
            PythonTokenization.CheckCancellation(token, 0);
            var depth = depths[blockIndex]!.Value;
            if (regions.Length != 0)
            {
                for (var i = FirstProtectedRegion(regions, blockIndex);
                     i < regions.Length && regions[i].ProtectedStartIndex == blockIndex; i++)
                {
                    var region = regions[i];
                    if (region.ExceptIndex is int except) Enter(jumpTargets[except], depth);
                    if (region.FinallyIndex is int cleanup) Enter(jumpTargets[cleanup], depth);
                }
            }

            var instructions = blocks[blockIndex];
            for (var instructionIndex = 0; instructionIndex < instructions.Count; instructionIndex++)
            {
                PythonTokenization.CheckCancellation(token, instructionIndex);
                var instruction = instructions[instructionIndex];
                var (pop, push, minimum) = Effect(instruction, constants, calls, firstTemporarySlot);
                if (depth < minimum)
                    throw new InvalidOperationException($"Executable operand underflow in block {blockIndex}: {instruction.OpCode}, depth {depth}, required {minimum}.");
                // A raw __class_getitem__ descriptor getter temporarily retains
                // its two protocol arguments above the ordinary subscript inputs.
                if (instruction.OpCode == ExecutableOpCode.Subscript)
                    maximum = Math.Max(maximum, checked(depth - pop + 4));
                var resultSlot = depth - pop;
                var inputStart = depth - minimum;
                depth = checked(resultSlot + push);
                // Prepare the owned instruction array in this existing shape
                // pass; linearization preserves these facts while retargeting.
                var prepared = instruction.WithStorageSlots(
                    checked(firstTemporarySlot + inputStart), checked(firstTemporarySlot + resultSlot),
                    checked(firstTemporarySlot + depth));
                if (instruction.ValueInputCount != 0)
                {
                    var physical = checked(firstTemporarySlot + inputStart);
                    var first = instruction.InputStart < firstTemporarySlot ? instruction.InputStart : physical++;
                    var second = instruction.ValueInputCount > 1
                        ? instruction.SecondInput < firstTemporarySlot ? instruction.SecondInput : physical++ : int.MaxValue;
                    var third = instruction.ValueInputCount == 3
                        ? instruction.ThirdInput < firstTemporarySlot ? instruction.ThirdInput : physical++ : int.MaxValue;
                    prepared = prepared.WithValueInputs(first, second, third);
                }
                instructions[instructionIndex] = prepared;
                var ownedSlot = instruction.OpCode switch
                {
                    ExecutableOpCode.GetIter => inputStart,
                    ExecutableOpCode.StartFormattedString => resultSlot,
                    // A directly consumed/forwarded call can return a private
                    // iteration loan. Cancellation before Return/GetIter must
                    // retire it in the same storage cleanup as other wrappers.
                    ExecutableOpCode.Call when calls[instruction.CallSiteIndex].ConsumedByIteration
                        || calls[instruction.CallSiteIndex].ReturnedDirectly => resultSlot,
                    // Conservatively include every duplicate, without another
                    // value-liveness analysis or runtime resource registry.
                    ExecutableOpCode.Dup => resultSlot + 1,
                    ExecutableOpCode.StartFunctionDefaults => depth - 1,
                    ExecutableOpCode.ApplyOperation when constants[instruction.ConstantIndex] is
                        ExecutableStartComprehension or ExecutableStartCall or ExecutableStartClassHeader => resultSlot,
                    _ => -1
                };
                if (ownedSlot >= 0)
                    lastOwnedTemporarySlot = Math.Max(lastOwnedTemporarySlot, checked(firstTemporarySlot + ownedSlot));
                maximum = Math.Max(maximum, depth);
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.ForNext:
                        Enter(jumpTargets[instruction.TargetIndex], depth - 2);
                        break;
                    case ExecutableOpCode.JumpIfFalse:
                    case ExecutableOpCode.CompareJumpIfFalse:
                        Enter(jumpTargets[instruction.TargetIndex], depth);
                        break;
                    case ExecutableOpCode.MatchCase:
                        Enter(jumpTargets[instruction.FailureIndex], depth);
                        break;
                    case ExecutableOpCode.AbruptJump:
                        Enter(instruction.TargetIndex, depth - (instruction.DiscardIterator ? 1 : 0));
                        goto CompleteBlock;
                    case ExecutableOpCode.Jump:
                    case ExecutableOpCode.EndFinally:
                        Enter(jumpTargets[instruction.TargetIndex], depth);
                        goto CompleteBlock;
                    case ExecutableOpCode.Return:
                    case ExecutableOpCode.ReturnNone:
                    case ExecutableOpCode.ReraiseException:
                        goto CompleteBlock;
                    case ExecutableOpCode.Raise or ExecutableOpCode.FailAssertion:
                        goto CompleteBlock;
                }
            }
            // An unterminated executable block completes its invocation; the
            // existing interpreter never falls through to the next stored block.
        CompleteBlock:;
        } while (pending is not null && pending.TryDequeue(out blockIndex));
        return new(maximum, depths, firstTemporarySlot, lastOwnedTemporarySlot);

        void Enter(int block, int depth)
        {
            if (depth < 0 || (uint)block >= (uint)blocks.Count)
                throw new InvalidOperationException("Invalid executable operand edge.");
            if (depths[block] is int previous)
            {
                if (previous != depth)
                    throw new InvalidOperationException($"Executable operand join mismatch in block {block}: {previous} versus {depth}.");
                return;
            }
            depths[block] = depth;
            (pending ??= new Queue<int>()).Enqueue(block);
        }
    }

    private static int FirstProtectedRegion(ExecutableExceptionRegion[] regions, int block)
    {
        var start = 0;
        var end = regions.Length;
        while (start < end)
        {
            var middle = start + (end - start) / 2;
            if (regions[middle].ProtectedStartIndex < block) start = middle + 1;
            else end = middle;
        }
        return start;
    }

    // Forward adjacent pure leaf inputs before assigning physical positions.
    // Checked locals keep their original spans in cold metadata; neither branch
    // initialization facts nor their invalidation inventories are needed.
    internal static List<(int Block, int Instruction, int Input, LythonSourceSpan Span)> ForwardValues(
        ExecutableCodeObject code, List<List<ExecutableInstruction>> blocks, ExecutableCallSite[] sites, CancellationToken token)
    {
        var reads = new List<(int Block, int Instruction, int Input, LythonSourceSpan Span)>();
        for (var block = 0; block < blocks.Count; block++)
        {
            PythonTokenization.CheckCancellation(token, block);
            var instructions = blocks[block];
            var kept = 0;
            for (var index = 0; index < instructions.Count; index++)
            {
                PythonTokenization.CheckCancellation(token, index);
                var instruction = instructions[index];
                // Site facts use original adjacency, before forwarding can
                // remove or rewrite the instructions supplying operands.
                if (instruction.OpCode is ExecutableOpCode.Call or ExecutableOpCode.LoadCallMember)
                    code.PrepareCallSite(instructions, index, instruction, sites);
                if (instruction.OpCode == ExecutableOpCode.StoreLocal && code.CapturedLocalSlots.Count != 0 &&
                    code.CapturedLocalSlots.Contains(instruction.LocalSlot))
                    instruction = instruction.WithCapturedLocalStore();
                var count = instruction.ValueInputCount;
                var first = instruction.InputStart;
                var second = instruction.SecondInput;
                var third = count == 3 ? instruction.ThirdInput : int.MaxValue;
                LythonSourceSpan? firstSpan = null, secondSpan = null, thirdSpan = null;
                for (var input = count - 1; input >= 0 && kept != 0; input--)
                {
                    var leaf = instructions[kept - 1];
                    int source;
                    LythonSourceSpan? span = null;
                    if (leaf.OpCode == ExecutableOpCode.LoadConst) source = ~leaf.ConstantIndex;
                    else if (leaf.OpCode == ExecutableOpCode.LoadGlobal)
                    {
                        // Negative sources address immutable constants followed
                        // by names. Resolve the name at consumption; never retain
                        // its value or move a read across an intervening effect.
                        source = ~checked(code.Constants.Length + leaf.NameIndex);
                        span = leaf.Span;
                    }
                    else if (leaf.OpCode == ExecutableOpCode.LoadLocal && !code.RequiresLocalVariableMirroring
                        && !code.CapturedLocalSlots.Contains(leaf.LocalSlot))
                    {
                        source = leaf.LocalSlot;
                        span = leaf.Span;
                    }
                    else break;
                    if (input == 0) { first = source; firstSpan = span; }
                    else if (input == 1) { second = source; secondSpan = span; }
                    else { third = source; thirdSpan = span; }
                    kept--;
                }
                if (count != 0) instruction = instruction.WithValueInputs(first, second, third);
                // Keep the consumer as the pending-cleanup continuation and for its
                // own guard/span. Direct sources do not consume a producer result.
                if (kept > 0 &&
                    (((instruction.OpCode is ExecutableOpCode.StoreLocal or ExecutableOpCode.StoreFunctionDefault) &&
                        first == int.MaxValue
                        && CanForwardResult(instructions[kept - 1], code.Constants))
                    || (instruction.OpCode == ExecutableOpCode.PopTop
                        && instructions[kept - 1].OpCode == ExecutableOpCode.Call)))
                    instructions[kept - 1] = instructions[kept - 1].WithResultConsumer();
                if (firstSpan is not null) reads.Add((block, kept, 0, firstSpan));
                if (secondSpan is not null) reads.Add((block, kept, 1, secondSpan));
                if (thirdSpan is not null) reads.Add((block, kept, 2, thirdSpan));
                instructions[kept++] = instruction;
            }
            if (kept != instructions.Count)
                instructions.RemoveRange(kept, instructions.Count - kept);
        }
        return reads;
    }

    private static bool CanForwardResult(ExecutableInstruction instruction, IReadOnlyList<object?> constants)
        => instruction.OpCode switch
        {
            ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadClosure or
                ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName or ExecutableOpCode.LoadMember or
                ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or
                ExecutableOpCode.MakeSet or ExecutableOpCode.MakeDict or ExecutableOpCode.MakeRecord or ExecutableOpCode.MakeSlice or
                ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Unary or
                ExecutableOpCode.Subscript or ExecutableOpCode.Slice or ExecutableOpCode.Call or
                ExecutableOpCode.ForNext or ExecutableOpCode.FinishFormattedString or ExecutableOpCode.ConvertFormattedValue => true,
            ExecutableOpCode.FormatField => constants[instruction.ConstantIndex] is ExecutableFormatField { Append: false },
            ExecutableOpCode.ApplyOperation => constants[instruction.ConstantIndex] is
                ExecutableStartDisplay or ExecutableFinishComprehension or ExecutableFinishTuple or
                ExecutableFinishCall or ExecutableCallWithKeywordMapping or ExecutableCreateGeneratorExpression or
                ExecutableCreateFunction or ExecutableCreateLambda,
            _ => false,
        };

    private static (int Pop, int Push, int Minimum) Effect(ExecutableInstruction instruction,
        IReadOnlyList<object?> constants, IReadOnlyList<ExecutableCallSite> calls, int firstTemporarySlot)
    {
        var (pop, push) = instruction.OpCode switch
        {
            ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadClosure or
                ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName or ExecutableOpCode.StartFormattedString => (0, 1),
            ExecutableOpCode.StoreLocal or ExecutableOpCode.StoreClosure or ExecutableOpCode.StoreGlobal or
                ExecutableOpCode.StoreName or ExecutableOpCode.PopTop or ExecutableOpCode.JumpIfFalse or
                ExecutableOpCode.MatchCase or ExecutableOpCode.Return or ExecutableOpCode.ExitContextManager => (1, 0),
            ExecutableOpCode.Dup or ExecutableOpCode.LoadCallMember => (1, 2),
            ExecutableOpCode.LoadMember or ExecutableOpCode.GetIter or
                ExecutableOpCode.ResolveContextManager or ExecutableOpCode.EnterContextManager or
                ExecutableOpCode.Unary or ExecutableOpCode.MatchException or
                ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FinishFormattedString or
                ExecutableOpCode.Yield or ExecutableOpCode.YieldFrom => (1, 1),
            ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Subscript => (2, 1),
            ExecutableOpCode.CompareJumpIfFalse => (2, 0),
            ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet => (instruction.ItemCount, 1),
            ExecutableOpCode.MakeDict => (checked(2 * instruction.PairCount), 1),
            ExecutableOpCode.MakeRecord => (((Lokad.Lython.Runtime.PyDict.RecordKeys)constants[instruction.ConstantIndex]!).Count, 1),
            ExecutableOpCode.Call => (checked(calls[instruction.CallSiteIndex].ArgumentCount + calls[instruction.CallSiteIndex].ArgumentOffset), 1),
            ExecutableOpCode.Slice => (1 + BitOperations.PopCount((uint)instruction.SliceParts), 1),
            ExecutableOpCode.MakeSlice => (BitOperations.PopCount((uint)instruction.SliceParts), 1),
            ExecutableOpCode.ForNext => (0, 1),
            ExecutableOpCode.Import or
                ExecutableOpCode.PreloadImplicitSuperArguments or ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or
                ExecutableOpCode.BindException or ExecutableOpCode.ReraiseException or ExecutableOpCode.ClearException or
                ExecutableOpCode.EndFinally or ExecutableOpCode.ReturnNone => (0, 0),
            ExecutableOpCode.ReadRetainedSubscript or ExecutableOpCode.ReadRetainedSlice => (0, 1),
            ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast => (2, 0),
            ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast => (3, 0),
            ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast => (5, 0),
            ExecutableOpCode.UnpackValues => (1, instruction.UnpackLayout.TargetCount),
            ExecutableOpCode.FailAssertion => (instruction.HasAssertionMessage ? 1 : 0, 0),
            ExecutableOpCode.Raise => (instruction.HasRaiseCause ? 2 : 1, 0),
            ExecutableOpCode.DeleteName => (0, 0),
            ExecutableOpCode.DeleteMember => (1, 0),
            ExecutableOpCode.DeleteSubscript => (2, 0),
            ExecutableOpCode.DeleteSlice => (1 + BitOperations.PopCount((uint)instruction.SliceParts), 0),
            ExecutableOpCode.AppendFormattedText => (0, 0),
            ExecutableOpCode.StartFunctionDefaults => (0, instruction.StartsKeywordDefaultsOnly ? 1
                : ((FunctionParameterLayout)constants[instruction.ConstantIndex]!).DefaultStorageCount),
            ExecutableOpCode.StoreFunctionDefault => (1, 0),
            ExecutableOpCode.FormatField => constants[instruction.ConstantIndex] is ExecutableFormatField field
                ? (field.DynamicSpecifier ? 2 : 1, field.Append ? 0 : 1)
                : throw new InvalidOperationException("Missing formatted-field descriptor."),
            ExecutableOpCode.ApplyOperation => OperationEffect((ExecutableOperation)constants[instruction.ConstantIndex]!),
            _ => throw new InvalidOperationException($"Unknown executable operand effect: {instruction.OpCode}.")
        };
        var minimum = instruction.OpCode switch
        {
            ExecutableOpCode.ForNext or ExecutableOpCode.AppendFormattedText => 1,
            ExecutableOpCode.FormatField when constants[instruction.ConstantIndex] is ExecutableFormatField { Append: true } => checked(pop + 1),
            ExecutableOpCode.ReadRetainedSubscript => 2,
            ExecutableOpCode.ReadRetainedSlice => 4,
            ExecutableOpCode.StoreFunctionDefault => checked(1 + instruction.DefaultStorageDepth),
            _ => pop
        };
        if (instruction.OpCode == ExecutableOpCode.ApplyOperation)
        {
            minimum = constants[instruction.ConstantIndex] switch
            {
                ExecutableAppendDisplay item => checked(2 + item.IteratorDepth),
                ExecutableStoreDictionaryItem item => checked(pop + 1 + item.IteratorDepth),
                ExecutableAppendCall => 2,
                _ => pop
            };
        }
        if (instruction.ValueInputCount != 0)
        {
            var forwarded = instruction.InputStart < firstTemporarySlot ? 1 : 0;
            if (instruction.ValueInputCount > 1 && instruction.SecondInput < firstTemporarySlot) forwarded++;
            if (instruction.ValueInputCount == 3 && instruction.ThirdInput < firstTemporarySlot) forwarded++;
            pop -= forwarded;
            minimum -= forwarded;
        }
        return (pop, push, minimum);
    }

    private static (int Pop, int Push) OperationEffect(ExecutableOperation operation) => operation switch
    {
        ExecutableStartDisplay or ExecutableStartClassHeader => (0, 1),
        ExecutableStartComprehension => (1, 3),
        ExecutableFinishComprehension => (2, 1),
        ExecutableFinishCall => (1, 1),
        ExecutableCallWithKeywordMapping call => (checked(call.PositionalCount + 2), 1),
        ExecutableFinishTuple or ExecutableStartCall or ExecutableCreateGeneratorExpression => (1, 1),
        ExecutableAppendDisplay or ExecutableAppendCall or ExecutableStoreModuleAnnotation => (1, 0),
        ExecutableBindTypeAlias => (0, 0),
        ExecutableStoreDictionaryItem item => (item.Unpacking ? 1 : 2, 0),
        ExecutableCreateFunction function => (function.InputCount, 1),
        ExecutableCreateLambda lambda => (lambda.InputCount, 1),
        ExecutableCreateClass type => (checked(type.Syntax.Decorators.Count +
            (type.ExpandedHeader ? 1 : type.DirectHeaderCount)), 0),
        _ => throw new InvalidOperationException($"Unknown prepared operand effect: {operation.GetType().Name}.")
    };
}
