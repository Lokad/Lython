using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

// The same existing kernel serves an immediate ordinary chain and a closed
// native body. Original instructions remain the pre-effect dynamic path.
internal sealed record ExecutableCasedSplit(
    LythonRuntime.ReceiverMethod CasingMethod, bool Upper,
    LythonSourceSpan CasingMemberSpan, LythonSourceSpan CasingCallSpan,
    LythonSourceSpan SplitMemberSpan, LythonSourceSpan SplitCallSpan);

internal sealed partial class ExecutableCodeObject
{
    private void PrepareCasedSplits(IReadOnlyList<ExecutableBasicBlock> blocks, ExecutableCallSite[] sites)
    {
        foreach (var block in blocks)
        {
            var instructions = block.InstructionArray;
            // The result never enters a guest binding: the very next operation
            // acquires it for this frame's private iteration storage.
            for (var index = 0; index + 1 < instructions.Length; index++)
                if (instructions[index].OpCode == ExecutableOpCode.Call
                    && instructions[index + 1].OpCode == ExecutableOpCode.GetIter)
                {
                    var site = instructions[index].CallSiteIndex;
                    sites[site] = sites[site] with { ConsumedByIteration = true };
                }
            for (var index = 1; index + 2 < instructions.Length; index++)
                if (instructions[index].OpCode == ExecutableOpCode.Call
                    && instructions[index + 2].OpCode == ExecutableOpCode.Call)
                    PrepareCasedSplit(instructions, index, index + 2, sites);
        }
    }

    private ExecutableCasedSplit? PrepareCasedSplit(ExecutableInstruction[] instructions,
        int producer, int consumer, ExecutableCallSite[] sites)
    {
        if (instructions[producer - 1].OpCode != ExecutableOpCode.LoadCallMember
            || instructions[consumer - 1].OpCode != ExecutableOpCode.LoadCallMember) return null;
        var firstIndex = instructions[producer].CallSiteIndex;
        var lastIndex = instructions[consumer].CallSiteIndex;
        var first = sites[firstIndex];
        var last = sites[lastIndex];
        if (!first.HasReceiver || !last.HasReceiver || first.ArgumentCount != 0 || last.ArgumentCount != 0
            || !first.IsPositionalOnly || !last.IsPositionalOnly
            || Names[instructions[consumer - 1].NameIndex] is not ("split" or "rsplit")) return null;
        var name = Names[instructions[producer - 1].NameIndex];
        if (name is not ("lower" or "upper")) return null;
        var recipe = last.CasedSplit ?? new ExecutableCasedSplit(LythonRuntime.StringMembers.GetMethod(name)!,
            name == "upper", instructions[producer - 1].Span, first.CallSpan,
            instructions[consumer - 1].Span, last.CallSpan);
        sites[lastIndex] = last with { CasedSplit = recipe };
        // A local between the calls is observable on the ordinary path. Only
        // native-body proof may elide that binding; never skip its instructions.
        if (consumer == producer + 2) sites[firstIndex] = first with
        { CasedSplit = recipe, ConsumedByIteration = last.ConsumedByIteration };
        return recipe;
    }
}
