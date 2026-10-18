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
public sealed class JsonResultCopyDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<bool,string> Routes => new()
    {
        {false,"ascii"},{true,"ascii"},{false,"unicode"},{true,"unicode"},
        {false,"escaped"},{true,"escaped"},{false,"edge"},{true,"edge"},
        {false,"astral-edge"},{true,"astral-edge"},{false,"over"},{true,"over"},
        {false,"encode"},{true,"encode"},{false,"iterator"},{true,"iterator"},
        {false,"iterator-over"},{true,"iterator-over"},{false,"container"},{true,"container"}
    };
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Allocation(bool asynchronous,string route)
    {
        var expression=route switch
        {
            "encode"=>"json.JSONEncoder(ensure_ascii=False).encode(value)",
            "iterator" or "iterator-over" or "container"=>"list(json.JSONEncoder(ensure_ascii=False).iterencode(value))",
            "escaped"=>"json.dumps(value)",
            _=>"json.dumps(value,ensure_ascii=False)",
        };
        var code=Compile("import json\nreturn "+expression);
        var budget=new LythonMemoryBudget(64*1024*1024);
        using var owner=budget.CreateScope();
        var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner,MaxExecutionSteps=10_000_000});
        var text=route switch
        {
            "ascii"=>"hello",
            "edge"=>new string('x',126),
            "over" or "iterator-over"=>new string('x',127),
            "astral-edge"=>string.Concat(Enumerable.Repeat("😀",63)),
            _=>"café😀",
        };
        var scalar=PyString.FromString(text,context.MemoryGovernor,null);
        context.Variables["value"]=route=="container" ? new PyList([scalar,BigInteger.One],context.MemoryGovernor,null) : scalar;
        var expected=route=="escaped" ? "\"caf\\u00e9\\ud83d\\ude00\"" : route=="container" ? "[\"café😀\", 1]" : "\""+text+"\"";
        var samples=new List<long>();
        for(var sample=-4;sample<8;sample++)
        {
            var thread=Environment.CurrentManagedThreadId;var before=GC.GetAllocatedBytesForCurrentThread();
            object? result=null;
            for(var call=0;call<128;call++)
            {
                var pending=Execute(code,context,asynchronous);Assert.True(pending.IsCompletedSuccessfully);result=await pending;
            }
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(thread,Environment.CurrentManagedThreadId);
            var rendered=result is PyString value ? value.AsString() : string.Concat(Assert.IsType<PyList>(result).Cast<PyString>().Select(s=>s.AsString()));
            Assert.Equal(expected,rendered);CheckCleanup(context);
            if(sample>=0)samples.Add(allocated);
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("JSON_RESULT_ALLOCATION:"+JsonSerializer.Serialize(new{asynchronous,route,callsPerSample=128,samples}));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var records=new List<object>();
        foreach(var route in new[]{"default","encode","iterator","sorted","hook","cycle","hook-error"})
        {
            var expression=route switch
            {
                "encode"=>"json.JSONEncoder(ensure_ascii=False).encode(value)",
                "iterator"=>"list(json.JSONEncoder(ensure_ascii=False).iterencode(value))",
                "sorted"=>"json.dumps(value,sort_keys=True)",
                "hook" or "hook-error"=>"json.dumps(value,default=hook,ensure_ascii=False)",
                _=>"json.dumps(value)",
            };
            var code=Compile("import json\nreturn "+expression);
            for(var fuel=1;fuel<=40;fuel++)
            {
                var budget=new LythonMemoryBudget();using var owner=budget.CreateScope();
                var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner,MaxExecutionSteps=fuel});
                var seed=PyString.FromString("café😀",context.MemoryGovernor,null);
                object input=seed;
                if(route=="sorted")
                {
                    var dict=new PyDict(context.MemoryGovernor,null);dict.SetItem(PyString.FromString("z"),seed);dict.SetItem(PyString.FromString("a"),BigInteger.One);input=dict;
                }
                if(route=="cycle") {var list=new PyList([],context.MemoryGovernor,null);list.Add(list);input=list;}
                if(route is "hook" or "hook-error")input=new PyInstance(new PyType("Value",[],new Dictionary<string,object>()));
                var hook=new Hook(seed,route=="hook-error");context.Variables["value"]=input;context.Variables["hook"]=hook;
                string? result=null,exception=null,message=null,reason=null;string[]? chunks=null;LythonSourceSpan? span=null;
                try
                {
                    var value=await Execute(code,context,asynchronous);
                    if(value is PyList list)chunks=list.Cast<PyString>().Select(v=>v.AsString()).ToArray();
                    result=value is PyString text ? text.AsString() : string.Concat(chunks!);
                }
                catch(LythonRuntimeException error){exception=error.ExceptionType;message=error.Message;reason=error.Attribution?.Reason.ToString();span=error.Span;}
                CheckCleanup(context);owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
                records.Add(new{route,fuel,result,chunks,exception,message,reason,span,calls=hook.Calls,steps=context.Limits.ExecutionStepCount});
            }
        }
        output.WriteLine("JSON_RESULT_WORK:"+JsonSerializer.Serialize(new{asynchronous,records}));
    }
    private sealed class Hook(object result,bool fail):LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments,LythonSourceSpan span,Context context)
        {Calls++;return fail ? throw new LythonRuntimeException("ValueError","callback",span) : result;}
    }
    private static void CheckCleanup(Context context)
    {Assert.Null(context.CurrentExecutableFrame);Assert.Equal(0,context.Limits.CurrentInterpreterDepth);Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);}
    private static ExecutableCodeObject Compile(string source)=>ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code,Context context,bool asynchronous)
        =>asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code,context) : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code,context,default,null));
}
