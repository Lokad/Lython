using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JoinDrainDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Allocation(bool asynchronous, bool generator)
    {
        var source = generator ? "return '|'.join(x for x in parts)" : "return '|'.join(parts)";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10_000_000 });
        var scalar = PyString.FromString("café😀", context.MemoryGovernor, null);
        context.Variables["parts"] = new PyList(Enumerable.Repeat<object>(scalar, 16), context.MemoryGovernor, null);
        var samples = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread();
            object? result = null;
            for (var call = 0; call < 64; call++)
            {
                var pending = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                Assert.True(pending.IsCompletedSuccessfully);
                result = await pending;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Assert.Equal(string.Join("|", Enumerable.Repeat("café😀", 16)), Assert.IsType<PyString>(result).AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Null(context.CurrentExecutableFrame);
            if (sample >= 0) samples.Add(allocated);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("JOIN_DRAIN_ALLOCATION:" + JsonSerializer.Serialize(new { asynchronous, generator, callsPerSample = 64, samples }));
    }
}
