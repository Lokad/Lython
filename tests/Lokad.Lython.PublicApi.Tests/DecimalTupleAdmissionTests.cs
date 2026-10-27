using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class DecimalTupleAdmissionTests
{
    [Fact]
    public async Task SequenceProjectionRefusesBeforeCopyingAnUnfundedDigitArray()
    {
        using var script = new LythonEngine().Compile("from decimal import DecimalTuple\nimport time\nvalue=DecimalTuple(0,[0]*16384,0)\ntime.time()\nreturn value");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 8192);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
#if !DEBUG
                Assert.InRange(GC.GetTotalAllocatedBytes(true) - host.AllocatedAtGate, 0L, 131071L);
#endif
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }
}
