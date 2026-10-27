using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class EncodingAdmissionTests
{
    [Theory]
    [InlineData("text.encode('utf-8')",262144)]
    [InlineData("bytes(text,'utf-8')",262144)]
    [InlineData("text.encode('ascii')",262144)]
    [InlineData("text.encode('utf-16-le')",131072)]
    public async Task GuestEncodingTransfersItsBackingWithoutReadmission(string expression,int count)
    {
        using var script=new LythonEngine().Compile("import time\ntext='x'*"+count+"\ntime.time()\nvalue="+expression+"\ntime.time()\nreturn len(value)");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[]{false,true})
        {
            var budget=new AdmissionGateBudget();var host=new AdmissionGateHost(budget,400000);
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}):script.Run(host,new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal("262144",result.ReturnValue?.ToString());
                Assert.InRange(host.AtSecondGate-budget.AtGate,262144L,399999L);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task CodecPayloadRefusalIsTerminalBeforeTheLargeCopy()
    {
        using var script=new LythonEngine().Compile("import time\ndata=b'x'*262144+b'\\xff'\ntime.time()\ntry:data.decode('utf-8')\nexcept UnicodeDecodeError:print('caught')\nfinally:return 0");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[]{false,true})
        {
            var budget=new AdmissionGateBudget();var host=new AdmissionGateHost(budget,32768);
            using(var result=asynchronous?await script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget}):script.Run(host,new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                // This is a before-copy check, not an exact object-size threshold.
                Assert.InRange(GC.GetTotalAllocatedBytes(true)-host.AllocatedAtGate,0L,262143L);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedCodecPayloadKeepsItsOwnerUntilResultCleanup()
    {
        var budget=new LythonMemoryBudget(4*1024*1024);
        using(var script=new LythonEngine().Compile("data=b'x'*262144+b'\\xff'\ntry:data.decode('utf-8')\nexcept UnicodeDecodeError as error:payload=error.object\nreturn payload",budget))
        {
            var baseline=budget.ReservedBytes;
            foreach(var asynchronous in new[]{false,true})
            {
                using(var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    var payload=Assert.IsType<byte[]>(result.ReturnValue);
                    Assert.Equal(262145,payload.Length);
                    Assert.Equal(255,payload[^1]);
                    Assert.True(budget.ReservedBytes>baseline+payload.Length);
                }
                Assert.Equal(baseline,budget.ReservedBytes);
            }
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
