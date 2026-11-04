using System.Numerics;

namespace Lokad.Lython.Frontend;

// Operand shapes are a compiler invariant. Guest iteration, expansion and
// suspension change values, never the number of executable operand slots.
internal sealed record ExecutableStackLayout(int MaximumDepth, int?[] EntryDepths, int FirstTemporarySlot)
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
                    ExecutableOpCode.Jump or ExecutableOpCode.AbruptJump or ExecutableOpCode.JumpIfFalse or
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
                var (pop, push, minimum) = Effect(instruction, constants, calls);
                if (depth < minimum)
                    throw new InvalidOperationException($"Executable operand underflow in block {blockIndex}: {instruction.OpCode}, depth {depth}, required {minimum}.");
                // A raw __class_getitem__ descriptor getter temporarily retains
                // its two protocol arguments above the ordinary subscript inputs.
                if (instruction.OpCode == ExecutableOpCode.Subscript)
                    maximum = Math.Max(maximum, checked(depth + 2));
                var resultSlot = depth - pop;
                var inputStart = depth - minimum;
                depth = checked(resultSlot + push);
                // Prepare the owned instruction array in this existing shape
                // pass; linearization preserves these facts while retargeting.
                instructions[instructionIndex] = instruction.WithStorageSlots(
                    checked(firstTemporarySlot + inputStart), checked(firstTemporarySlot + resultSlot),
                    checked(firstTemporarySlot + depth));
                maximum = Math.Max(maximum, depth);
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.ForNext:
                        Enter(instruction.TargetIndex, depth - 2);
                        break;
                    case ExecutableOpCode.JumpIfFalse:
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
                    case ExecutableOpCode.ApplyOperation when constants[instruction.ConstantIndex] is ExecutableRaise or ExecutableFailAssertion:
                        goto CompleteBlock;
                }
            }
            // An unterminated executable block completes its invocation; the
            // existing interpreter never falls through to the next stored block.
        CompleteBlock:;
        }
        return new(maximum, depths, firstTemporarySlot);

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

    // Fixed-arity consumers can read an immutable constant or an already-bound
    // plain local directly. Other consumers keep their evaluated contiguous
    // packets. Only adjacent leaf reads are removed: callbacks and failable
    // reads retain their original order and source attribution.
    internal static void ForwardValues(ExecutableCodeObject code, List<ExecutableBasicBlock> blocks)
    {
        var initialized = code.IsFunctionScope && !code.RequiresLocalVariableMirroring
            ? new bool[code.LocalNames.Count] : [];
        for (var block = 0; block < blocks.Count; block++)
        {
            Array.Clear(initialized);
            var instructions = blocks[block].InstructionArray;
            var kept = 0;
            for (var index = 0; index < instructions.Length; index++)
            {
                var instruction = instructions[index];
                var count = instruction.OpCode switch
                {
                    ExecutableOpCode.Binary or ExecutableOpCode.Augmented => 2,
                    ExecutableOpCode.Unary or ExecutableOpCode.StoreLocal or ExecutableOpCode.Return => 1,
                    _ => 0
                };
                var first = instruction.InputStart;
                var second = instruction.SecondInput;
                var inputBase = kept - count;
                for (var input = count - 1; input >= 0; input--)
                {
                    var previous = inputBase + input;
                    if (previous < 0) break;
                    var leaf = instructions[previous];
                    if (leaf.OpCode is not (ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal)
                        || leaf.ResultSlot != instruction.ResultSlot + input) break;
                    int source;
                    if (leaf.OpCode == ExecutableOpCode.LoadConst) source = ~leaf.ConstantIndex;
                    else if (initialized.Length != 0 && initialized[leaf.LocalSlot]) source = leaf.LocalSlot;
                    else continue;
                    if (input == 0) first = source;
                    else second = source;
                    // At most one later leaf remains. Preserve that failable
                    // read in order while dropping the infallible input copy.
                    if (previous < kept - 1) instructions[previous] = instructions[kept - 1];
                    kept--;
                }
                instructions[kept++] = count == 0 ? instruction : instruction.WithValueInputs(first, second);
                if (initialized.Length != 0)
                {
                    if (instruction.OpCode == ExecutableOpCode.StoreLocal
                        && !code.CapturedLocalSlots.Contains(instruction.LocalSlot))
                        initialized[instruction.LocalSlot] = true;
                    // Prepared deletion and lowered statement execution may
                    // withdraw a binding. Keep their original local-read checks.
                    else if (instruction.OpCode is ExecutableOpCode.ApplyOperation or ExecutableOpCode.ExecuteFallbackStatement)
                        Array.Clear(initialized);
                }
            }
            if (kept != instructions.Length)
                blocks[block] = blocks[block] with { InstructionArray = instructions[..kept] };
        }
    }

    private static (int Pop, int Push, int Minimum) Effect(ExecutableInstruction instruction,
        IReadOnlyList<object?> constants, IReadOnlyList<ExecutableCallSite> calls)
    {
        var (pop, push) = instruction.OpCode switch
        {
            ExecutableOpCode.LoadConst or ExecutableOpCode.LoadLocal or ExecutableOpCode.LoadClosure or
                ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadName => (0, 1),
            ExecutableOpCode.StoreLocal or ExecutableOpCode.StoreClosure or ExecutableOpCode.StoreGlobal or
                ExecutableOpCode.StoreName or ExecutableOpCode.PopTop or ExecutableOpCode.JumpIfFalse or
                ExecutableOpCode.MatchCase or ExecutableOpCode.Return or ExecutableOpCode.ExitContextManager => (1, 0),
            ExecutableOpCode.Dup or ExecutableOpCode.LoadCallReceiver => (1, 2),
            ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.GetIter or
                ExecutableOpCode.ResolveContextManager or ExecutableOpCode.EnterContextManager or
                ExecutableOpCode.Unary or ExecutableOpCode.MatchException or
                ExecutableOpCode.Yield or ExecutableOpCode.YieldFrom => (1, 1),
            ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Subscript => (2, 1),
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
            ExecutableOpCode.ApplyOperation => OperationEffect((ExecutableOperation)constants[instruction.ConstantIndex]!),
            _ => throw new InvalidOperationException($"Unknown executable operand effect: {instruction.OpCode}.")
        };
        var minimum = instruction.OpCode == ExecutableOpCode.ForNext ? 1 : pop;
        if (instruction.OpCode == ExecutableOpCode.ApplyOperation)
        {
            minimum = constants[instruction.ConstantIndex] switch
            {
                ExecutableAppendDisplay item => checked(2 + item.IteratorDepth),
                ExecutableStoreDictionaryItem item => checked(pop + 1 + item.IteratorDepth),
                ExecutableAppendCall => 2,
                ExecutableReadRetainedSubscript => 2,
                ExecutableReadRetainedSlice => 4,
                ExecutableAppendFormattedText => 1,
                ExecutableFormatField { Append: true } => checked(pop + 1),
                _ => pop
            };
        }
        return (pop, push, minimum);
    }

    private static (int Pop, int Push) OperationEffect(ExecutableOperation operation) => operation switch
    {
        ExecutableStartDisplay or ExecutableStartClassHeader or ExecutablePostponedAnnotation or ExecutableAnnotationsEnabled or ExecutableStartFormattedString => (0, 1),
        ExecutableStartComprehension => (1, 3),
        ExecutableFinishComprehension => (2, 1),
        ExecutableFinishTuple or ExecutableStartCall or ExecutableFinishCall or ExecutableConvertFormattedValue or ExecutableCreateGeneratorExpression or ExecutableFinishFormattedString => (1, 1),
        ExecutableAppendDisplay or ExecutableAppendCall or ExecutableStoreModuleAnnotation => (1, 0),
        ExecutableStoreDictionaryItem item => (item.Unpacking ? 1 : 2, 0),
        ExecutableFormatField field => (field.DynamicSpecifier ? 2 : 1, field.Append ? 0 : 1),
        ExecutableAppendFormattedText => (0, 0),
        ExecutableCreateFunction function => (function.Inputs.Count, 0),
        ExecutableCreateLambda lambda => (lambda.Expression.Parameters.Count(p => p.DefaultValue is not null), 1),
        ExecutableCreateClass type => (checked(type.Definition.Decorators.Count + (type.ExpandedHeader ? 1 : 0)), 0),
        ExecutableReadRetainedSubscript or ExecutableReadRetainedSlice => (0, 1),
        ExecutableStoreMember => (2, 0),
        ExecutableStoreSubscript => (3, 0),
        ExecutableStoreSlice => (5, 0),
        ExecutableUnpackValues unpack => (1, unpack.Layout.TargetCount),
        ExecutableFailAssertion assertion => (assertion.HasMessage ? 1 : 0, 0),
        ExecutableRaise raised => (raised.HasCause ? 2 : 1, 0),
        ExecutableDeletePreparedTarget deleted => (deleted.Target switch
        {
            NameAssignmentTargetSyntax => 0,
            MemberAssignmentTargetSyntax => 1,
            SubscriptAssignmentTargetSyntax => 2,
            SliceAssignmentTargetSyntax slice => 1 + (slice.Start is null ? 0 : 1) + (slice.End is null ? 0 : 1) + (slice.Step is null ? 0 : 1),
            _ => throw new InvalidOperationException("Unknown executable delete shape.")
        }, 0),
        _ => throw new InvalidOperationException($"Unknown prepared operand effect: {operation.GetType().Name}.")
    };
}
