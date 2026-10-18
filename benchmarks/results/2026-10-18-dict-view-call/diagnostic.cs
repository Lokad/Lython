using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class DictionaryViewCallDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<string, string, bool> Routes
    {
        get
        {
            var result = new TheoryData<string, string, bool>();
            foreach (var method in new[] { "items", "keys", "values" })
            foreach (var route in new[] { "distinct", "repeated", "exposed", "expanded", "custom" })
            foreach (var asynchronous in new[] { false, true }) result.Add(method, route, asynchronous);
            return result;
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Allocation(string method, string route, bool asynchronous)
    {
        var code = Compile("total=0\nfor d in rows:total+=len(" + (route == "exposed" ? "d()" : $"d.{method}({(route == "expanded" ? "*()" : "")})") + ")\nreturn total");
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10_000_000 });
        var rows = new PyList([], context.MemoryGovernor);
        var first = new PyDict(context.MemoryGovernor);
        first.SetItem(BigInteger.One, BigInteger.One);
        if (route == "custom")
            _ = await Run(Compile($"class C:\n def {method}(self):return [1]\nc=C()"), context, false);
        for (var i = 0; i < 64; i++)
        {
            var dict = route == "repeated" ? first : new PyDict(context.MemoryGovernor);
            if (route != "repeated") dict.SetItem(BigInteger.One, BigInteger.One);
            context.Variables["d"] = dict;
            rows.Add(route == "exposed" ? LythonRuntime.ExecuteExecutableCodeObject(Compile($"return d.{method}"), context, default, null)!
                : route == "custom" ? context.Variables["c"] : dict);
        }
        context.Variables["rows"] = rows;
        var samples = new List<long>();
        var steps = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var work = context.Limits.ExecutionStepCount;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var call = 0; call < 8; call++)
            {
                var pending = Run(code, context, asynchronous);
                Assert.True(pending.IsCompletedSuccessfully);
                Assert.Equal(new BigInteger(64), await pending);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Check(context);
            if (sample >= 0) { samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("DICT_VIEW_ALLOCATION:" + JsonSerializer.Serialize(new { method, route, asynchronous, callsPerSample = 8, viewCallsPerInvocation = 64, samples, steps }));
    }

    [Theory]
    [InlineData("items", false)] [InlineData("items", true)]
    [InlineData("keys", false)] [InlineData("keys", true)]
    [InlineData("values", false)] [InlineData("values", true)]
    public async Task Boundaries(string method, bool asynchronous)
    {
        var records = new List<object>();
        foreach (var route in new[] { "direct", "expanded", "exposed", "wrong-arity", "keyword", "custom" })
        for (var fuel = 1; fuel <= 16; fuel++)
        {
            var budget = new LythonMemoryBudget(); using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000 });
            var dict = new PyDict(context.MemoryGovernor); dict.SetItem(BigInteger.One, BigInteger.One);
            context.Variables["d"] = dict;
            context.Variables["method"] = LythonRuntime.ExecuteExecutableCodeObject(Compile($"return d.{method}"), context, default, null)!;
            if (route == "custom") await Run(Compile($"class C:\n def {method}(self):return [1]\nd=C()"), context, asynchronous);
            context.Limits.ExecutionStepCount = 10000 - fuel;
            var source = route switch
            {
                "expanded" => $"return d.{method}(*())",
                "exposed" => "return method()",
                "wrong-arity" => $"return d.{method}(1)",
                "keyword" => $"return d.{method}(value=1)",
                _ => $"return d.{method}()"
            };
            string? result = null, exception = null, message = null, reason = null;
            LythonSourceSpan? span = null;
            try { result = (await Run(Compile(source), context, asynchronous))?.GetType().Name; }
            catch (LythonRuntimeException error) { exception = error.ExceptionType; message = error.Message; reason = error.Attribution?.Reason.ToString(); span = error.Span; }
            Check(context); owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { route, fuel, result, exception, message, reason, span, steps = context.Limits.ExecutionStepCount - (10000 - fuel) });
        }
        output.WriteLine("DICT_VIEW_BOUNDARIES:" + JsonSerializer.Serialize(new { method, asynchronous, records }));
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
