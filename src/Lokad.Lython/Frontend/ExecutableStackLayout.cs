using System.Numerics;

namespace Lokad.Lython.Frontend;

// Operand shapes are a compiler invariant. Guest iteration, expansion and
// suspension change values, never the number of executable operand slots.
internal sealed record ExecutableStackLayout(int MaximumDepth, int?[] EntryDepths, int FirstTemporarySlot,
    int LastOwnedTemporarySlot)
{
    internal static ExecutableInstruction[] Linearize(IReadOnlyList<ExecutableBasicBlock> blocks,
        ExecutableStackLayout layout, out int[] offsets)
    {
        offsets = new int[blocks.Count + 1];
        for (var i = 0; i < blocks.Count; i++)
            offsets[i + 1] = checked(offsets[i] + blocks[i].InstructionArray.Length + (Completes(blocks[i]) ? 1 : 0));
        var instructions = new ExecutableInstruction[offsets[^1]];
        for (var i = 0; i < blocks.Count; i++)
        {
            var source = blocks[i].InstructionArray;
            for (var j = 0; j < source.Length; j++)
            {
                var instruction = source[j];
                instructions[offsets[i] + j] = instruction.OpCode switch
                {
                    ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or ExecutableOpCode.JumpIfFalse or ExecutableOpCode.CompareJumpIfFalse or
                        ExecutableOpCode.ForNext or ExecutableOpCode.EndFinally => instruction.WithTargetIndex(offsets[instruction.TargetIndex]),
                    ExecutableOpCode.MatchCase => instruction.WithFailureIndex(offsets[instruction.FailureIndex]),
                    _ => instruction
                };
            }
            // Stored blocks never fell through. Preserve implicit completion
            // distinctly from a guest top-level return (not legal in imports).
            if (Completes(blocks[i]))
            {
                var extent = source.Length == 0 ? checked(layout.FirstTemporarySlot + (layout.EntryDepths[i] ?? 0)) : source[^1].LiveExtent;
                instructions[offsets[i + 1] - 1] = ExecutableInstruction.Complete(
                    source.Length == 0 ? new LythonSourceSpan(0, 0, 0, 0) : source[^1].Span)
                    .WithStorageSlots(extent, extent, extent);
            }
        }
        return instructions;

        static bool Completes(ExecutableBasicBlock block) => block.InstructionArray.Length == 0 ||
            block.InstructionArray[^1].OpCode is not (ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or
                ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone or ExecutableOpCode.ReraiseException);
    }

    internal static ExecutableStackLayout Prepare(IReadOnlyList<ExecutableBasicBlock> blocks,
        IReadOnlyList<object?> constants, IReadOnlyList<ExecutableCallSite> calls,
        IReadOnlyList<ExecutableExceptionRegion> regions, int entryBlock, int initialDepth, int firstTemporarySlot)
    {
        var depths = new int?[blocks.Count];
        var pending = new Queue<int>();
        var maximum = initialDepth;
        // Generator expressions arrive with their acquired outer cursor. Other
        // guest values never expose the interpreter's private temporary wrappers.
        var lastOwnedTemporarySlot = initialDepth == 0 ? -1 : checked(firstTemporarySlot + initialDepth - 1);
        Enter(entryBlock, initialDepth);
        while (pending.TryDequeue(out var blockIndex))
        {
            var depth = depths[blockIndex]!.Value;
            foreach (var region in regions)
            {
                if (region.ProtectedStartIndex != blockIndex) continue;
                if (region.ExceptIndex is int except) Enter(except, depth);
                if (region.FinallyIndex is int cleanup) Enter(cleanup, depth);
            }

            var instructions = blocks[blockIndex].InstructionArray;
            for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
            {
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
                    // Conservatively include every duplicate, without another
                    // value-liveness analysis or runtime resource registry.
                    ExecutableOpCode.Dup => resultSlot + 1,
                    ExecutableOpCode.ApplyOperation when constants[instruction.ConstantIndex] is
                        ExecutableStartComprehension or ExecutableStartCall or
                        ExecutableStartClassHeader => resultSlot,
                    _ => -1
                };
                if (ownedSlot >= 0)
                    lastOwnedTemporarySlot = Math.Max(lastOwnedTemporarySlot, checked(firstTemporarySlot + ownedSlot));
                maximum = Math.Max(maximum, depth);
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.ForNext:
                        Enter(instruction.TargetIndex, depth - 2);
                        break;
                    case ExecutableOpCode.JumpIfFalse:
                    case ExecutableOpCode.CompareJumpIfFalse:
                        Enter(instruction.TargetIndex, depth);
                        break;
                    case ExecutableOpCode.MatchCase:
                        Enter(instruction.FailureIndex, depth);
                        break;
                    case ExecutableOpCode.AbruptJump:
                        Enter(instruction.TargetIndex, depth - (instruction.DiscardIterator ? 1 : 0));
                        goto CompleteBlock;
                    case ExecutableOpCode.Jump:
                    case ExecutableOpCode.EndFinally:
                        Enter(instruction.TargetIndex, depth);
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
        }
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
            pending.Enqueue(block);
        }
    }

    // Forward adjacent pure leaf inputs before assigning physical positions.
    // Checked locals keep their original spans in cold metadata; neither branch
    // initialization facts nor their invalidation inventories are needed.
    internal static List<(int Block, int Instruction, int Input, LythonSourceSpan Span)> ForwardValues(
        ExecutableCodeObject code, List<ExecutableBasicBlock> blocks)
    {
        var reads = new List<(int Block, int Instruction, int Input, LythonSourceSpan Span)>();
        for (var block = 0; block < blocks.Count; block++)
        {
            var instructions = blocks[block].InstructionArray;
            var kept = 0;
            for (var index = 0; index < instructions.Length; index++)
            {
                var instruction = instructions[index];
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
                // Keep the store as the pending-cleanup continuation and for its
                // own guard/span. Direct sources do not consume a producer result.
                if (instruction.OpCode == ExecutableOpCode.StoreLocal && first == int.MaxValue && kept > 0
                    && CanForwardResult(instructions[kept - 1], code.Constants))
                    instructions[kept - 1] = instructions[kept - 1].WithLocalResult();
                if (firstSpan is not null) reads.Add((block, kept, 0, firstSpan));
                if (secondSpan is not null) reads.Add((block, kept, 1, secondSpan));
                if (thirdSpan is not null) reads.Add((block, kept, 2, thirdSpan));
                instructions[kept++] = instruction;
            }
            if (kept != instructions.Length)
                blocks[block] = blocks[block] with { InstructionArray = instructions[..kept] };
        }
        return reads;
    }

    private static bool CanForwardResult(ExecutableInstruction instruction, IReadOnlyList<object?> constants)
        => instruction.OpCode switch
        {
            ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadClosure or
                ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName or ExecutableOpCode.LoadMember or
                ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or
                ExecutableOpCode.MakeSet or ExecutableOpCode.MakeDict or ExecutableOpCode.MakeSlice or
                ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Unary or
                ExecutableOpCode.Subscript or ExecutableOpCode.Slice or ExecutableOpCode.Call or
                ExecutableOpCode.ForNext or ExecutableOpCode.FinishFormattedString or ExecutableOpCode.ConvertFormattedValue => true,
            ExecutableOpCode.FormatField => constants[instruction.ConstantIndex] is ExecutableFormatField { Append: false },
            ExecutableOpCode.ApplyOperation => constants[instruction.ConstantIndex] is
                ExecutableStartDisplay or ExecutableFinishComprehension or ExecutableFinishTuple or
                ExecutableFinishCall or ExecutableCreateGeneratorExpression or
                ExecutableCreateLambda or ExecutablePostponedAnnotation or ExecutableAnnotationsEnabled,
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
            ExecutableOpCode.Call => (checked(calls[instruction.CallSiteIndex].ArgumentCount + calls[instruction.CallSiteIndex].ArgumentOffset), 1),
            ExecutableOpCode.Slice => (1 + BitOperations.PopCount((uint)instruction.SliceParts), 1),
            ExecutableOpCode.MakeSlice => (BitOperations.PopCount((uint)instruction.SliceParts), 1),
            ExecutableOpCode.ForNext => (0, 1),
            ExecutableOpCode.Import or ExecutableOpCode.DefineFunction or ExecutableOpCode.ExecuteFallbackStatement or
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
        ExecutableStartDisplay or ExecutableStartClassHeader or ExecutablePostponedAnnotation or ExecutableAnnotationsEnabled => (0, 1),
        ExecutableStartComprehension => (1, 3),
        ExecutableFinishComprehension => (2, 1),
        ExecutableFinishTuple or ExecutableStartCall or ExecutableFinishCall or ExecutableCreateGeneratorExpression => (1, 1),
        ExecutableAppendDisplay or ExecutableAppendCall or ExecutableStoreModuleAnnotation => (1, 0),
        ExecutableStoreDictionaryItem item => (item.Unpacking ? 1 : 2, 0),
        ExecutableCreateFunction function => (function.Inputs.Count, 0),
        ExecutableCreateLambda lambda => (lambda.Expression.Parameters.Count(p => p.DefaultValue is not null), 1),
        ExecutableCreateClass type => (checked(type.Definition.Decorators.Count + (type.ExpandedHeader ? 1 : 0)), 0),
        _ => throw new InvalidOperationException($"Unknown prepared operand effect: {operation.GetType().Name}.")
    };
}
