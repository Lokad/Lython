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
public sealed class NativeFieldDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Shapes = ["text", "integer", "repr", "ascii", "width", "lowered", "custom-format", "custom-str", "custom-repr", "descriptor", "bad-native", "bad-instance"];
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var rows = new TheoryData<string, bool>();
            foreach (var shape in Shapes) foreach (var mode in new[] { false, true }) rows.Add(shape, mode);
            return rows;
        }
    }
    private static string Source(string shape) => shape switch
    {
        "repr" or "lowered" or "custom-repr" => "return f'{value!r}'", "ascii" => "return f'{value!a}'",
        "custom-str" => "return f'{value!s}'", "width" => "return f'{value:>12}'", "bad-native" => "return f'{value:Q}'", _ => "return f'{value}'"
    };
    private static string Expected(string shape) => shape switch
    {
        "integer" => "42", "repr" or "lowered" => "'café 😀'", "ascii" => "'caf\\xe9 \\U0001f600'",
        "width" => "      café 😀", "custom-format" or "custom-str" or "custom-repr" or "descriptor" => "custom", _ => "café 😀"
    };
    private static LoweredScript Lower(string source) => LoweredScript.Lower(LythonFrontend.Compile(source).Script!);
    private static (ExecutableCodeObject Code, LoweredExpression? Lowered) Compile(string shape)
    {
        var lowered = Lower(Source(shape));
        return (ExecutableScript.Compile(lowered).EntryPoint, shape == "lowered" ? Assert.IsType<LoweredReturnStatement>(Assert.Single(lowered.Statements)).Expression : null);
    }
    private static Context Prepare(string shape, ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000,
            Globals = new Dictionary<string, object?> { ["value"] = shape == "integer" ? new BigInteger(42) : "café 😀" } });
        var setup = shape switch
        {
            "custom-format" => "class Value:\n def __format__(self,spec):return 'custom'\nvalue=Value()",
            "custom-str" => "class Value:\n def __str__(self):return 'custom'\nvalue=Value()",
            "custom-repr" => "class Value:\n def __repr__(self):return 'custom'\nvalue=Value()",
            "descriptor" => "class Descriptor:\n def __get__(self,instance,owner):return lambda spec:'custom'\nclass Value:__format__=Descriptor()\nvalue=Value()",
            "bad-instance" => "class Value:\n def __format__(self,spec):raise ValueError('format')\nvalue=Value()", _ => null
        };
        if (setup is not null) _ = LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(Lower("global value\n"+setup)).EntryPoint, context, default, null);
        return context;
    }
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
        var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(16*1024*1024);using var owner=budget.CreateScope();var context=Prepare(shape,owner);
        var samples=new List<long>();var steps=new List<long>();var committed=new List<long>();object? failure=null;
        for(var sample=-2;sample<8;sample++)
        {
            var thread=Environment.CurrentManagedThreadId;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
            var before=GC.GetAllocatedBytesForCurrentThread();object? result=null;LythonRuntimeException? error=null;
            for(var call=0;call<8;call++)
            {
                try {var pending=Run(code,lowered,context,mode);Assert.True(pending.IsCompleted);result=await pending;}
                catch(LythonRuntimeException ex){error=ex;}
            }
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(thread,Environment.CurrentManagedThreadId);Check(context);
            if(shape.StartsWith("bad-",StringComparison.Ordinal)){Assert.NotNull(error);Assert.Equal("ValueError",error.ExceptionType);failure=Describe(error);}
            else {Assert.Null(error);Assert.Equal(Expected(shape),Assert.IsType<PyString>(result).AsString());}
            if(sample>=0){samples.Add(bytes);steps.Add(context.Limits.ExecutionStepCount-work);committed.Add(context.MemoryGovernor.CurrentCommittedBytes-funding);}
        }
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        output.WriteLine("NATIVE_FIELD_ALLOCATION:"+JsonSerializer.Serialize(new{shape,asynchronous=mode,samples,steps,committed,callsPerSample=8,source=Source(shape),expected=shape.StartsWith("bad-",StringComparison.Ordinal)?null:Expected(shape),failure}));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Boundaries(bool mode)
    {
        var fuelRecords=new List<object>();var admissionRecords=new List<object>();
        foreach(var shape in new[]{"text","width","custom-format","descriptor","bad-native","bad-instance"})
        foreach(var fuel in Enumerable.Range(0,33).Append(256))
        {
            var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(16*1024*1024);using var owner=budget.CreateScope();var context=Prepare(shape,owner);
            context.Limits.ExecutionStepCount=10000000-fuel;object? result=null;LythonRuntimeException? error=null;
            try{result=await Run(code,lowered,context,mode);}catch(LythonRuntimeException ex){error=ex;}
            Check(context);fuelRecords.Add(new{shape,fuel,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-(10000000-fuel)});
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        foreach(var shape in new[]{"text","width","custom-format","bad-native"})
        foreach(var headroom in new[]{0,32,64,128,256,512,1024,4096})
        {
            var (code,lowered)=Compile(shape);var budget=new LythonMemoryBudget(1024*1024);using var owner=budget.CreateScope();var context=Prepare(shape,owner);
            Assert.True(budget.TryReserve(1024*1024-budget.ReservedBytes-headroom,out var padding));
            using(padding)
            {
                object? result=null;LythonRuntimeException? error=null;var work=context.Limits.ExecutionStepCount;var funding=context.MemoryGovernor.CurrentCommittedBytes;
                try{result=await Run(code,lowered,context,mode);}catch(LythonRuntimeException ex){error=ex;if(error.Attribution?.Reason==LythonFailureReason.SharedMemoryAllowance)Assert.Same(context.MemoryGovernor.AdmissionFailure,error);}
                Check(context);admissionRecords.Add(new{shape,headroom,value=(result as PyString)?.AsString(),failure=Describe(error),steps=context.Limits.ExecutionStepCount-work,committed=context.MemoryGovernor.CurrentCommittedBytes-funding,denied=context.MemoryGovernor.LastDeniedReservationBytes});
            }
            owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
        }
        output.WriteLine("NATIVE_FIELD_BOUNDARIES:"+JsonSerializer.Serialize(new{asynchronous=mode,fuelRecords,admissionRecords}));
    }
    private static object? Describe(LythonRuntimeException? error)=>error is null?null:new{error.ExceptionType,error.Message,error.Span,reason=error.Attribution?.Reason.ToString()};
}
