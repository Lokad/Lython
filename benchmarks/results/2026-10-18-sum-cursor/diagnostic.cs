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
public sealed class SumCursorDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<string, bool> Routes
    {
        get
        {
            var cases = new TheoryData<string, bool>();
            foreach (var route in new[] { "list0", "list65", "tuple0", "tuple65", "range65", "generator65", "custom65", "bytes65" })
            foreach (var asynchronous in new[] { false, true }) cases.Add(route, asynchronous);
            return cases;
        }
    }
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Allocation(string route, bool asynchronous)
    {
        var code = Compile(Source(route));
        var budget = new LythonMemoryBudget(64 * 1024 * 1024); using var owner = budget.CreateScope();
        var context = await Prepare(owner, route, asynchronous);
        var samples = new List<long>(); var steps = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var work = context.Limits.ExecutionStepCount; var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread(); object? result = null;
            for (var call = 0; call < 8; call++)
            {
                var pending = Run(code, context, asynchronous); Assert.True(pending.IsCompletedSuccessfully);
                result = await pending;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            if (route == "custom65" || route.EndsWith('0')) Assert.Same(context.Variables["start"], result);
            else Assert.Equal(new BigInteger(2080), result);
            Check(context);
            if (sample >= 0) { samples.Add(allocated); steps.Add(context.Limits.ExecutionStepCount - work); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("SUM_CURSOR_ALLOCATION:" + JsonSerializer.Serialize(new { route, asynchronous, callsPerSample = 8, samples, steps }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Boundaries(bool asynchronous)
    {
        var records = new List<object>();
        foreach (var route in new[] { "list0", "list65", "tuple65", "range65", "generator65", "custom65", "bad-item" })
        foreach (var fuel in new[] { 1, 4, 8, 12, 16, 32, 64, 128, 512 })
        {
            var budget = new LythonMemoryBudget(); using var owner = budget.CreateScope();
            var context = await Prepare(owner, route, asynchronous);
            context.Limits.ExecutionStepCount = 10000000 - fuel;
            object? value = null; string? exception = null, message = null, reason = null; LythonSourceSpan? span = null;
            try { value = await Run(Compile(Source(route)), context, asynchronous); }
            catch (LythonRuntimeException error) { exception = error.ExceptionType; message = error.Message; reason = error.Attribution?.Reason.ToString(); span = error.Span; }
            var identity = ReferenceEquals(context.Variables["start"], value);
            var result = route == "custom65" ? null : value?.ToString();
            Check(context); owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { route, fuel, result, identity, exception, message, reason, span, steps = context.Limits.ExecutionStepCount - (10000000 - fuel) });
        }
        output.WriteLine("SUM_CURSOR_BOUNDARIES:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    private static async ValueTask<Context> Prepare(ILythonMemoryBudget owner, string route, bool asynchronous)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000 });
        var numbers = Enumerable.Range(0, route.EndsWith('0') ? 0 : 65).Select(i => (object)new BigInteger(i)).ToArray();
        object source = route.StartsWith("tuple") ? new PyTuple(numbers, context.MemoryGovernor, null)
            : route == "bytes65" ? new PyBytes(Enumerable.Range(0, 65).Select(i => (byte)i).ToArray())
            : new PyList(numbers, context.MemoryGovernor, null);
        if (route == "range65") source = await Run(Compile("return range(65)"), context, asynchronous) ?? throw new InvalidOperationException();
        if (route == "bad-item") source = new PyList([BigInteger.One, PyString.FromString("bad", context.MemoryGovernor, null)], context.MemoryGovernor, null);
        context.Variables["source"] = source;
        context.Variables["start"] = BigInteger.Zero;
        if (route == "custom65") _ = await Run(Compile("global start\nclass Total:\n def __add__(self,item):return self\nstart=Total()"), context, asynchronous);
        return context;
    }
    private static string Source(string route) => route == "generator65" ? "return sum((item for item in source),start)" : "return sum(source,start)";
    private static ExecutableCodeObject Compile(string source) => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
