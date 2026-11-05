using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private ExecutableNativeBody? PrepareNativeBody(IReadOnlyList<ExecutableBasicBlock> blocks, int exceptionRegionCount)
    {
        if (!IsFunctionScope || RequiresLocalVariableMirroring || CapturedLocalSlots.Count != 0
            || ClosureNames.Count != 0 || exceptionRegionCount != 0 || blocks.Count != 1)
            return null;
        // A second reachable block requires control flow, exception routing or
        // implicit completion; none belongs to this straight-line proof.
        var instructions = blocks[0].InstructionArray;
        if (instructions.Length == 1 && instructions[0].OpCode == ExecutableOpCode.ReturnNone)
            return ExecutableNativeBody.ReturnNone;
        if (instructions.Length < 2 || instructions[^1].OpCode != ExecutableOpCode.Return)
            return null;

        var values = new List<NativeReturnValue>();
        var stack = new List<(int Value, int Member, int Depth)>();
        HashSet<int>? forwardedLocals = null;
        var hasFormattedBuilder = false;
        for (var index = 0; index < instructions.Length - 1; index++)
        {
            var instruction = instructions[index];
            var operation = NativeReturnOperation.Read;
            var first = -1;
            var second = -1;
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
                    stack[^1] = (stack[^1].Value, index, stack[^1].Depth);
                    continue;
                case ExecutableOpCode.Binary when instruction.BinaryOperator == BinaryOperatorSyntax.Add:
                case ExecutableOpCode.Subscript:
                    if (stack.Count < 2 || stack[^1].Member >= 0 || stack[^2].Member >= 0) return null;
                    operation = instruction.OpCode == ExecutableOpCode.Binary ? NativeReturnOperation.Add : NativeReturnOperation.Subscript;
                    first = stack[^2].Value;
                    second = stack[^1].Value;
                    depth = 1 + Math.Max(stack[^2].Depth, stack[^1].Depth);
                    stack.RemoveRange(stack.Count - 2, 2);
                    break;
                case ExecutableOpCode.ApplyOperation:
                    operation = Constants[instruction.ConstantIndex] switch
                    {
                        ExecutableStartFormattedString => NativeReturnOperation.StartFormat,
                        ExecutableAppendFormattedText => NativeReturnOperation.AppendFormatText,
                        ExecutableFinishFormattedString => NativeReturnOperation.FinishFormat,
                        ExecutableConvertFormattedValue => NativeReturnOperation.ConvertFormat,
                        ExecutableFormatField { DynamicSpecifier: false, Append: false } => NativeReturnOperation.FormatValue,
                        ExecutableFormatField { DynamicSpecifier: false, Append: true } => NativeReturnOperation.AppendFormatField,
                        _ => NativeReturnOperation.Read
                    };
                    switch (Constants[instruction.ConstantIndex])
                    {
                        case ExecutableStartFormattedString:
                            // One root builder has one deterministic native lifetime.
                            // Nested multi-part strings keep their ordinary frames.
                            if (hasFormattedBuilder || index != 0) return null;
                            hasFormattedBuilder = true;
                            break;
                        case ExecutableAppendFormattedText:
                        case ExecutableFinishFormattedString:
                        case ExecutableConvertFormattedValue:
                        case ExecutableFormatField { DynamicSpecifier: false, Append: false }:
                            if (stack.Count == 0 || stack[^1].Member >= 0) return null;
                            first = stack[^1].Value;
                            depth = 1 + stack[^1].Depth;
                            stack.RemoveAt(stack.Count - 1);
                            break;
                        case ExecutableFormatField { DynamicSpecifier: false, Append: true }:
                            if (stack.Count < 2 || stack[^1].Member >= 0 || stack[^2].Member >= 0) return null;
                            first = stack[^2].Value;
                            second = stack[^1].Value;
                            depth = 1 + Math.Max(stack[^2].Depth, stack[^1].Depth);
                            stack.RemoveRange(stack.Count - 2, 2);
                            break;
                        default:
                            return null;
                    }
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
                    first = target.Value;
                    second = target.Member;
                    if (target.Member < 0)
                    {
                        // These shapes never passed the dynamic builtin proof.
                        if (site.ArgumentCount != 1 || values[first].Operation != NativeReturnOperation.Read) return null;
                        operation = NativeReturnOperation.BuiltinUnary;
                    }
                    else
                    {
                        var member = Names[instructions[target.Member].NameIndex];
                        var selected = member switch
                        {
                            "split" when site.ArgumentCount <= 2 => NativeReturnOperation.Split,
                            "replace" when site.ArgumentCount is 2 or 3 => NativeReturnOperation.Replace,
                            "startswith" when site.ArgumentCount == 1 => NativeReturnOperation.StartsWith,
                            "endswith" when site.ArgumentCount == 1 => NativeReturnOperation.EndsWith,
                            _ when site.ArgumentCount == 0 => LythonRuntime.StringMembers.GetNoArgumentsKind(member),
                            _ => null
                        };
                        if (selected is null) return null;
                        operation = selected.Value;
                        if (operation == NativeReturnOperation.Split && site.ArgumentCount == 0)
                        {
                            if (values[first].Operation == NativeReturnOperation.Lower) operation = NativeReturnOperation.SplitLowerWhitespace;
                            else if (values[first].Operation == NativeReturnOperation.Upper) operation = NativeReturnOperation.SplitUpperWhitespace;
                        }
                    }
                    for (var a = 0; a < site.ArgumentCount; a++)
                        site.Arguments[a] = site.Arguments[a] with
                        {
                            NativeValueIndex = stack[start + 1 + a].Value
                        };
                    stack.RemoveRange(start, site.ArgumentCount + 1);
                    break;
                default:
                    return null;
            }
            // Eligibility bound for CLR-recursive evaluation, never a guest limit.
            // Deeper expressions still execute through the ordinary engine.
            if (depth > 16) return null;
            values.Add(new(operation, index, first, second));
            stack.Add((values.Count - 1, -1, depth));
        }
        if (stack.Count != 1 || stack[0].Member >= 0) return null;
        if (hasFormattedBuilder && values[stack[0].Value].Operation != NativeReturnOperation.FinishFormat)
            return null;
        return new(values.ToArray(), stack[0].Value);
    }
}
