using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// Operand shapes are a compiler invariant. Guest iteration, expansion and
// suspension change values, never the number of executable operand slots.
internal sealed record ExecutableStackLayout(int MaximumDepth, ExecutableStackLayout.Block[] Blocks, int FirstTemporarySlot,
    int LastOwnedTemporarySlot, List<ExecutableStackLayout.OperandRead> OperandReads)
{
    internal readonly record struct OperandRead(int Block, int Instruction, int Input, LythonSourceSpan Span);

    internal struct Block
    {
        // A zero-initialized entry is unreachable. Complementing a live depth
        // preserves every nonnegative value, including an empty operand stack.
        private int _encodedDepth;
        internal readonly bool IsReachable => _encodedDepth != 0;
        internal int EntryDepth { readonly get => ~_encodedDepth; set => _encodedDepth = ~value; }
        // Read-list start while preparing a reachable block, tape offset once
        // linearization has placed that block and its diagnostic spans.
        internal int Offset;
        internal int EncodedTarget;
    }

    internal static Block[] CreatePositions(int count, IReadOnlyList<ExecutableExceptionRegion> regions,
        CancellationToken token)
    {
        var positions = new Block[checked(count + 1)];
        for (var i = 0; i < regions.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            // Handlers need the operand depth at their protected entry even
            // when that entry consists only of an unconditional jump.
            var start = regions[i].ProtectedStartIndex;
            positions[start].EncodedTarget = checked(start + 1);
        }
        return positions;
    }

    internal static int ResolveTarget(IReadOnlyList<List<ExecutableInstruction>> blocks, Block[] positions, int start,
        CancellationToken token)
    {
        var targets = positions.AsSpan(0, positions.Length - 1);
        var target = targets[start].EncodedTarget;
        return target > 0 ? target - 1 : ResolveUnseenTarget(blocks, targets, start, token);
    }

    private static int ResolveUnseenTarget(IReadOnlyList<List<ExecutableInstruction>> blocks, Span<Block> targets,
        int start, CancellationToken token)
    {
        const int unseen = 0, visiting = -1;
        var current = start;
        for (var step = 0; targets[current].EncodedTarget == unseen; step++)
        {
            PythonTokenization.CheckCancellation(token, step);
            var next = Successor(blocks[current]);
            if (next == -1)
            {
                targets[current].EncodedTarget = current + 1;
                break;
            }
            targets[current].EncodedTarget = visiting;
            current = next;
        }

        var target = targets[current].EncodedTarget;
        if (target == visiting)
        {
            // Preserve every cycle node, including the first node a tail
            // enters. The walk has no recursive calls or separate path list.
            target = current + 1;
            var step = 0;
            do
            {
                PythonTokenization.CheckCancellation(token, step++);
                targets[current].EncodedTarget = current + 1;
                current = Successor(blocks[current]);
            } while (current + 1 != target);
        }

        current = start;
        for (var step = 0; targets[current].EncodedTarget == visiting; step++)
        {
            PythonTokenization.CheckCancellation(token, step);
            targets[current].EncodedTarget = target;
            current = Successor(blocks[current]);
        }
        return target - 1;

        static int Successor(List<ExecutableInstruction> block)
            => block.Count == 1 && block[0].OpCode == ExecutableOpCode.Jump ? block[0].TargetIndex : -1;
    }

    internal static ExecutableInstruction[] Linearize(ExecutableCodeObject code, IReadOnlyList<List<ExecutableInstruction>> blocks,
        ExecutableStackLayout layout, bool hasNoExceptionRegions, CancellationToken token, out ExecutableOperandSpan[] readSpans)
    {
        var positions = layout.Blocks;
        var reads = layout.OperandReads;
        readSpans = reads.Count == 0 ? [] : new ExecutableOperandSpan[reads.Count];
        var nextRead = 0;
        var offset = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            ref var position = ref positions[i];
            var firstRead = position.Offset;
            position.Offset = offset;
            if (!position.IsReachable) continue;
            // The operand analysis discovers blocks in control-flow order.
            // Here each block's contiguous reads follow the final tape order.
            for (var index = firstRead; index < reads.Count && reads[index].Block == i; index++)
            {
                PythonTokenization.CheckCancellation(token, index - firstRead);
                var read = reads[index];
                readSpans[nextRead++] = new(((long)(offset + read.Instruction) << 2) | (uint)read.Input, read.Span);
            }
            offset = checked(offset + blocks[i].Count + (Completes(blocks[i]) ? 1 : 0));
        }
        positions[^1].Offset = offset;
        var instructions = new ExecutableInstruction[offset];
        for (var i = 0; i < blocks.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            var position = positions[i];
            if (!position.IsReachable) continue;
            var source = blocks[i];
            var unpackedStart = 0;
            var unpackedWidth = 0;
            for (var j = 0; j < source.Count; j++)
            {
                PythonTokenization.CheckCancellation(token, j);
                var instruction = source[j];
                if (instruction.OpCode == ExecutableOpCode.ForNext && j + 1 < source.Count &&
                    CanUnpackIteration(instruction, source[j + 1]))
                    (unpackedStart, unpackedWidth) = GetUnpackedLocals(code, source, j + 1);
                else if (unpackedWidth != 0)
                {
                    if (instruction.OpCode == ExecutableOpCode.LoadLocal &&
                        (uint)(instruction.LocalSlot - unpackedStart) < (uint)unpackedWidth)
                    {
                        var call = GetDictionaryCallOffset(code, source, j);
                        if (call != 0) instruction = instruction.WithDictionaryGetKey(call);
                    }
                    else if (instruction.OpCode == ExecutableOpCode.StoreSubscript &&
                        (uint)(instruction.ThirdInput - unpackedStart) < (uint)unpackedWidth)
                        instruction = instruction.WithBorrowableTextStore();
                }
                instructions[position.Offset + j] = instruction.OpCode switch
                {
                    ExecutableOpCode.AbruptJump => instruction.WithTargetIndex(positions[instruction.TargetIndex].Offset),
                    ExecutableOpCode.ForNext when j + 1 < source.Count && CanUnpackIteration(instruction, source[j + 1])
                        => instruction.WithTargetIndex(ResolvedOffset(instruction.TargetIndex)).WithFixedUnpack(),
                    ExecutableOpCode.Binary when !instruction.HasResultConsumer && j + 1 < source.Count &&
                        CanContinueInteger(source[j + 1], instruction.ResultSlot, layout.FirstTemporarySlot)
                        => instruction.WithIntegerContinuation(),
                    ExecutableOpCode.Jump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse or
                        ExecutableOpCode.ForNext or ExecutableOpCode.EndFinally => instruction.WithTargetIndex(ResolvedOffset(instruction.TargetIndex)),
                    ExecutableOpCode.MatchCase => instruction.WithFailureIndex(ResolvedOffset(instruction.FailureIndex)),
                    _ => instruction
                };
                if (instruction.OpCode == ExecutableOpCode.Call && !code.RequiresLocalVariableMirroring)
                    PrepareIntegerCallUpdate(code, instructions, source, position.Offset, j, token);
                if (instruction.OpCode == ExecutableOpCode.MakeTuple && hasNoExceptionRegions &&
                    code.UsesOnlyLexicalSlots && !code.RequiresLocalVariableMirroring &&
                    code.ValueStorageCapacity <= LythonRuntime.NativeValueCapacity)
                    PrepareIntegerTupleReturn(code, instructions, source, position.Offset, j);
            }
            // Stored blocks never fell through. Preserve implicit completion
            // distinctly from a guest top-level return (not legal in imports).
            if (Completes(blocks[i]))
            {
                var extent = source.Count == 0 ? checked(layout.FirstTemporarySlot + position.EntryDepth) : source[^1].LiveExtent;
                instructions[positions[i + 1].Offset - 1] = ExecutableInstruction.Complete(
                    source.Count == 0 ? new LythonSourceSpan(0, 0, 0, 0) : source[^1].Span)
                    .WithStorageSlots(extent, extent, extent);
            }
        }
        System.Diagnostics.Debug.Assert(nextRead == readSpans.Length);
        return instructions;

        int ResolvedOffset(int block) => positions[ResolveTarget(blocks, positions, block, token)].Offset;

        static bool CanUnpackIteration(ExecutableInstruction iteration, ExecutableInstruction next)
            => !iteration.HasResultConsumer && next.OpCode == ExecutableOpCode.UnpackValues
                && next.InputStart == iteration.ResultSlot && next.ResultSlot == iteration.ResultSlot
                && next.UnpackLayout is { HasStarredTarget: false, TargetCount: > 0 };

        // Only the fixed local targets in this block can receive the parser's
        // deferred text slots. Other blocks retain their ordinary instructions.
        static (int Start, int Width) GetUnpackedLocals(ExecutableCodeObject code,
            List<ExecutableInstruction> block, int unpackIndex)
        {
            var width = block[unpackIndex].UnpackLayout.TargetCount;
            if (code.RequiresLocalVariableMirroring || width > LythonRuntime.NativeValueCapacity ||
                block.Count - unpackIndex <= width || block[unpackIndex + 1].OpCode != ExecutableOpCode.StoreLocal)
                return default;
            var start = block[unpackIndex + 1].LocalSlot;
            for (var i = 0; i < width; i++)
            {
                var store = block[unpackIndex + 1 + i];
                if (store.OpCode != ExecutableOpCode.StoreLocal || store.StoresCapturedLocal ||
                    store.LocalSlot != start + i)
                    return default;
            }
            return (start, width);
        }

        static int GetDictionaryCallOffset(ExecutableCodeObject code, List<ExecutableInstruction> block, int key)
        {
            if (key == 0 || key + 2 >= block.Count ||
                block[key - 1] is not { OpCode: ExecutableOpCode.LoadCallMember } member ||
                code.Names[member.NameIndex] != "get")
                return 0;
            var offset = block[key + 1].OpCode == ExecutableOpCode.LoadConst ? 2 : 1;
            if (key + offset + 1 >= block.Count ||
                block[key + offset] is not { OpCode: ExecutableOpCode.Call, HasResultConsumer: true } call ||
                block[key + offset + 1].OpCode != ExecutableOpCode.StoreLocal ||
                code.CallSites[call.CallSiteIndex] is not { HasReceiver: true, IsPositionalOnly: true } site ||
                site.ArgumentCount != offset)
                return 0;
            return offset;
        }

        static bool CanContinueInteger(in ExecutableInstruction next, int resultSlot, int firstTemporarySlot)
            => next.OpCode == ExecutableOpCode.Binary && next.ResultSlot == resultSlot &&
                (next.InputStart == resultSlot && next.SecondInput < firstTemporarySlot ||
                 next.SecondInput == resultSlot && next.InputStart < firstTemporarySlot);

        static bool Completes(List<ExecutableInstruction> block) => block.Count == 0 ||
            block[^1].OpCode is not (ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or
                ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException);
    }

    private static void PrepareIntegerTupleReturn(ExecutableCodeObject code, ExecutableInstruction[] tape,
        List<ExecutableInstruction> block, int offset, int index)
    {
        var tuple = block[index];
        var first = index - tuple.ItemCount;
        if (tuple.ItemCount < 2 || tuple.HasResultConsumer || first < 0 ||
            index + 1 != block.Count - 1 || block[index + 1].OpCode != ExecutableOpCode.Return ||
            block[index + 1].InputStart != tuple.ResultSlot ||
            block[first].OpCode != ExecutableOpCode.LoadLocal) return;

        // Each operation fills one final element. A direct local read appears
        // at most once, so a fresh value has only one surviving identity.
        uint locals = 0;
        for (var i = first; i < index; i++)
        {
            var producer = block[i];
            if (producer.HasResultConsumer || producer.ResultSlot != tuple.InputStart + i - first ||
                producer.LiveExtent != producer.ResultSlot + 1) return;
            if (producer.OpCode == ExecutableOpCode.LoadLocal)
            {
                var bit = 1u << producer.LocalSlot;
                if ((locals & bit) != 0 || code.CapturedLocalSlots.Contains(producer.LocalSlot)) return;
                locals |= bit;
            }
            else if (producer.OpCode != ExecutableOpCode.Binary ||
                !IsPrivateInput(producer.InputStart) || !IsPrivateInput(producer.SecondInput)) return;
        }
        tape[offset + first] = block[first].WithIntegerTupleReturn(offset + index);

        bool IsPrivateInput(int source) => source >= 0 ? source < code.FirstTemporarySlot : ~source < code.Constants.Length;
    }

    private static void PrepareIntegerCallUpdate(ExecutableCodeObject code, ExecutableInstruction[] tape,
        List<ExecutableInstruction> block, int offset, int index, CancellationToken token)
    {
        var call = block[index];
        if (index < 3 || index + 2 >= block.Count || call.HasResultConsumer || call.ValueInputCount != 0 ||
            code.CallSites[call.CallSiteIndex] is not { HasReceiver: false, IsPositionalOnly: true, ArgumentCount: > 0 }
                site || site.UnpackedReturnCount != 0)
            return;
        var update = block[index + 1];
        var store = block[index + 2];
        if (update.OpCode != ExecutableOpCode.Augmented || !update.HasResultConsumer ||
            update.AugmentedOperator != AugmentedAssignmentOperatorSyntax.Add ||
            update.SecondInput != call.ResultSlot ||
            store.OpCode != ExecutableOpCode.StoreLocal || store.StoresCapturedLocal ||
            store.InputStart != update.ResultSlot || store.LiveExtent != update.ResultSlot)
            return;

        // This block has no entry between the early reads and their consumer.
        // Reuse the ordinary dead argument window. Every provisional value is
        // an exact integer, so declining the segment cannot leave a loan.
        var argumentStart = call.InputStart + site.ArgumentOffset;
        var limit = argumentStart + site.ArgumentCount;
        var first = index - 1;
        for (; first >= 2; first--)
        {
            PythonTokenization.CheckCancellation(token, index - first);
            var argument = block[first];
            if (argument.ResultSlot < argumentStart) break;
            if (argument.ResultSlot >= limit || argument.LiveExtent > limit || argument.HasResultConsumer ||
                argument.OpCode is not (ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadConst or ExecutableOpCode.Binary))
                return;
            if (argument.OpCode == ExecutableOpCode.Binary &&
                (argument.InputStart < 0 && ~argument.InputStart >= code.Constants.Length ||
                 argument.SecondInput < 0 && ~argument.SecondInput >= code.Constants.Length))
                return;
        }
        if (first < 1 || block[first].OpCode is not (ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadGlobal) ||
            block[first].ResultSlot != call.InputStart || block[first].LiveExtent != argumentStart ||
            block[first - 1] is not { HasResultConsumer: false } load ||
            load.OpCode is not (ExecutableOpCode.LoadLocal or ExecutableOpCode.CheckLocal) ||
            load.LiveExtent != call.InputStart || load.LocalSlot != store.LocalSlot ||
            update.InputStart != (load.OpCode == ExecutableOpCode.CheckLocal ? load.LocalSlot : load.ResultSlot) ||
            update.ResultSlot != load.ResultSlot)
            return;
        tape[offset + first - 1] = load.WithIntegerUpdateCall(offset + index);
    }

    internal static ExecutableStackLayout Prepare(ExecutableCodeObject code, IReadOnlyList<List<ExecutableInstruction>> blocks,
        ExecutableCallSite[] calls, ExecutableExceptionRegion[] regions,
        int entryBlock, int initialDepth, CancellationToken token)
    {
        if (initialDepth < 0 || (uint)entryBlock >= (uint)blocks.Count)
            throw new InvalidOperationException("Invalid executable entry operand depth.");
        var positions = CreatePositions(blocks.Count, regions, token);
        var constants = code.Constants;
        var firstTemporarySlot = code.FirstTemporarySlot;
        var canForwardReturn = code.IsFunctionScope && code.UsesOnlyLexicalSlots &&
            !code.RequiresLocalVariableMirroring && regions.Length == 0;
        List<OperandRead> reads = [];
        Queue<int>? pending = null;
        var maximum = initialDepth;
        // Generator expressions arrive with their acquired outer cursor. Other
        // guest values never expose the interpreter's private temporary wrappers.
        var lastOwnedTemporarySlot = initialDepth == 0 ? -1 : checked(firstTemporarySlot + initialDepth - 1);
        positions[entryBlock].EntryDepth = initialDepth;
        var blockIndex = entryBlock;
        do
        {
            PythonTokenization.CheckCancellation(token, 0);
            var depth = positions[blockIndex].EntryDepth;
            if (regions.Length != 0)
            {
                for (var i = FirstProtectedRegion(regions, blockIndex);
                     i < regions.Length && regions[i].ProtectedStartIndex == blockIndex; i++)
                {
                    var region = regions[i];
                    if (region.ExceptIndex is int except) Enter(ResolveTarget(blocks, positions, except, token), depth);
                    if (region.FinallyIndex is int cleanup) Enter(ResolveTarget(blocks, positions, cleanup, token), depth);
                }
            }

            var instructions = blocks[blockIndex];
            // Forwarding removes only reads that feed an operand. It leaves
            // jump-only blocks and the already resolved successors intact.
            positions[blockIndex].Offset = reads.Count;
            ForwardBlock(code, blockIndex, instructions, calls, reads, canForwardReturn, token);
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
                else if (instruction.OpCode == ExecutableOpCode.Call && instruction.HasForwardedCallOperands)
                    maximum = Math.Max(maximum, checked(depth + 2));
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
                        Enter(ResolveTarget(blocks, positions, instruction.TargetIndex, token), depth - 2);
                        break;
                    case ExecutableOpCode.JumpIfFalse:
                    case ExecutableOpCode.CompareJumpIfFalse:
                        Enter(ResolveTarget(blocks, positions, instruction.TargetIndex, token), depth);
                        break;
                    case ExecutableOpCode.MatchCase:
                        Enter(ResolveTarget(blocks, positions, instruction.FailureIndex, token), depth);
                        break;
                    case ExecutableOpCode.AbruptJump:
                        Enter(instruction.TargetIndex, depth - (instruction.DiscardIterator ? 1 : 0));
                        goto CompleteBlock;
                    case ExecutableOpCode.Jump:
                    case ExecutableOpCode.EndFinally:
                        Enter(ResolveTarget(blocks, positions, instruction.TargetIndex, token), depth);
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
        return new(maximum, positions, firstTemporarySlot, lastOwnedTemporarySlot, reads);

        void Enter(int block, int depth)
        {
            if (depth < 0 || (uint)block >= (uint)blocks.Count)
                throw new InvalidOperationException("Invalid executable operand edge.");
            ref var position = ref positions[block];
            if (position.IsReachable)
            {
                var previous = position.EntryDepth;
                if (previous != depth)
                    throw new InvalidOperationException($"Executable operand join mismatch in block {block}: {previous} versus {depth}.");
                return;
            }
            position.EntryDepth = depth;
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
    private static void ForwardBlock(ExecutableCodeObject code, int block, List<ExecutableInstruction> instructions,
        ExecutableCallSite[] calls, List<OperandRead> reads, bool canForwardReturn, CancellationToken token)
    {
        var kept = 0;
        for (var index = 0; index < instructions.Count; index++)
        {
            PythonTokenization.CheckCancellation(token, index);
            var instruction = instructions[index];
            // Site facts use original adjacency, before forwarding can
            // remove or rewrite the instructions supplying operands.
            if (instruction.OpCode is ExecutableOpCode.Call or ExecutableOpCode.LoadCallMember)
                instruction = code.PrepareCallSite(instructions, index, instruction, calls);
            if (instruction.OpCode == ExecutableOpCode.Call && kept >= 2 &&
                !code.RequiresLocalVariableMirroring &&
                calls[instruction.CallSiteIndex] is { HasReceiver: false, IsPositionalOnly: true, ArgumentCount: 1 } &&
                instructions[kept - 1] is { OpCode: ExecutableOpCode.LoadLocal } argument &&
                !code.CapturedLocalSlots.Contains(argument.LocalSlot) &&
                (instructions[kept - 2] is { OpCode: ExecutableOpCode.LoadGlobal } target &&
                    (code.IsFunctionScope || code.Names[target.NameIndex] == "float") ||
                 instructions[kept - 2] is { OpCode: ExecutableOpCode.LoadLocal } localTarget &&
                    !code.CapturedLocalSlots.Contains(localTarget.LocalSlot) &&
                    index + 1 < instructions.Count && instructions[index + 1].OpCode == ExecutableOpCode.StoreLocal))
                instruction = instruction.WithForwardedCallOperands();
            if (instruction.OpCode == ExecutableOpCode.StoreLocal)
            {
                var captured = code.CapturedLocalSlots.Count != 0 && code.CapturedLocalSlots.Contains(instruction.LocalSlot);
                if (captured) instruction = instruction.WithCapturedLocalStore();
                else if (canForwardReturn && index == instructions.Count - 3 &&
                    instructions[index + 1].OpCode == ExecutableOpCode.LoadLocal &&
                    instruction.LocalSlot == instructions[index + 1].LocalSlot &&
                    instructions[index + 2].OpCode == ExecutableOpCode.Return)
                {
                    // This private assignment has no user after the read.
                    // The existing return consumes the evaluated operand.
                    index++;
                    continue;
                }
            }
            if (instruction.OpCode == ExecutableOpCode.LoadLocal && instruction.CheckedLocalConsumer >= 0 &&
                code.UsesOnlyLexicalSlots && !code.CapturedLocalSlots.Contains(instruction.LocalSlot) &&
                CanReuseCheckedLocal(instructions, index, instruction.CheckedLocalConsumer, instruction.LocalSlot, token))
            {
                var consumer = instruction.CheckedLocalConsumer;
                instructions[consumer] = instructions[consumer].WithValueInputs(instruction.LocalSlot, int.MaxValue);
                instruction = instruction.AsCheckLocal();
            }
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
                || (instruction.OpCode == ExecutableOpCode.StoreClosure && first == int.MaxValue
                    && instructions[kept - 1].OpCode == ExecutableOpCode.Binary)
                || (instruction.OpCode == ExecutableOpCode.PopTop
                    && instructions[kept - 1].OpCode == ExecutableOpCode.Call)))
                instructions[kept - 1] = instructions[kept - 1].WithResultConsumer();
            if (firstSpan is not null) reads.Add(new(block, kept, 0, firstSpan));
            if (secondSpan is not null) reads.Add(new(block, kept, 1, secondSpan));
            if (thirdSpan is not null) reads.Add(new(block, kept, 2, thirdSpan));
            instructions[kept++] = instruction;
        }
        if (kept != instructions.Count)
            instructions.RemoveRange(kept, instructions.Count - kept);
    }

    // A private lexical local remains authoritative during a call. A
    // same-block expression can still write it explicitly with a walrus.
    private static bool CanReuseCheckedLocal(List<ExecutableInstruction> instructions,
        int load, int consumer, int slot, CancellationToken token)
    {
        for (var i = load + 1; i < consumer; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            var item = instructions[i];
            if ((item.OpCode == ExecutableOpCode.StoreLocal && item.LocalSlot == slot) ||
                item.OpCode is ExecutableOpCode.StoreName or ExecutableOpCode.DeleteName)
                return false;
        }
        return true;
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
            ExecutableOpCode.Dup or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCasedSplit => (1, 2),
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
                ExecutableOpCode.EndFinally or ExecutableOpCode.ReturnNone or ExecutableOpCode.CheckLocal => (0, 0),
            ExecutableOpCode.ReadRetainedSubscript or ExecutableOpCode.ReadRetainedSlice => (0, 1),
            ExecutableOpCode.StoreMember or ExecutableOpCode.StoreMemberLast => (2, 0),
            ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast => (3, 0),
            ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast => (5, 0),
            ExecutableOpCode.UnpackValues => (1, instruction.UnpackLayout.TargetCount),
            ExecutableOpCode.ReverseAssignmentValues => (instruction.AssignmentValueCount, instruction.AssignmentValueCount),
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
