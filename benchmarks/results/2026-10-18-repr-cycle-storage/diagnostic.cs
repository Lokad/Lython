using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ReprCycleDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Shapes = ["direct-text", "direct-integer", "text", "integer", "boolean", "none", "float", "ascii", "conversion", "empty-set", "list-control", "tuple-control", "dict-control", "set-control", "list-cycle-control", "tuple-cycle-control", "view-cycle-control"];
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var cases = new TheoryData<string, bool>();
            foreach (var shape in Shapes) foreach (var mode in new[] { false, true }) cases.Add(shape, mode);
            return cases;
        }
    }
    private static string Source(string shape) => shape switch
    {
        "ascii" => "return ascii(value)", "conversion" => "return f'{value!r}'", _ => "return repr(value)"
    };
    private static string Expected(string shape) => shape switch
    {
        "direct-integer" or "integer" => "-12345", "boolean" => "True", "none" => "None", "float" => "3.25",
        "ascii" => "'caf\\xe9 \\U0001f600\\n'", "empty-set" => "set()", "list-control" => "[1, 2]", "tuple-control" => "(1, 2)",
        "dict-control" => "{'x': 1}", "set-control" => "{1, 2}", "list-cycle-control" => "[[...]]",
        "tuple-cycle-control" => "([(...)],)", "view-cycle-control" => "dict_values([{'self': {...}}])", _ => "'caf\u00e9 \U0001f600\\n'"
    };
    private static ExecutableCodeObject Compile(string source) => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static Context Prepare(string shape, ILythonMemoryBudget owner)
    {
        object? value = shape switch { "integer" or "direct-integer" => new BigInteger(-12345), "boolean" => true, "none" => null, "float" => 3.25, _ => "caf\u00e9 \U0001f600\n" };
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000, Globals = new Dictionary<string, object?> { ["value"] = value } });
        var setup = shape switch
        {
            "empty-set" => "value=set()", "list-control" => "value=[1,2]", "tuple-control" => "value=(1,2)",
            "dict-control" => "value={'x':1}", "set-control" => "value={1,2}", "list-cycle-control" => "value=[]\nvalue.append(value)",
            "tuple-cycle-control" => "items=[]\nvalue=(items,)\nitems.append(value)", "view-cycle-control" => "items={}\nitems['self']=items\nvalue=items.values()", _ => null
        };
        if (setup is not null) _ = LythonRuntime.ExecuteExecutableCodeObject(Compile("global value\n"+setup), context, default, null);
        return context;
    }
    private static ValueTask<object?> Run(string shape, ExecutableCodeObject code, Context context, bool mode)
        => shape.StartsWith("direct-", StringComparison.Ordinal) ? new(PyRendering.ToReprPyString(context.Variables["value"], new PyRenderingContext(context)))
            : mode ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    [Theory] [MemberData(nameof(Cases))]
    public async Task Allocation(string shape, bool mode)
    {
        var code = Compile(Source(shape)); var budget = new LythonMemoryBudget(16*1024*1024); using var owner = budget.CreateScope(); var context = Prepare(shape, owner);
        var samples = new List<long>(); var steps = new List<long>(); var committed = new List<long>();
        for (var sample=-2;sample<8;sample++)
        {
            var thread=Environment.CurrentManagedThreadId;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
            var before=GC.GetAllocatedBytesForCurrentThread();object? result=null;
            for(var call=0;call<8;call++){var pending=Run(shape,code,context,mode);Assert.True(pending.IsCompletedSuccessfully);result=await pending;}
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(thread,Environment.CurrentManagedThreadId);Check(context);Assert.Equal(Expected(shape),Assert.IsType<PyString>(result).AsString());
            if(sample>=0){samples.Add(bytes);steps.Add(context.Limits.ExecutionStepCount-work);committed.Add(context.MemoryGovernor.CurrentCommittedBytes-funding);}
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("REPR_CYCLE_ALLOCATION:"+JsonSerializer.Serialize(new {shape,asynchronous=mode,samples,steps,committed,callsPerSample=8,outputSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Expected(shape))))}));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Boundaries(bool mode)
    {
        var fuelRecords=new List<object>();var admissionRecords=new List<object>();
        foreach(var shape in new[]{"text","integer","ascii","conversion","list-cycle-control","tuple-cycle-control","view-cycle-control","empty-set"})
        foreach(var fuel in Enumerable.Range(0,18).Append(128))
        {
            var code=Compile(Source(shape));var budget=new LythonMemoryBudget(16*1024*1024);using var owner=budget.CreateScope();var context=Prepare(shape,owner);
            context.Limits.ExecutionStepCount=10000000-fuel;object? result=null;LythonRuntimeException? error=null;
            try{result=await Run(shape,code,context,mode);}catch(LythonRuntimeException ex){error=ex;}
            Check(context);fuelRecords.Add(new{shape,fuel,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-(10000000-fuel)});
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        foreach(var shape in new[]{"text","integer","ascii","list-cycle-control"})
        foreach(var headroom in new[]{0,32,64,128,256,512,1024,4096})
        {
            var code=Compile(Source(shape));var budget=new LythonMemoryBudget(1024*1024);using var owner=budget.CreateScope();var context=Prepare(shape,owner);
            Assert.True(budget.TryReserve(1024*1024-budget.ReservedBytes-headroom,out var padding));
            using(padding)
            {
                object? result=null;LythonRuntimeException? error=null;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
                try{result=await Run(shape,code,context,mode);}catch(LythonRuntimeException ex){error=ex;Assert.Same(context.MemoryGovernor.AdmissionFailure,error);}
                Check(context);admissionRecords.Add(new{shape,headroom,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-work,committed=context.MemoryGovernor.CurrentCommittedBytes-funding,denied=context.MemoryGovernor.LastDeniedReservationBytes});
            }
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        output.WriteLine("REPR_CYCLE_BOUNDARIES:"+JsonSerializer.Serialize(new{asynchronous=mode,fuelRecords,admissionRecords}));
    }
    private static object? Describe(LythonRuntimeException? error)=>error is null?null:new{error.ExceptionType,error.Message,error.Span,reason=error.Attribution?.Reason.ToString()};
}
