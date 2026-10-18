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
public sealed class NativeStrDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Shapes = ["int", "wide", "alias", "string", "expanded-control", "float-control", "bytes-control", "codec-control", "zero-control", "custom-control", "shadow-control"];
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var shape in Shapes) foreach (var asynchronous in new[] { false, true }) data.Add(shape, asynchronous);
            return data;
        }
    }
    private static string Source(string shape) => shape switch
    {
        "alias" => "alias=str\nreturn alias(value)",
        "expanded-control" => "return str(*(value,))",
        "codec-control" => "return str(value,'utf-8')",
        "zero-control" => "return str()",
        _ => "return str(value)"
    };
    private static string Expected(string shape) => shape switch
    {
        "wide" => (BigInteger.One << 256).ToString(), "string" => "caf\u00e9 \U0001f600", "float-control" => "1.5",
        "bytes-control" => "b'x'", "codec-control" => "\u00e9", "zero-control" => "", "custom-control" => "custom",
        "shadow-control" => "shadow", _ => "12"
    };
    private static Context Prepare(string shape, ILythonMemoryBudget owner)
    {
        object value = shape switch
        {
            "wide" => BigInteger.One << 256, "string" => "caf\u00e9 \U0001f600", "float-control" => 1.5,
            _ => new BigInteger(12)
        };
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, MaxExecutionSteps = 10000000,
            Globals = new Dictionary<string, object?> { ["value"] = value }
        });
        var setup = shape switch
        {
            "bytes-control" => "global value\nvalue=b'x'",
            "codec-control" => "global value\nvalue=b'\\xc3\\xa9'",
            "custom-control" => "global value\nclass Item:\n def __str__(self):return 'custom'\nvalue=Item()",
            "shadow-control" => "global str\ndef replacement(value):return 'shadow'\nstr=replacement",
            _ => null
        };
        if (setup is not null) _ = LythonRuntime.ExecuteExecutableCodeObject(Compile(setup), context, default, null);
        return context;
    }
    private static ExecutableCodeObject Compile(string source) => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Allocation(string shape, bool asynchronous)
    {
        var source = Source(shape); var code = Compile(source);
        var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope();
        var context = Prepare(shape, owner); var samples = new List<long>(); var steps = new List<long>(); var committed = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId; var work = context.Limits.ExecutionStepCount;
            var funding = context.MemoryGovernor.CurrentCommittedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread(); object? value = null;
            for (var call = 0; call < 8; call++)
            {
                var pending = Run(code, context, asynchronous);
                Assert.True(pending.IsCompletedSuccessfully); value = await pending;
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId); Check(context);
            Assert.Equal(Expected(shape), Assert.IsType<PyString>(value).AsString());
            if (shape == "string") Assert.Same(context.Variables["value"], value);
            if (sample >= 0) { samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); committed.Add(context.MemoryGovernor.CurrentCommittedBytes - funding); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("NATIVE_STR_ALLOCATION:" + JsonSerializer.Serialize(new { shape, asynchronous, samples, steps, committed, callsPerSample = 8, sourceSha256 = Hash(source), outputSha256 = Hash(Expected(shape)) }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Fuel(bool asynchronous)
    {
        var records = new List<object>();
        foreach (var shape in new[] { "int", "wide", "string", "expanded-control", "float-control", "custom-control" })
        foreach (var fuel in Enumerable.Range(0, 17).Append(128))
        {
            var code = Compile(Source(shape));
            var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope();
            var context = Prepare(shape, owner); context.Limits.ExecutionStepCount = 10000000 - fuel;
            object? value = null; LythonRuntimeException? error = null;
            try { value = await Run(code, context, asynchronous); }
            catch (LythonRuntimeException ex) { error = ex; }
            Check(context);
            records.Add(new { shape, fuel, value = value is PyString text ? text.AsString() : null, failure = Failure(error), steps = context.Limits.ExecutionStepCount - (10000000 - fuel) });
            owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        }
        output.WriteLine("NATIVE_STR_FUEL:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Admission(bool asynchronous, bool shared)
    {
        var records = new List<object>();
        foreach (var shape in new[] { "int", "string", "wide" })
        foreach (var headroom in new[] { 128, 512, 4096 })
        {
            var code = Compile("global published\npublished=str(value)\nreturn published");
            var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope();
            var context = Prepare(shape, owner); context.Variables["published"] = new BigInteger(11);
            ILythonMemoryReservation? padding = null;
            if (shared) Assert.True(budget.TryReserve(16 * 1024 * 1024 - budget.ReservedBytes - headroom, out padding));
            else
            {
                var bytes = 1024 * 1024 - context.MemoryGovernor.CurrentAccountedBytes - headroom;
                context.MemoryGovernor.Reserve(bytes, null); context.MemoryGovernor.Commit(bytes);
            }
            object? value = null; LythonRuntimeException? error = null; var before = context.Limits.ExecutionStepCount;
            try { value = await Run(code, context, asynchronous); }
            catch (LythonRuntimeException ex) { error = ex; }
            Check(context);
            records.Add(new { shape, headroom, value = value is PyString text ? text.AsString() : null, published = context.Variables["published"].ToString(), failure = Failure(error), originalRefusal = Failure(context.MemoryGovernor.AdmissionFailure), denied = context.MemoryGovernor.LastDeniedReservationBytes, steps = context.Limits.ExecutionStepCount - before });
            owner.Dispose(); padding?.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        }
        output.WriteLine("NATIVE_STR_ADMISSION:" + JsonSerializer.Serialize(new { asynchronous, shared, records }));
    }
    private static object? Failure(LythonRuntimeException? error) => error is null ? null : new { error.ExceptionType, error.Message, reason = error.Attribution?.Reason.ToString(), error.Span };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
