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
    private void PrepareCallResults(IReadOnlyList<ExecutableBasicBlock> blocks, ExecutableCallSite[] sites)
    {
        foreach (var block in blocks)
        {
            var instructions = block.InstructionArray;
            // Before operand forwarding, these adjacent uses cannot expose a
            // result in a guest binding. A direct return inherits its caller's
            // consumption; a local store always keeps an ordinary Python value.
            for (var index = 0; index + 1 < instructions.Length; index++)
                if (instructions[index].OpCode == ExecutableOpCode.Call)
                {
                    var site = instructions[index].CallSiteIndex;
                    var next = instructions[index + 1];
                    sites[site] = sites[site] with
                    {
                        ConsumedByIteration = next.OpCode == ExecutableOpCode.GetIter,
                        ReturnedDirectly = next.OpCode == ExecutableOpCode.Return,
                        UnpackedReturnCount = next.OpCode == ExecutableOpCode.UnpackValues
                            && !next.UnpackLayout.HasStarredTarget
                            && next.UnpackLayout.TargetCount <= LythonRuntime.NativeValueCapacity
                            ? next.UnpackLayout.TargetCount : 0,
                    };
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
        // A local between the calls is observable. Never skip its instructions.
        if (consumer == producer + 2) sites[firstIndex] = first with
        { CasedSplit = recipe };
        return recipe;
    }
}
