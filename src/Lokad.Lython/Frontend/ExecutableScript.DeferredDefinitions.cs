namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    internal bool TrackDeferredDefinition(in ExecutableInstruction step, int[] definitions, ref int previousDefinition)
    {
        var previous = previousDefinition;
        previousDefinition = 0;
        switch (step.OpCode)
        {
            case ExecutableOpCode.LoadLocal:
                definitions[step.LocalSlot] = -1;
                return true;
            case ExecutableOpCode.StoreLocal:
                ref var definition = ref definitions[step.LocalSlot];
                definition = definition == 0 && previous != 0 ? previous : -1;
                return true;
            case ExecutableOpCode.ApplyOperation when Constants[step.ConstantIndex] is ExecutableCreateFunction create:
                if (create is
                    {
                        PrepaysCallable: true, IsGenerator: false,
                        ParameterLayout: { PositionalDefaultCount: 1, Defaults.Length: 2 },
                        CodeObject.UsesOnlyLexicalSlots: true,
                    })
                    previousDefinition = step.ConstantIndex + 1;
                return true;
            case ExecutableOpCode.LoadConst or ExecutableOpCode.LoadGlobal or ExecutableOpCode.Binary
                or ExecutableOpCode.Call or ExecutableOpCode.StartFunctionDefaults or ExecutableOpCode.StoreFunctionDefault
                or ExecutableOpCode.MakeTuple or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone:
                return true;
            default:
                return false;
        }
    }

    private int PlaceDeferredDefinitions(int[]? sites, CancellationToken token)
    {
        var valueCapacity = ValueStorageCapacity;
        if (sites is null) return valueCapacity;
        for (var i = 0; i < sites.Length; i++)
        {
            PythonTokenization.CheckCancellation(token, i);
            var index = sites[i];
            ref var create = ref Instructions[index];
            var definition = (ExecutableCreateFunction)Constants[create.ConstantIndex]!;
            // A compiled positional tuple leaves the local free for the prepaid keyword default.
            var keywordSlot = definition.LiteralPositionalDefaults is null
                ? checked(valueCapacity++) : Instructions[index + 1].LocalSlot;
            create = create.WithDeferredDefinition(keywordSlot);
        }
        return valueCapacity;
    }
}
