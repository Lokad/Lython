namespace Lokad.Lython.Frontend;

// Links into the existing executable instructions, not a second copy of code.
// First is the receiver/target/left operand; the remaining links are arguments.
internal readonly record struct NativeReturnNode(
    int Instruction, int First = -1, int Second = -1, int Third = -1, int Fourth = -1,
    int MemberInstruction = -1);

internal sealed partial class ExecutableCodeObject
{
    private NativeReturnNode[]? PrepareNativeReturn()
    {
        if (!IsFunctionScope || RequiresLocalVariableMirroring || CapturedLocalSlots.Count != 0
            || ClosureNames.Count != 0 || ExceptionRegions.Count != 0 || Blocks.Count != 1)
            return null;
        var instructions = Blocks[EntryBlockIndex].InstructionArray;
        if (instructions.Length == 1 && instructions[0].OpCode == ExecutableOpCode.ReturnNone)
            return [];
        if (instructions.Length < 2 || instructions[^1].OpCode != ExecutableOpCode.Return)
            return null;

        var nodes = new List<NativeReturnNode>();
        var stack = new List<(int Node, int Member, int Depth)>();
        HashSet<int>? forwardedLocals = null;
        for (var index = 0; index < instructions.Length - 1; index++)
        {
            var instruction = instructions[index];
            NativeReturnNode node;
            var depth = 1;
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadLocal:
                    // A later read needs the actual binding, not its old input.
                    if (forwardedLocals?.Contains(instruction.LocalSlot) == true) return null;
                    goto case ExecutableOpCode.LoadConst;
                case ExecutableOpCode.LoadConst:
                case ExecutableOpCode.LoadName:
                case ExecutableOpCode.LoadGlobal:
                    node = new(index);
                    break;
                case ExecutableOpCode.StoreLocal:
                    // Forward the operand only inside this closed native proof.
                    // Ordinary bytecode retains both instructions and bindings
                    // when protocols, locals inspection or other guards fail.
                    if (stack.Count == 0 || stack[^1].Member >= 0 || index + 1 >= instructions.Length - 1
                        || instructions[index + 1].OpCode != ExecutableOpCode.LoadLocal
                        || instructions[index + 1].LocalSlot != instruction.LocalSlot)
                        return null;
                    forwardedLocals ??= new();
                    if (!forwardedLocals.Add(instruction.LocalSlot)) return null;
                    index++;
                    continue;
                case ExecutableOpCode.LoadCallReceiver:
                case ExecutableOpCode.LoadCallMember:
                    if (stack.Count == 0 || stack[^1].Member >= 0) return null;
                    // Fold receiver+method into the call; no exposed bound value.
                    stack[^1] = (stack[^1].Node, index, stack[^1].Depth);
                    continue;
                case ExecutableOpCode.Binary when instruction.BinaryOperator == BinaryOperatorSyntax.Add:
                case ExecutableOpCode.Subscript:
                    if (stack.Count < 2 || stack[^1].Member >= 0 || stack[^2].Member >= 0) return null;
                    node = new(index, stack[^2].Node, stack[^1].Node);
                    depth = 1 + Math.Max(stack[^2].Depth, stack[^1].Depth);
                    stack.RemoveRange(stack.Count - 2, 2);
                    break;
                case ExecutableOpCode.Call:
                    var site = CallSites[instruction.CallSiteIndex];
                    if (!site.IsPositionalOnly || site.ArgumentCount > 3 || stack.Count < site.ArgumentCount + 1)
                        return null;
                    var start = stack.Count - site.ArgumentCount - 1;
                    var target = stack[start];
                    if (site.HasReceiver && target.Member < 0) return null;
                    for (var a = start + 1; a < stack.Count; a++)
                    {
                        if (stack[a].Member >= 0) return null;
                        depth = Math.Max(depth, stack[a].Depth);
                    }
                    depth = 1 + Math.Max(depth, target.Depth);
                    node = new(index, target.Node,
                        site.ArgumentCount > 0 ? stack[start + 1].Node : -1,
                        site.ArgumentCount > 1 ? stack[start + 2].Node : -1,
                        site.ArgumentCount > 2 ? stack[start + 3].Node : -1, target.Member);
                    stack.RemoveRange(start, site.ArgumentCount + 1);
                    break;
                default:
                    return null;
            }
            // Eligibility bound for CLR-recursive evaluation, never a guest limit.
            // Deeper expressions still execute through the ordinary engine.
            if (depth > 16) return null;
            stack.Add((nodes.Count, -1, depth));
            nodes.Add(node);
        }
        return stack.Count == 1 && stack[0].Member < 0 ? nodes.ToArray() : null;
    }
}
