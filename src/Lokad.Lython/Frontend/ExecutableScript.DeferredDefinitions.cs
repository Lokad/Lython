using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    private int[]? FindDeferredDefinitionLocals(List<ExecutableInstruction> instructions, CancellationToken token)
    {
        // Use the original tape before physical operand forwarding can erase
        // a load. Limit this to an already array-backed lexical activation.
        if (!UsesOnlyLexicalSlots || FirstTemporarySlot <= LythonRuntime.NativeValueCapacity ||
            CapturedLocalSlots.Count != 0 || ClosureNames.Count != 0)
            return null;
        var definitions = new int[FirstTemporarySlot];
        for (var i = 0; i < instructions.Count; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            var step = instructions[i];
            switch (step.OpCode)
            {
                case ExecutableOpCode.LoadLocal:
                    definitions[step.LocalSlot] = -1;
                    break;
                case ExecutableOpCode.StoreLocal:
                    ref var definition = ref definitions[step.LocalSlot];
                    if (definition == 0 && i != 0 &&
                        instructions[i - 1] is { OpCode: ExecutableOpCode.ApplyOperation } create &&
                        Constants[create.ConstantIndex] is ExecutableCreateFunction
                        {
                            PrepaysCallable: true, IsGenerator: false,
                            ParameterLayout: { PositionalDefaultCount: 1, Defaults.Length: 2 },
                            CodeObject.UsesOnlyLexicalSlots: true,
                        })
                        definition = create.ConstantIndex + 1;
                    else definition = -1;
                    break;
                case ExecutableOpCode.ApplyOperation when Constants[step.ConstantIndex] is ExecutableCreateFunction:
                case ExecutableOpCode.LoadConst or ExecutableOpCode.LoadGlobal or ExecutableOpCode.Binary
                    or ExecutableOpCode.Call or ExecutableOpCode.StartFunctionDefaults or ExecutableOpCode.StoreFunctionDefault
                    or ExecutableOpCode.MakeTuple or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone:
                    break;
                default:
                    return null;
            }
        }
        return definitions;
    }

    private int[]? PrepareDeferredDefinitions(int[]? candidates, CancellationToken token, out int valueCapacity)
    {
        valueCapacity = ValueStorageCapacity;
        if (candidates is null) return null;
        List<int>? sites = null;
        for (var i = 0; i + 1 < Instructions.Length; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            var create = Instructions[i];
            if (create is not { OpCode: ExecutableOpCode.ApplyOperation, HasResultConsumer: true } ||
                Instructions[i + 1] is not { OpCode: ExecutableOpCode.StoreLocal } store ||
                candidates[store.LocalSlot] != create.ConstantIndex + 1)
                continue;
            sites ??= [];
            var definition = (ExecutableCreateFunction)Constants[create.ConstantIndex]!;
            // A shared tuple needs no slot. The local can hold the prepaid keyword default until publication.
            var keywordSlot = definition.LiteralPositionalDefaults is null ? checked(valueCapacity++) : store.LocalSlot;
            Instructions[i] = create.WithDeferredDefinition(keywordSlot);
            sites.Add(i);
        }
        return sites?.ToArray();
    }
}
