using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class DecimalSignalValidationTests
{
    [Fact]
    public void RepeatedValidationKeepsExistingMapsAndAvoidsTransientAllocation()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var policy = PyDecimalContext.Default(governor);
            var flags = policy.Flags;
            var traps = policy.Traps;
            var span = new LythonSourceSpan(0,0,0,0);
            policy.ValidateSignalMaps(span);
            var retained = root.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i=0;i<64;i++) policy.ValidateSignalMaps(span);
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread()-before < 1024);
#endif
            Assert.Same(flags,policy.Flags);
            Assert.Same(traps,policy.Traps);
            Assert.Equal(retained,root.ReservedBytes);
            Assert.Equal(0,governor.CurrentReservedBytes);
        }
        Assert.Equal(0,root.ReservedBytes);
    }
}
