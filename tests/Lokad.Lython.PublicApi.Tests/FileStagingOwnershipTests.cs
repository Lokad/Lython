using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class FileStagingOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartlyFilledTextBufferRefusesBeforeCopyOrPublication(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import time\npayload='x'*262144\nf=open('out','w');f.write(payload);f.write('x')\ntime.time()\nf.flush()");
        Assert.True(script.IsValid);
        for (var pass=0;pass<3;pass++)
        {
            var budget = new AdmissionGateBudget();
            ILythonHost files = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            var host = new AdmissionGateHost(budget,32768,files);
            using (var result = asynchronous ? await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}) : script.Run(host,new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.True(result.DeniedReservationBytes>262144);
#if !DEBUG
                if(pass==2) Assert.InRange(GC.GetTotalAllocatedBytes(true)-host.AllocatedAtGate,0,65536);
#endif
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false,"utf-8")]
    [InlineData(true,"utf-8")]
    [InlineData(false,"latin-1")]
    [InlineData(true,"latin-1")]
    [InlineData(false,"utf-16")]
    [InlineData(true,"utf-16")]
    public async Task CompletedFlushStagingDoesNotAccumulate(bool asynchronous,string encoding)
    {
        async Task<long> Measure(int iterations)
        {
            using var script = new LythonEngine().Compile("import time\npayload='x'*32768\nf=open('out','w',encoding='"+encoding+"')\ntime.time()\nfor i in range("+iterations+"):\n f.write(payload)\n f.flush()\ntime.time()\nf.close()");
            Assert.True(script.IsValid);
            var budget = new AdmissionGateBudget();
            ILythonHost files = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            var host = new AdmissionGateHost(budget,1024*1024,files);
            long retained;
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}):script.Run(host,new LythonRunOptions{MemoryBudget=budget}))
            { Assert.True(result.Success,result.Failure?.Message);retained=host.AtSecondGate-budget.AtGate; }
            Assert.Equal(0,budget.Root.ReservedBytes);
            return retained;
        }
        Assert.Equal(await Measure(1),await Measure(4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeBinaryReadTransfersItsFinalCopyFunding(bool asynchronous)
    {
        const int size=262144;
        using var script = new LythonEngine().Compile("import time\nf=open('data','rb')\ntime.time()\nvalue=f.read()\nf.close()\nreturn len(value)");
        Assert.True(script.IsValid);
        var mock = new MockLythonHost();mock.SeedBytes("/data",new byte[size]);
        var delayed = new DelayedLythonHost();delayed.SeedBytes("/data",new byte[size]);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget,size*5/2+32768,asynchronous?delayed:mock);
        using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}):script.Run(host,new LythonRunOptions{MemoryBudget=budget}))
        { Assert.True(result.Success,result.Failure?.Message);Assert.Equal(new System.Numerics.BigInteger(size),result.ReturnValue); }
        Assert.Equal(0,budget.Root.ReservedBytes);
    }
}
