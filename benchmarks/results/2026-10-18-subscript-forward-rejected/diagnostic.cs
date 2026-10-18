using System.Diagnostics;
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
public sealed class SubscriptForwardDiagnostic(ITestOutputHelper output)
{
    [Fact]
    public async Task NativeReads()
    {
        var code = Compile("total=0\nfor data in values: total+=data[0]\nreturn total");
        var budget = new LythonMemoryBudget(64*1024*1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget=owner, MaxExecutionSteps=20_000_000 });
        context.Variables["values"] = new PyList(Enumerable.Range(0,128).Select(i => (object)new PyList([BigInteger.One],context.MemoryGovernor,null)),context.MemoryGovernor,null);
        var samples = new List<object>();
        for (var sample=-8; sample<12; sample++)
        foreach (var asynchronous in sample%2==0 ? new[]{false,true} : new[]{true,false})
        {
            var thread=Environment.CurrentManagedThreadId;
            var allocated=GC.GetAllocatedBytesForCurrentThread();
            var start=Stopwatch.GetTimestamp();
            object? result=null;
            for(var run=0;run<32;run++)
            {
                var pending=Execute(code,context,asynchronous);
                Assert.True(pending.IsCompletedSuccessfully);
                result=await pending;
            }
            var ticks=Stopwatch.GetTimestamp()-start;
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Assert.Equal(thread,Environment.CurrentManagedThreadId);
            Assert.Equal(new BigInteger(128),Assert.IsType<BigInteger>(result));
            CheckCleanup(context);
            if(sample>=0)samples.Add(new{sample,asynchronous,ticks,allocated});
        }
        owner.Dispose(); Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("SUBSCRIPT_NATIVE:"+JsonSerializer.Serialize(new{callsPerSample=4096,frequency=Stopwatch.Frequency,samples}));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var code=Compile("return target[index]");
        var records=new List<object>();
        foreach(var kind in new[]{"native","negative","out-of-range","noninteger","guest-index","bad-index","guest-item","raising-item"})
        for(var fuel=1;fuel<=20;fuel++)
        {
            var budget=new LythonMemoryBudget();
            using var owner=budget.CreateScope();
            var context=new Context(new MockLythonHost(),new LythonRunOptions {MemoryBudget=owner,MaxExecutionSteps=fuel});
            var seed=PyString.FromString("café😀",context.MemoryGovernor,null);
            var callback=new Callback(seed,kind);
            context.Variables["target"]=kind is "guest-item" or "raising-item"
                ? new PyInstance(new PyType("Box",[],new Dictionary<string,object>{{"__getitem__",callback}}))
                : new PyList([seed,BigInteger.One],context.MemoryGovernor,null);
            context.Variables["index"]=kind switch
            {
                "negative"=>new BigInteger(-1),
                "out-of-range"=>BigInteger.One<<100,
                "noninteger"=>PyString.FromString("bad"),
                "guest-index" or "bad-index"=>new PyInstance(new PyType("Index",[],new Dictionary<string,object>{{"__index__",callback}})),
                _=>BigInteger.Zero,
            };
            string? result=null,exception=null,message=null,reason=null;
            LythonSourceSpan? span=null; bool? identity=null;
            try
            {
                var value=await Execute(code,context,asynchronous);
                result=value is PyString text ? text.AsString() : value?.ToString(); identity=ReferenceEquals(value,seed);
            }
            catch(LythonRuntimeException error)
            { exception=error.ExceptionType;message=error.Message;reason=error.Attribution?.Reason.ToString();span=error.Span; }
            CheckCleanup(context);owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
            records.Add(new{kind,fuel,result,identity,exception,message,reason,span,calls=callback.Calls,steps=context.Limits.ExecutionStepCount});
        }
        output.WriteLine("SUBSCRIPT_WORK:"+JsonSerializer.Serialize(new{asynchronous,records}));
    }
    private sealed class Callback(object seed,string kind):LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments,LythonSourceSpan span,Context context)
        {
            Calls++;
            if(kind=="raising-item")throw new LythonRuntimeException("ValueError","item",span);
            return kind switch {"guest-index"=>BigInteger.One,"bad-index"=>PyString.FromString("bad"),_=>seed};
        }
    }
    private static void CheckCleanup(Context context)
    {Assert.Null(context.CurrentExecutableFrame);Assert.Equal(0,context.Limits.CurrentInterpreterDepth);Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);}
    private static ExecutableCodeObject Compile(string source)
        =>ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code,Context context,bool asynchronous)
        =>asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code,context)
            :ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code,context,default,null));
}
