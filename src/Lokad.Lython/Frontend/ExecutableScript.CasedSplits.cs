using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// The same existing kernel serves an immediate ordinary chain and a closed
// native body. Original instructions remain the pre-effect dynamic path.
internal abstract record ExecutableSplitPrefix;

internal sealed record ExecutableCasedSplit(
    bool Upper,
    LythonSourceSpan CasingMemberSpan, LythonSourceSpan CasingCallSpan,
    LythonSourceSpan SplitMemberSpan, LythonSourceSpan SplitCallSpan) : ExecutableSplitPrefix;

internal sealed record ExecutableTrimmedSplit(
    PyString Separator, LythonSourceSpan MemberSpan, LythonSourceSpan SeparatorSpan) : ExecutableSplitPrefix;

internal sealed partial class ExecutableCodeObject
{
    private int PrepareDirectCasedReturn(CancellationToken cancellationToken)
    {
        var split = Instructions.Length - 5;
        if (split < 0 || EntryIndex != 0 || ExceptionRegions.Count != 0 || !UsesOnlyLexicalSlots ||
            CapturedLocalSlots.Count != 0 || Instructions[split] is not { OpCode: ExecutableOpCode.LoadCasedSplit, InputStart: 0 } ||
            Instructions[^2] is not { OpCode: ExecutableOpCode.Call } call ||
            !CallSites[call.CallSiteIndex].ReturnedDirectly ||
            Instructions[^1] is not { OpCode: ExecutableOpCode.Return } returned ||
            returned.InputStart != call.ResultSlot) return -1;
        if (split == 0) return split;

        // An optional prefix writes a closed integer chain seeded by len(text)
        // into an existing cell. The live length callable is checked at entry.
        var storeIndex = split - 1;
        if (storeIndex < 2 || Instructions[storeIndex] is not { OpCode: ExecutableOpCode.StoreClosure } store ||
            Instructions[0] is not { OpCode: ExecutableOpCode.Call, HasForwardedCallOperands: true, InputStart: < 0, SecondInput: 0 } length ||
            CallSites[length.CallSiteIndex] is not { ArgumentCount: 1, HasReceiver: false, IsPositionalOnly: true }) return -1;
        var name = ~length.InputStart - Constants.Length;
        if ((uint)name >= (uint)Names.Length || Names[name] != "len") return -1;

        var prior = length.ResultSlot;
        for (var index = 1; index < storeIndex; index++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, index);
            ref readonly var binary = ref Instructions[index];
            if (binary.OpCode != ExecutableOpCode.Binary ||
                binary.BinaryOperator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Subtract or
                    BinaryOperatorSyntax.Multiply or BinaryOperatorSyntax.FloorDivide or BinaryOperatorSyntax.Modulo) ||
                !ReadsExactValue(binary.InputStart, prior) || !ReadsExactValue(binary.SecondInput, prior) ||
                binary.ResultSlot < FirstTemporarySlot || binary.HasResultConsumer != (index == storeIndex - 1)) return -1;
            prior = binary.ResultSlot;
        }
        return store.InputStart == prior ? split : -1;

        bool ReadsExactValue(int source, int prior) => source >= 0 ? source == prior :
            (uint)~source < (uint)Constants.Length && Constants[~source] is BigInteger;
    }

    internal ExecutableInstruction PrepareCallSite(List<ExecutableInstruction> instructions, int index,
        ExecutableInstruction instruction, ExecutableCallSite[] sites)
    {
        if (index + 1 >= instructions.Count) return instruction;
        var next = instructions[index + 1];
        if (instruction.OpCode == ExecutableOpCode.Call)
        {
            var site = instruction.CallSiteIndex;
            var call = sites[site];
            switch (next.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    sites[site] = call with { ConsumedByIteration = true };
                    break;
                case ExecutableOpCode.Return:
                    sites[site] = call with { ReturnedDirectly = true };
                    break;
                case ExecutableOpCode.UnpackValues when !next.UnpackLayout.HasStarredTarget
                    && next.UnpackLayout.TargetCount <= LythonRuntime.NativeValueCapacity:
                    sites[site] = call with { UnpackedReturnCount = next.UnpackLayout.TargetCount };
                    break;
            }
            return instruction;
        }

        var name = Names[instruction.NameIndex];
        if (name is "lower" or "upper" && index + 3 < instructions.Count
            && next.OpCode == ExecutableOpCode.Call && instructions[index + 3].OpCode == ExecutableOpCode.Call
            && PrepareCasedSplit(instructions, index + 1, name == "upper", sites))
            return instruction.AsCasedSplit();
        if (name == "rstrip" && index + 5 < instructions.Count && instructions[index + 2].OpCode == ExecutableOpCode.Call
            && instructions[index + 4].OpCode == ExecutableOpCode.LoadConst
            && instructions[index + 5].OpCode == ExecutableOpCode.Call)
            PrepareTrimmedSplit(instructions, index + 2, sites);
        return instruction;
    }

    private bool PrepareCasedSplit(List<ExecutableInstruction> instructions,
        int producer, bool upper, ExecutableCallSite[] sites)
    {
        var consumer = producer + 2;
        if (instructions[consumer - 1].OpCode != ExecutableOpCode.LoadCallMember) return false;
        var firstIndex = instructions[producer].CallSiteIndex;
        var lastIndex = instructions[consumer].CallSiteIndex;
        var first = sites[firstIndex];
        var last = sites[lastIndex];
        if (!first.HasReceiver || !last.HasReceiver || first.ArgumentCount != 0 || last.ArgumentCount != 0
            || !first.IsPositionalOnly || !last.IsPositionalOnly
            || Names[instructions[consumer - 1].NameIndex] is not ("split" or "rsplit")) return false;
        sites[firstIndex] = first with { SplitPrefix = new ExecutableCasedSplit(upper,
            instructions[producer - 1].Span, first.CallSpan, instructions[consumer - 1].Span, last.CallSpan) };
        return true;
    }

    private void PrepareTrimmedSplit(List<ExecutableInstruction> instructions,
        int producer, ExecutableCallSite[] sites)
    {
        var splitMember = instructions[producer + 1];
        if (splitMember.OpCode != ExecutableOpCode.LoadCallMember || Names[splitMember.NameIndex] != "split") return;
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
