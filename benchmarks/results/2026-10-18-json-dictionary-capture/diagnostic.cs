using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JsonDictionaryCaptureDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<string, bool> Routes
    {
        get
        {
            var result = new TheoryData<string, bool>();
            foreach (var route in new[] { "scalar", "sequence", "dictionaries", "sorted", "default", "encoder", "incremental-control", "loads-control" })
            foreach (var asynchronous in new[] { false, true }) result.Add(route, asynchronous);
            return result;
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Allocation(string route, bool asynchronous)
    {
        var source = "import json\nreturn " + (route switch
        {
            "sorted" => "json.dumps(value,sort_keys=True)",
            "default" => "json.dumps(value,default=replacement)",
            "encoder" => "json.JSONEncoder().encode(value)",
            "incremental-control" => "''.join(json.JSONEncoder().iterencode(value))",
            "loads-control" => "len(json.loads(value))",
            _ => "json.dumps(value)"
        });
        var code = Compile(source);
        var budget = new LythonMemoryBudget(64 * 1024 * 1024); using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10_000_000 });
        var setup = route switch
        {
            "scalar" => "value=7",
            "sequence" => "value=list(range(64))",
            "default" => "value=[{i} for i in range(64)]\ndef replacement(value):return [1]",
            "loads-control" => "value='[0,1,2,3]'",
            _ => "value=[{'z':i,'a':i+1} for i in range(64)]"
        };
        _ = await Run(Compile("global value,replacement\n" + setup), context, asynchronous);
        var samples = new List<long>(); var steps = new List<long>();
        string? resultHash = null;
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var work = context.Limits.ExecutionStepCount;
            var before = GC.GetAllocatedBytesForCurrentThread();
            object? value = null;
            for (var call = 0; call < 8; call++)
            {
                var pending = Run(code, context, asynchronous); Assert.True(pending.IsCompletedSuccessfully);
                value = await pending;
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            if (route == "loads-control") Assert.Equal(new BigInteger(4), value);
            else
            {
                var text = Assert.IsType<PyString>(value);
                Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
                var hash = Convert.ToHexString(SHA256.HashData(text.Utf8Bytes.Span)).ToLowerInvariant();
                if (resultHash is null) resultHash = hash;
                else Assert.Equal(resultHash, hash);
            }
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (sample >= 0) { samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("JSON_CAPTURE_ALLOCATION:" + JsonSerializer.Serialize(new { route, asynchronous, callsPerSample = 8, elements = 64, resultHash, samples, steps }));
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
}
