using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class NativeSliceAdmissionTests
{
    [Theory]
    [InlineData("repr", false)] [InlineData("repr", true)]
    [InlineData("str", false)] [InlineData("str", true)]
    public async Task RenderingRetainsPriorCapacityAndAdmitsNewStorageAfterCallback(string operation, bool fundedPrefix)
    {
        var arguments = fundedPrefix ? "'x'*262144,Gate(),None" : "Gate(),'x'*262144,None";
        using var script = new LythonEngine().Compile("import time\nclass Gate:\n def __repr__(self):time.time();return 'gate'\ns=slice("+arguments+")\nvalue="+operation+"(s)\ntime.time()");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[] {false,true})
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget,32768,new MockLythonHost());
            using(var result = asynchronous ? await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                : script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.Equal(fundedPrefix, result.Success);
                if (!fundedPrefix) Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.True(host.AllocatedAtGate>0);
                // The gate and refusal/completed rendering are synchronous on
                // one execution thread. Exclude other process traffic and later
                // diagnostic/async cleanup; keep the copy-sized allocation bound.
                Assert.Equal(host.ThreadAtGate,fundedPrefix?host.ThreadAtSecondGate:budget.ThreadAtFirstRefusal);
                var allocated=(fundedPrefix?host.ThreadAllocatedAtSecondGate:budget.ThreadAllocatedAtFirstRefusal)-host.ThreadAllocatedAtGate;
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
                Assert.Equal(host.ThreadAtGate,budget.ThreadAtFirstRefusal);
                Assert.InRange(budget.ThreadAllocatedAtFirstRefusal-host.ThreadAllocatedAtGate,0,128*1024);
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
