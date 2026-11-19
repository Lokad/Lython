using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// The same existing kernel serves an immediate ordinary chain and a closed
// native body. Original instructions remain the pre-effect dynamic path.
internal abstract record ExecutableSplitPrefix;

internal sealed record ExecutableCasedSplit(
    LythonRuntime.ReceiverMethod CasingMethod, bool Upper,
    LythonSourceSpan CasingMemberSpan, LythonSourceSpan CasingCallSpan,
    LythonSourceSpan SplitMemberSpan, LythonSourceSpan SplitCallSpan) : ExecutableSplitPrefix;

internal sealed record ExecutableTrimmedSplit(
    PyString Separator, LythonSourceSpan MemberSpan, LythonSourceSpan SeparatorSpan) : ExecutableSplitPrefix;

internal sealed partial class ExecutableCodeObject
{
    private void PrepareCallResults(IReadOnlyList<List<ExecutableInstruction>> blocks, ExecutableCallSite[] sites)
    {
        foreach (var block in blocks)
        {
            var instructions = block;
            // Before operand forwarding, these adjacent uses cannot expose a
            // result in a guest binding. A direct return inherits its caller's
            // consumption; a local store always keeps an ordinary Python value.
            for (var index = 0; index + 1 < instructions.Count; index++)
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
            for (var index = 1; index + 2 < instructions.Count; index++)
            {
                if (instructions[index].OpCode != ExecutableOpCode.Call) continue;
                if (instructions[index + 2].OpCode == ExecutableOpCode.Call)
                    PrepareCasedSplit(instructions, index, index + 2, sites);
                else if (index >= 2 && index + 3 < instructions.Count &&
                    instructions[index + 2].OpCode == ExecutableOpCode.LoadConst &&
                    instructions[index + 3].OpCode == ExecutableOpCode.Call)
                    PrepareTrimmedSplit(instructions, index, sites);
            }
        }
    }

    private ExecutableCasedSplit? PrepareCasedSplit(IReadOnlyList<ExecutableInstruction> instructions,
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
        var recipe = last.SplitPrefix as ExecutableCasedSplit ?? new ExecutableCasedSplit(LythonRuntime.StringMembers.GetMethod(name)!,
            name == "upper", instructions[producer - 1].Span, first.CallSpan,
            instructions[consumer - 1].Span, last.CallSpan);
        sites[lastIndex] = last with { SplitPrefix = recipe };
        // A local between the calls is observable. Never skip its instructions.
        if (consumer == producer + 2) sites[firstIndex] = first with
        { SplitPrefix = recipe };
        return recipe;
    }

    private void PrepareTrimmedSplit(IReadOnlyList<ExecutableInstruction> instructions,
        int producer, ExecutableCallSite[] sites)
    {
        var trimMember = instructions[producer - 2];
        var splitMember = instructions[producer + 1];
        if (trimMember.OpCode != ExecutableOpCode.LoadCallMember || Names[trimMember.NameIndex] != "rstrip" ||
            splitMember.OpCode != ExecutableOpCode.LoadCallMember || Names[splitMember.NameIndex] != "split") return;
        var trimIndex = instructions[producer].CallSiteIndex;
        var trim = sites[trimIndex];
        var split = sites[instructions[producer + 3].CallSiteIndex];
        var delimiter = instructions[producer + 2];
        if (!trim.HasReceiver || !split.HasReceiver || trim.ArgumentCount != 1 || split.ArgumentCount != 1 ||
            !trim.IsPositionalOnly || !split.IsPositionalOnly ||
            Constants[delimiter.ConstantIndex] is not PyString { ByteLength: > 0 } separator) return;

        sites[trimIndex] = trim with { SplitPrefix = new ExecutableTrimmedSplit(separator, splitMember.Span, delimiter.Span) };
    }
}
