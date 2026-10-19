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
public sealed class IdentityWrapperDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Shapes = ["repr", "ascii", "field", "percent-s", "percent-r", "percent-a", "method", "mapping", "lowered", "join-control", "exception", "repr-container"];
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var rows = new TheoryData<string, bool>();
            foreach (var shape in Shapes) foreach (var mode in new[] { false, true }) rows.Add(shape, mode);
            return rows;
        }
    }
    private static string Source(string shape) => "return " + (shape switch
    {
        "repr" => "repr(value)", "ascii" => "ascii(value)", "percent-s" => "'%10.4s' % value", "percent-r" => "'%-10.4r' % value",
        "percent-a" => "'%a' % value", "method" => "'{0!r:>12}'.format(value)", "mapping" => "'{word!a}'.format_map({'word':value})",
        "join-control" => "','.join([value,value])", "exception" => "repr(ValueError(value))", "repr-container" => "repr([value])", _ => "f'{value!r:>12}'"
    });
    private static string Expected(string shape) => shape switch
    {
        "repr" => "'caf\u00e9 \U0001f600'", "ascii" or "percent-a" or "mapping" => "'caf\\xe9 \\U0001f600'",
        "percent-s" => "      caf\u00e9", "percent-r" => "'caf      ", "join-control" => "caf\u00e9 \U0001f600,caf\u00e9 \U0001f600",
        "exception" => "ValueError('caf\u00e9 \U0001f600')", "repr-container" => "['caf\u00e9 \U0001f600']", _ => "    'caf\u00e9 \U0001f600'"
    };
    private static (ExecutableCodeObject Code, LoweredExpression? Lowered) Compile(string shape)
    {
        var lowered = LoweredScript.Lower(LythonFrontend.Compile(Source(shape)).Script!);
        return (ExecutableScript.Compile(lowered).EntryPoint, shape == "lowered" ? Assert.IsType<LoweredReturnStatement>(Assert.Single(lowered.Statements)).Expression : null);
    }
    private static Context Prepare(ILythonMemoryBudget owner) => new(new MockLythonHost(), new LythonRunOptions
    { MemoryBudget = owner, MaxExecutionSteps = 10000000, Globals = new Dictionary<string, object?> { ["value"] = "caf\u00e9 \U0001f600" } });
    private static async ValueTask<object?> Run(ExecutableCodeObject code, LoweredExpression? lowered, Context context, bool mode)
    {
        if (lowered is not null) return mode ? await LythonRuntime.EvaluateLoweredExpressionAsync(lowered, context) : LythonRuntime.EvaluateLoweredExpression(lowered, context);
        return mode ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
    }
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    [Theory] [MemberData(nameof(Cases))]
    public async Task Allocation(string shape, bool mode)
    {
        var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(16*1024*1024);using var owner=budget.CreateScope();var context=Prepare(owner);
        var samples=new List<long>();var steps=new List<long>();var committed=new List<long>();
        for(var sample=-2;sample<8;sample++)
        {
            var thread=Environment.CurrentManagedThreadId;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
            var before=GC.GetAllocatedBytesForCurrentThread();object? result=null;
            for(var call=0;call<8;call++){var pending=Run(code,lowered,context,mode);Assert.True(pending.IsCompletedSuccessfully);result=await pending;}
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(thread,Environment.CurrentManagedThreadId);Check(context);
            Assert.Equal(Expected(shape),Assert.IsType<PyString>(result).AsString());
            if(sample>=0){samples.Add(bytes);steps.Add(context.Limits.ExecutionStepCount-work);committed.Add(context.MemoryGovernor.CurrentCommittedBytes-funding);}
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("IDENTITY_WRAPPER_ALLOCATION:"+JsonSerializer.Serialize(new{shape,asynchronous=mode,samples,steps,committed,callsPerSample=8,source=Source(shape),expected=Expected(shape),outputSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Expected(shape))))}));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Boundaries(bool mode)
    {
        var fuelRecords=new List<object>();var admissionRecords=new List<object>();
        foreach(var shape in new[]{"field","percent-a","method","lowered"})
        foreach(var fuel in Enumerable.Range(0,22).Append(128))
        {
            var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(16*1024*1024);using var owner=budget.CreateScope();var context=Prepare(owner);
            context.Limits.ExecutionStepCount=10000000-fuel;object? result=null;LythonRuntimeException? error=null;
            try{result=await Run(code,lowered,context,mode);}catch(LythonRuntimeException ex){error=ex;}
            Check(context);fuelRecords.Add(new{shape,fuel,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-(10000000-fuel)});
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        foreach(var shape in new[]{"field","percent-s","method","mapping"})
        foreach(var headroom in new[]{0,32,64,128,256,512,1024,4096})
        {
            var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(1024*1024);using var owner=budget.CreateScope();var context=Prepare(owner);
            Assert.True(budget.TryReserve(1024*1024-budget.ReservedBytes-headroom,out var padding));
            using(padding)
            {
                object? result=null;LythonRuntimeException? error=null;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
                try{result=await Run(code,lowered,context,mode);}catch(LythonRuntimeException ex){error=ex;Assert.Same(context.MemoryGovernor.AdmissionFailure,error);}
                Check(context);admissionRecords.Add(new{shape,headroom,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-work,committed=context.MemoryGovernor.CurrentCommittedBytes-funding,denied=context.MemoryGovernor.LastDeniedReservationBytes});
            }
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        output.WriteLine("IDENTITY_WRAPPER_BOUNDARIES:"+JsonSerializer.Serialize(new{asynchronous=mode,fuelRecords,admissionRecords}));
    }
    private static object? Describe(LythonRuntimeException? error)=>error is null?null:new{error.ExceptionType,error.Message,error.Span,reason=error.Attribution?.Reason.ToString()};
}
