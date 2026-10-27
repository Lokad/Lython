using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class NumericArithmeticAdmissionTests
{
    [Theory]
    [InlineData("value/value")]
    [InlineData("operator.truediv(value,value)")]
    public async Task LargeRatioRefusesBeforeCopyingWorkspace(string expression)
    {
        using var script = new LythonEngine().Compile("import operator,time\nvalue=1<<2097152\ntime.time()\ntry:result=" + expression + "\nfinally:return 0");
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

    [Fact]
    public async Task RatioWorkspaceReturnsAllowanceBeforeContinuation()
    {
        using var script = new LythonEngine().Compile("import time\nvalue=1<<2097152\ntime.time()\nresult=value/value\ntime.time()\nreturn result");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 8 * 1024 * 1024);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(1.0, Assert.IsType<double>(result.ReturnValue));
                Assert.InRange(host.AtSecondGate - budget.AtGate, 0L, 8192L);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }
}
