namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableCodeObject
{
    // A native get/add/store can reuse one table probe. Original instructions
    // remain the pre-effect path for dynamic values, growth and other methods.
    private void PrepareDictionaryAccumulation(ExecutableCallSite[] sites)
    {
        for (var index = 0; index + 2 < Instructions.Length; index++)
        {
            ref readonly var call = ref Instructions[index];
            ref readonly var add = ref Instructions[index + 1];
            ref readonly var store = ref Instructions[index + 2];
            if (call.OpCode != ExecutableOpCode.Call || call.HasLocalResult
                || add.OpCode != ExecutableOpCode.Binary || add.BinaryOperator != BinaryOperatorSyntax.Add || add.HasLocalResult
                || store.OpCode != ExecutableOpCode.StoreSubscript
                || add.InputStart != call.ResultSlot || add.ResultSlot != call.ResultSlot || add.LiveExtent != call.LiveExtent
                || store.InputStart != call.ResultSlot || store.LiveExtent != call.ResultSlot
                || !IsUncapturedLocal(store.SecondInput) || !IsUncapturedLocal(store.ThirdInput)
                || !(add.SecondInput < 0 || IsUncapturedLocal(add.SecondInput))) continue;
            var site = sites[call.CallSiteIndex];
            if (!site.HasReceiver || !site.IsPositionalOnly || site.ArgumentCount is not (1 or 2)) continue;
            var crossesBoundary = false;
            foreach (var region in ExceptionRegions)
                if (InteriorBoundary(region, index)) { crossesBoundary = true; break; }
            if (crossesBoundary) continue;
            sites[call.CallSiteIndex] = site with { AccumulatesDictionary = true };
        }

        bool IsUncapturedLocal(int slot) => slot >= 0 && slot < FirstTemporarySlot && !CapturedLocalSlots.Contains(slot);

        // Do not skip across protection/handler boundaries. Routing an error
        // with the call current must select the same handler as add or store.
        static bool InteriorBoundary(ExecutableExceptionRegion region, int index)
        {
            return Inside(region.ProtectedStartIndex) || Inside(region.ProtectedEndIndex + 1)
                || Inside(region.ExceptIndex) || Inside(region.FinallyIndex)
                || Inside(region.SuiteStartIndex) || Inside(region.SuiteEndIndex is int end ? end + 1 : null);
            bool Inside(int? position) => position > index && position <= index + 2;
        }
    }
}
