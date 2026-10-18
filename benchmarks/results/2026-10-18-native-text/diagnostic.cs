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
public sealed class NativeTextDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Compare(bool asynchronous, bool numeric)
    {
        const int comparisons = 2048;
        var source = "count=0\nfor left in values:\n if left<right:count+=1\nreturn count";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        context.Variables["values"] = new PyList(Enumerable.Range(0, comparisons).Select(i => numeric ? (object)new BigInteger(i)
            : PyString.FromString("café" + i, context.MemoryGovernor, null)), context.MemoryGovernor, null);
        context.Variables["right"] = numeric ? new BigInteger(comparisons) : PyString.FromString("😀", context.MemoryGovernor, null);
        var samples = new List<object>();
        for (var sample = -4; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var pending = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.True(pending.IsCompletedSuccessfully);
            var result = await pending;
            var ticks = Stopwatch.GetTimestamp() - started;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Assert.Equal(new BigInteger(comparisons), Assert.IsType<BigInteger>(result));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (sample >= 0) samples.Add(new { ticks, allocated });
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("NATIVE_TEXT:" + JsonSerializer.Serialize(new { asynchronous, numeric, comparisons, frequency = Stopwatch.Frequency, samples }));
    }
}
