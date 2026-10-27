using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class NativeSliceAdmissionTests
{
    [Theory]
    [InlineData("repr")]
    [InlineData("str")]
    public async Task OutputIsAdmittedAfterBoundCallback(string operation)
    {
        using var script = new LythonEngine().Compile("import time\nclass Gate:\n def __repr__(self):time.time();return 'gate'\ns=slice('x'*262144,Gate(),None)\nvalue="+operation+"(s)\ntime.time()");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[] {false,true})
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget,32768,new MockLythonHost());
            using(var result = asynchronous ? await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                : script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.True(host.AllocatedAtGate>0);
                var allocated=GC.GetTotalAllocatedBytes(true)-host.AllocatedAtGate;
                Assert.InRange(allocated,0,128*1024);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task ExactIndicesAdmitLargeArithmeticBeforeAllocation()
    {
        using var script=new LythonEngine().Compile("import time\nlength=1<<2097152\ntime.time()\nvalue=slice(None,None,-1).indices(length)");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[] {false,true})
        {
            var budget=new AdmissionGateBudget();var host=new AdmissionGateHost(budget,32768,new MockLythonHost());
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget }):script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.InRange(GC.GetTotalAllocatedBytes(true)-host.AllocatedAtGate,0,128*1024);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedRenderedResultsStayFunded()
    {
        using var script = new LythonEngine().Compile("return repr(slice('x'*262144,None,None))");
        var budget = new LythonMemoryBudget(4*1024*1024);
        foreach(var asynchronous in new[] {false,true})
        {
            using(var result = asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget })
                : script.Run(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal("slice('"+new string('x',262144)+"', None, None)",result.ReturnValue);
                Assert.True(budget.ReservedBytes>=262144);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
}
