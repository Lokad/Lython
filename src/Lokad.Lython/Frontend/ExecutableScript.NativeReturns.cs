using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableNativeBody? PrepareNativeBody(IReadOnlyList<ExecutableBasicBlock> blocks,
        int entryBlock, int exceptionRegionCount, ExecutableCallSite[] sites)
    {
        if (!IsFunctionScope || RequiresLocalVariableMirroring || CapturedLocalSlots.Count != 0
            || exceptionRegionCount != 0)
            return null;
        if (blocks.Count == 1 && blocks[0].InstructionArray is [{ OpCode: ExecutableOpCode.ReturnNone }])
            return ExecutableNativeBody.ReturnNone;

        // Offsets identify visited source operations only during preparation.
        // Proof nodes own immutable metadata; ordinary bytecode may forward loads.
        var offsets = new int[blocks.Count + 1];
        for (var block = 0; block < blocks.Count; block++)
        {
            var instructions = blocks[block].InstructionArray;
            if (instructions.Length == 0 || instructions[^1].OpCode is not
                (ExecutableOpCode.Jump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone))
                return null;
            offsets[block + 1] = checked(offsets[block] + instructions.Length);
        }
        var visited = new bool[offsets[^1]];
        var values = new List<NativeReturnValue>();
        var hasConditional = false;
        var readsFormatStorage = false;
        var hasIncomingCells = false;
        var proof = Compile(entryBlock, 0, false, null, 0);
        if (proof is null || hasConditional && readsFormatStorage) return null;
        return new(values.ToArray(), proof.Value.Root - FirstTemporarySlot, hasIncomingCells ? ClosureNames.Count : 0);

        (int Root, int Depth)? Compile(int block, int firstInstruction, bool hasEffects,
            HashSet<int>? forwardedLocals, int controlDepth)
        {
            // Compile/evaluate within the existing native recursion admission.
            // Other control flow keeps ordinary executable code, before effects.
            if (controlDepth > 16) return null;
            var stack = new List<(int Value, LythonSourceSpan? Member, int Depth, int MemberName, int Producer, LythonSourceSpan? ReadSpan)>();
            var instructions = blocks[block].InstructionArray;
            var negateCondition = false;
            for (var index = firstInstruction; index < instructions.Length; index++)
            {
                var sourceIndex = offsets[block] + index;
                if (visited[sourceIndex]) return null; // cycles or shared joins
                visited[sourceIndex] = true;
                var instruction = instructions[index];
                var operation = NativeReturnOperation.Read;
                LythonRuntime.ReceiverMethod? nativeMethod = null;
                var first = -1;
                var second = -1;
                var depth = 1;
                LythonSourceSpan? memberSpan = null;
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Jump:
                        block = instruction.TargetIndex;
                        instructions = blocks[block].InstructionArray;
                        index = -1;
                        continue;
                    case ExecutableOpCode.Unary when instruction.UnaryOperator == ExecutableUnaryOperator.Not:
                        if (stack.Count != 1 || stack[0].Member is not null) return null;
                        var end = index;
                        do { negateCondition = !negateCondition; end++; }
                        while (end < instructions.Length && instructions[end].OpCode == ExecutableOpCode.Unary
                            && instructions[end].UnaryOperator == ExecutableUnaryOperator.Not);
                        if (end >= instructions.Length || instructions[end].OpCode != ExecutableOpCode.JumpIfFalse)
                            return null;
                        for (var skipped = index + 1; skipped < end; skipped++)
                        {
                            if (visited[offsets[block] + skipped]) return null;
                            visited[offsets[block] + skipped] = true;
                        }
                        index = end - 1;
                        continue;
                    case ExecutableOpCode.JumpIfFalse:
                        if (stack.Count != 1 || stack[0].Member is not null) return null;
                        hasConditional = true;
                        var condition = stack[0];
                        var consequent = Compile(block, index + 1, hasEffects,
                            forwardedLocals is null ? null : new(forwardedLocals), controlDepth + 1);
                        var alternative = Compile(instruction.TargetIndex, 0, hasEffects,
                            forwardedLocals, controlDepth + 1);
                        if (consequent is null || alternative is null) return null;
                        var conditionalDepth = 1 + Math.Max(condition.Depth,
                            Math.Max(consequent.Value.Depth, alternative.Value.Depth));
                        if (conditionalDepth > 16) return null;
                        var whenTrue = negateCondition ? alternative.Value : consequent.Value;
                        var whenFalse = negateCondition ? consequent.Value : alternative.Value;
                        values.Add(new(NativeReturnOperation.Conditional, instruction, condition.Value,
                            whenTrue.Root, Third: whenFalse.Root));
                        return (FirstTemporarySlot + values.Count - 1, conditionalDepth);
                    case ExecutableOpCode.Return:
                        if (stack.Count != 1 || stack[0].Member is not null) return null;
                        var root = stack[0];
                        if (root.Value < FirstTemporarySlot)
                        {
                            // A real return keeps its source/exit metadata. Consumed
                            // pure leaves need no node in the native operation tree.
                            var read = root.Value < 0
                                ? ExecutableInstruction.LoadConst(~root.Value, root.ReadSpan!)
                                : ExecutableInstruction.LoadLocal(root.Value, root.ReadSpan!);
                            values.Add(new(NativeReturnOperation.Read, read, -1, -1,
                                ReturnSpan: instruction.Span));
                            return (FirstTemporarySlot + values.Count - 1, root.Depth);
                        }
                        var rootIndex = root.Value - FirstTemporarySlot;
                        values[rootIndex] = values[rootIndex] with { ReturnSpan = instruction.Span };
                        return (root.Value, root.Depth);
                    case ExecutableOpCode.ReturnNone:
                        if (stack.Count != 0) return null;
                        values.Add(new(NativeReturnOperation.None, instruction, -1, -1,
                            ReturnSpan: instruction.Span));
                        return (FirstTemporarySlot + values.Count - 1, 1);
                    case ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple when instruction.ItemCount == 0:
                        operation = NativeReturnOperation.EmptySequence;
                        break;
                    case ExecutableOpCode.LoadLocal:
                        // A later read needs the actual binding, not its old input.
                        if (forwardedLocals?.Contains(instruction.LocalSlot) == true) return null;
                        goto case ExecutableOpCode.LoadConst;
                    case ExecutableOpCode.LoadConst:
                        var source = instruction.OpCode == ExecutableOpCode.LoadConst
                            ? ~instruction.ConstantIndex : instruction.LocalSlot;
                        stack.Add((source, null, 1, -1, sourceIndex, instruction.Span));
                        continue;
                    case ExecutableOpCode.LoadName:
                        // Unresolved lexical lookup needs an invocation context.
                        // Only explicit globals have the native module semantics.
                        return null;
                    case ExecutableOpCode.LoadClosure:
                        if (hasEffects) return null;
                        hasIncomingCells = true;
                        break;
                    case ExecutableOpCode.LoadGlobal:
                        // Earlier native work can invoke host admission. A later
                        // mutable lookup cannot rely on the entry type proof.
                        if (hasEffects) return null;
                        break;
                    case ExecutableOpCode.StoreClosure:
                        // The actual incoming cell remains the write target.
                        // Later mutable reads cannot be proved before this store
                        // or any admission performed while producing its value.
                        if (stack.Count != 1 || stack[0].Member is not null) return null;
                        hasIncomingCells = true;
                        var stored = stack[0];
                        var continuation = Compile(block, index + 1, true, forwardedLocals, controlDepth + 1);
                        if (continuation is null) return null;
                        var sequenceDepth = 1 + Math.Max(stored.Depth, continuation.Value.Depth);
                        if (sequenceDepth > 16) return null;
                        values.Add(new(NativeReturnOperation.StoreClosure, instruction, stored.Value, continuation.Value.Root));
                        return (FirstTemporarySlot + values.Count - 1, sequenceDepth);
                    case ExecutableOpCode.StoreLocal:
                        // Forward the operand only inside this closed native proof.
                        // Ordinary bytecode retains both instructions and bindings
                        // when protocols, locals inspection or other guards fail.
                        if (stack.Count == 0 || stack[^1].Member is not null || index + 1 >= instructions.Length
                            || instructions[index + 1].OpCode != ExecutableOpCode.LoadLocal
                            || instructions[index + 1].LocalSlot != instruction.LocalSlot)
                            return null;
                        forwardedLocals ??= new();
                        if (!forwardedLocals.Add(instruction.LocalSlot)) return null;
                        index++;
                        if (visited[offsets[block] + index]) return null;
                        visited[offsets[block] + index] = true;
                        continue;
                    case ExecutableOpCode.LoadCallMember:
                        if (stack.Count == 0 || stack[^1].Member is not null) return null;
                        // Fold receiver+method into the call; no exposed bound value.
                        stack[^1] = (stack[^1].Value, instruction.Span, stack[^1].Depth, instruction.NameIndex, stack[^1].Producer, stack[^1].ReadSpan);
                        continue;
                    case ExecutableOpCode.Binary when LythonRuntime.TryGetNativeBinaryShape(instruction.BinaryOperator,
                        LythonRuntime.NativeReturnShape.Integer, LythonRuntime.NativeReturnShape.Integer, out _):
                    case ExecutableOpCode.Augmented when LythonRuntime.TryGetNativeBinaryShape(
                        LythonRuntime.AugmentedBinaryOperator(instruction.AugmentedOperator),
                        LythonRuntime.NativeReturnShape.Integer, LythonRuntime.NativeReturnShape.Integer, out _):
                    case ExecutableOpCode.Subscript:
                        if (instruction.OpCode == ExecutableOpCode.Subscript && hasEffects) return null;
                        if (stack.Count < 2 || stack[^1].Member is not null || stack[^2].Member is not null) return null;
                        operation = instruction.OpCode switch
                        {
                            ExecutableOpCode.Binary => NativeReturnOperation.Binary,
                            ExecutableOpCode.Augmented => NativeReturnOperation.Augmented,
                            _ => NativeReturnOperation.Subscript,
                        };
                        first = stack[^2].Value;
                        second = stack[^1].Value;
                        depth = 1 + Math.Max(stack[^2].Depth, stack[^1].Depth);
                        stack.RemoveRange(stack.Count - 2, 2);
                        break;
                    case ExecutableOpCode.ConvertFormattedValue:
                    case ExecutableOpCode.FormatField:
                        operation = instruction.OpCode == ExecutableOpCode.ConvertFormattedValue
                            ? NativeReturnOperation.ConvertFormat
                            : Constants[instruction.ConstantIndex] is ExecutableFormatField { DynamicSpecifier: false, Append: false }
                                ? NativeReturnOperation.FormatValue : NativeReturnOperation.Read;
                        if (operation == NativeReturnOperation.Read || stack.Count == 0 || stack[^1].Member is not null) return null;
                        first = stack[^1].Value;
                        depth = 1 + stack[^1].Depth;
                        // Actual dictionary-value queries are not speculated on
                        // an unselected branch. Straight-line proofs retain them.
                        var formattedInput = first;
                        while (formattedInput >= FirstTemporarySlot &&
                            values[formattedInput - FirstTemporarySlot].Operation is NativeReturnOperation.ConvertFormat or NativeReturnOperation.FormatValue)
                            formattedInput = values[formattedInput - FirstTemporarySlot].First;
                        if (formattedInput >= FirstTemporarySlot &&
                            values[formattedInput - FirstTemporarySlot].Operation == NativeReturnOperation.Subscript) readsFormatStorage = true;
                        stack.RemoveAt(stack.Count - 1);
                        break;
                    case ExecutableOpCode.Call:
                        var site = CallSites[instruction.CallSiteIndex];
                        if (!site.IsPositionalOnly || site.ArgumentCount > 3 || stack.Count < site.ArgumentCount + 1)
                            return null;
                        var start = stack.Count - site.ArgumentCount - 1;
                        var target = stack[start];
                        if (site.HasReceiver && target.Member is null) return null;
                        for (var a = start + 1; a < stack.Count; a++)
                        {
                            if (stack[a].Member is not null) return null;
                            depth = Math.Max(depth, stack[a].Depth);
                        }
                        depth = 1 + Math.Max(depth, target.Depth);
                        first = target.Value;
                        memberSpan = target.Member;
                        if (target.Member is null)
                        {
                            // These shapes never passed the dynamic builtin proof.
                            if (site.ArgumentCount != 1 || first >= FirstTemporarySlot && values[first - FirstTemporarySlot].Operation != NativeReturnOperation.Read) return null;
                            operation = NativeReturnOperation.BuiltinUnary;
                        }
                        else
                        {
                            var member = Names[target.MemberName];
                            if (site.ArgumentCount == 0 && LythonRuntime.StringMembers.GetMethod(member) is { } method
                                && method.AcceptsPositional(0)
                                && StaticContracts.TryGetMemberReturnShape(AbstractValueKind.String, member, out var shape)
                                && shape is StaticReturnShape.String or StaticReturnShape.Bytes or StaticReturnShape.Boolean or StaticReturnShape.ListOfString)
                            {
                                nativeMethod = method;
                                operation = shape switch
                                {
                                    StaticReturnShape.String => NativeReturnOperation.StringMethod,
                                    StaticReturnShape.Bytes => NativeReturnOperation.BytesMethod,
                                    StaticReturnShape.Boolean => NativeReturnOperation.BooleanMethod,
                                    _ => NativeReturnOperation.ListMethod,
                                };
                                if (member is "split" or "rsplit"
                                    && first >= FirstTemporarySlot && first - FirstTemporarySlot == values.Count - 1 && index > 0
                                    && ReferenceEquals(target.Member, instructions[index - 1].Span)
                                    && values[first - FirstTemporarySlot].Operation == NativeReturnOperation.StringMethod)
                                {
                                    var producer = values[first - FirstTemporarySlot];
                                    var producerIndex = target.Producer - offsets[block];
                                    if (producerIndex > 0 && producerIndex < instructions.Length
                                        && ReferenceEquals(producer.MemberSpan, instructions[producerIndex - 1].Span)
                                        && PrepareCasedSplit(instructions, producerIndex, index, sites) is not null)
                                    {
                                        operation = NativeReturnOperation.CasedSplit;
                                        first = producer.First;
                                        second = -1;
                                        nativeMethod = null;
                                        values.RemoveAt(values.Count - 1);
                                    }
                                }
                            }
                            else
                            {
                                var selected = LythonRuntime.SelectNativeStringOperation(
                                    LythonRuntime.StringMembers.GetMethod(member), site.ArgumentCount);
                                if (selected is null) return null;
                                operation = selected.Value;
                            }
                        }
                        for (var a = 0; a < site.ArgumentCount; a++)
                            site.Arguments[a] = site.Arguments[a] with
                            {
                                NativeInput = stack[start + 1 + a].Value
                            };
                        stack.RemoveRange(start, site.ArgumentCount + 1);
                        break;
                    default:
                        return null;
                }
                // Eligibility bound, never a guest syntax/nesting limit.
                if (depth > 16) return null;
                if (operation is not (NativeReturnOperation.Read or NativeReturnOperation.Subscript)) hasEffects = true;
                values.Add(new(operation, instruction, first, second, nativeMethod, MemberSpan: memberSpan));
                stack.Add((FirstTemporarySlot + values.Count - 1, null, depth, -1, sourceIndex, null));
            }
            return null;
        }
    }
}
