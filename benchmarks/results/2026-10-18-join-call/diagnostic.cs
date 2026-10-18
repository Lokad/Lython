using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JoinCallDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<bool,string> Routes => new()
    {
        {false,"direct"},{true,"direct"},{false,"generator"},{true,"generator"},
        {false,"exposed"},{true,"exposed"},{false,"expanded"},{true,"expanded"},
        {false,"custom"},{true,"custom"}
    };
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Allocation(bool asynchronous,string route)
    {
        var code=Compile(Source(route));
        var budget=new LythonMemoryBudget(64*1024*1024);using var owner=budget.CreateScope();
        var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner,MaxExecutionSteps=10_000_000});
        var seed=PyString.FromString("café😀",context.MemoryGovernor,null);
        var parts=new PyList(Enumerable.Repeat<object>(seed,16),context.MemoryGovernor,null);
        Setup(context,seed,parts);
        context.Variables["method"]=LythonRuntime.ExecuteExecutableCodeObject(Compile("return sep.join"),context,default,null)!;
        var samples=new List<long>();
        for(var sample=-2;sample<8;sample++)
        {
            var thread=Environment.CurrentManagedThreadId;var before=GC.GetAllocatedBytesForCurrentThread();object? result=null;
            for(var call=0;call<64;call++)
            {
                var pending=Execute(code,context,asynchronous);Assert.True(pending.IsCompletedSuccessfully);result=await pending;
            }
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(thread,Environment.CurrentManagedThreadId);
            Assert.Equal(route=="custom" ? "café😀" : string.Join("|",Enumerable.Repeat("café😀",16)),Assert.IsType<PyString>(result).AsString());
            Check(context);if(sample>=0)samples.Add(allocated);
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("JOIN_CALL_ALLOCATION:"+JsonSerializer.Serialize(new{asynchronous,route,callsPerSample=64,samples}));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var records=new List<object>();
        foreach(var route in new[]{"direct","generator","invalid","expanded","custom","callback","callback-error"})
        {
            var code=Compile(Source(route));
            for(var fuel=1;fuel<=32;fuel++)
            {
                var budget=new LythonMemoryBudget();using var owner=budget.CreateScope();
                var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner,MaxExecutionSteps=fuel});
                var seed=PyString.FromString("café😀",context.MemoryGovernor,null);
                var parts=new PyList([seed,route=="invalid" ? BigInteger.One : seed],context.MemoryGovernor,null);
                var (make,custom)=Setup(context,seed,parts,route=="callback-error");
                string? result=null,bytes=null,exception=null,message=null,reason=null;LythonSourceSpan? span=null;
                try
                {
                    var value=Assert.IsType<PyString>(await Execute(code,context,asynchronous));
                    result=value.AsString();bytes=Convert.ToHexString(value.Utf8Bytes.Span);
                    Assert.Same(context.MemoryGovernor,value.OwnerMemoryGovernor);
                }
                catch(LythonRuntimeException error){exception=error.ExceptionType;message=error.Message;reason=error.Attribution?.Reason.ToString();span=error.Span;}
                Check(context);owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
                records.Add(new{route,fuel,result,bytes,exception,message,reason,span,calls=make.Calls,customCalls=custom.Calls,steps=context.Limits.ExecutionStepCount});
            }
        }
        output.WriteLine("JOIN_CALL_WORK:"+JsonSerializer.Serialize(new{asynchronous,records}));
    }
    private static (Hook Make,Hook Custom) Setup(Context context,PyString seed,PyList parts,bool fail=false)
    {
        var make=new Hook(parts,fail);var custom=new Hook(seed,false);
        context.Variables["sep"]=PyString.FromString("|",context.MemoryGovernor,null);
        context.Variables["parts"]=parts;context.Variables["make"]=make;
        Assert.True(context.TryGetBuiltin("object",out var objectType));
        context.Variables["custom"]=new PyInstance(new PyType("Separator",[Assert.IsType<PyType>(objectType)],new Dictionary<string,object>{{"join",custom}}));
        return(make,custom);
    }
    private static string Source(string route)=>route switch
    {
        "generator"=>"return sep.join(x for x in parts)","exposed"=>"return method(parts)",
        "expanded"=>"return sep.join(*[parts])","custom"=>"return custom.join(parts)",
        "callback" or "callback-error"=>"return sep.join(make())",_=>"return sep.join(parts)"
    };
    private sealed class Hook(object result,bool fail):LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments,LythonSourceSpan span,Context context)
        {Calls++;return fail ? throw new LythonRuntimeException("ValueError","callback",span) : result;}
    }
    private static void Check(Context context)
    {Assert.Null(context.CurrentExecutableFrame);Assert.Equal(0,context.Limits.CurrentInterpreterDepth);Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);}
    private static ExecutableCodeObject Compile(string source)=>ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code,Context context,bool asynchronous)
        =>asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code,context) : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code,context,default,null));
}
