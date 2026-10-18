using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context=Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class AnyAllCursorAllocationDiagnostic(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
    {
        foreach(var all in new[]{false,true})
        {
            foreach(var shape in new[]{"list","tuple"})
            foreach(var count in new[]{0,65})
            foreach(var early in count==0 ? new[]{false} : new[]{false,true})
                yield return [true,all,shape,count,early];
            yield return [false,all,"list",65,false];
            yield return [true,all,"bytes",65,false];
            yield return [true,all,"range",all?65:1,false];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Allocation(bool asynchronous,bool all,string shape,int count,bool early)
    {
        var budget=new LythonMemoryBudget(64*1024*1024);
        using var owner=budget.CreateScope();
        var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner});
        var span=new LythonSourceSpan(0,1,1,1);
        var values=Enumerable.Repeat((object)all,count).ToArray();
        if(early)values[0]=!all;
        object value=shape switch
        {
            "tuple"=>new PyTuple(values),
            "bytes"=>new PyBytes(Enumerable.Repeat(all?(byte)1:(byte)0,count).ToArray()),
            "range"=>new PyRange(all?BigInteger.One:BigInteger.Zero,all?new BigInteger(66):BigInteger.One,BigInteger.One),
            _=>new PyList(values)
        };
        object[] arguments=[value];
        var name=(all?"All":"Any")+(asynchronous?"Async":"");
        var method=typeof(LythonRuntime).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!;
        var sync=asynchronous?null:method.CreateDelegate<Func<object[],LythonSourceSpan,Context,object>>();
        var async=asynchronous?method.CreateDelegate<Func<object[],LythonSourceSpan,Context,ValueTask<object>>>():null;
        var samples=new List<long>();
        object? result=null;
        for(var sample=-2;sample<8;sample++)
        {
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var call=0;call<64;call++)
            {
                if(asynchronous)
                {
                    var operation=async!(arguments,span,context);
                    Assert.True(operation.IsCompletedSuccessfully);
                    result=await operation;
                }
                else result=sync!(arguments,span,context);
            }
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(early?!all:all,Assert.IsType<bool>(result));
            Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
            if(sample>=0)samples.Add(allocated);
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("ANY_ALL_CURSOR_ALLOCATION:"+JsonSerializer.Serialize(new{asynchronous,all,shape,count,early,callsPerSample=64,samples}));
    }
}
