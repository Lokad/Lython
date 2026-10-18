using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class GenericFunctionComparisonDiagnostic(ITestOutputHelper output)
{
    private static string Source(bool generic) => "def add" + (generic ? "[T]" : "")
        + "(value:int=0)->int:return value+1\ntotal=0\nfor value in range(128):total+=add(value)\nreturn total";

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task NativeWarm(bool generic, bool asynchronous)
    {
        var syntax = LythonFrontend.Compile(Source(generic));
        Assert.NotNull(syntax.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax.Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        var host = new MockLythonHost();
        var allocated = new List<long>();
        var elapsed = new List<double>();
        var steps = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            long bytes = 0, ticks = 0, work = 0;
            for (var call = 0; call < 8; call++)
            {
                using var owner = budget.CreateScope();
                var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 1000000 });
                var before = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                object? value;
                if (asynchronous)
                {
                    var operation = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                    Assert.True(operation.IsCompletedSuccessfully);
                    value = await operation;
                }
                else value = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                ticks += Stopwatch.GetTimestamp() - started;
                bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(new BigInteger(8256), value);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                work += context.Limits.ExecutionStepCount;
                owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
            }
            if (sample >= 0)
            {
                allocated.Add(bytes); elapsed.Add(1000.0 * ticks / Stopwatch.Frequency); steps.Add(work);
            }
        }
        output.WriteLine("GENERIC_FUNCTION_COMPARISON:" + JsonSerializer.Serialize(new
        { boundary = "native-warm", generic, asynchronous, callsPerSample = 8, guestCallsPerInvocation = 128, result = 8256, allocated, elapsedMilliseconds = elapsed, executionSteps = steps }));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task FreshCompileToResult(bool generic, bool asynchronous)
    {
        var source = Source(generic);
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        var engine = new LythonEngine();
        var host = new MockLythonHost();
        var elapsed = new List<double>();
        for (var sample = -2; sample < 8; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var call = 0; call < 8; call++)
            {
                using (var script = engine.Compile(source, budget))
                {
                    Assert.True(script.IsValid);
                    using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(8256), result.ReturnValue);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            var milliseconds = 1000.0 * (Stopwatch.GetTimestamp() - started) / Stopwatch.Frequency;
            if (sample >= 0) elapsed.Add(milliseconds);
        }
        output.WriteLine("GENERIC_FUNCTION_COMPARISON:" + JsonSerializer.Serialize(new
        { boundary = "fresh-compile-result", generic, asynchronous, callsPerSample = 8, guestCallsPerInvocation = 128, result = 8256, elapsedMilliseconds = elapsed }));
    }
}
