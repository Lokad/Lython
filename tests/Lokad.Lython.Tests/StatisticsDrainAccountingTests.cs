using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Statistics scratch remains funded for its consuming caller, with admission
// before capacity allocation and cleanup after the last use.
[Collection("AllocationSensitive")]
public sealed class StatisticsDrainAccountingTests
{
    private static List<double> InvokeDrain(object data, LythonRuntime.ExecutionContext context, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var drain = moduleType.GetMethod("GetNumericValuesFromIterable", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericValuesFromIterable not found.");
        return Assert.IsType<List<double>>(drain.Invoke(null, [data, "statistics.test", span, context, scratch]));
    }

    [Fact]
    public void SizedDrainFundsBackingUntilCallerFinishes()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<double> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = InvokeDrain(data, context, span, scratch);
            Assert.Equal(1000, values.Count);
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 8000);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void LazyDrainFundsCurrentBackingUntilCallerFinishes()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var palette = Enumerable.Range(0, 1024).Select(static i => (object)(double)i).ToArray();
        IEnumerable<object> Lazy()
        {
            for (var i = 0; i < 1000; i++)
            {
                yield return palette[i & 1023];
            }
        }

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<double> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = InvokeDrain(Lazy(), context, span, scratch);
            Assert.Equal(1000, values.Count);
            // List<double> doubles from an initial four; incremental deltas sum to
            // exactly the final backing size: (4 - 0) + (8 - 4) + ... + (1024 - 512).
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 8 * 1024);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }
    private static List<object> InvokeObjectsDrain(object[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch)
    {
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var drain = moduleType.GetMethod("GetNumericObjects", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericObjects not found.");
        return Assert.IsType<List<object>>(drain.Invoke(null, [arguments, "statistics.test", span, context, scratch]));
    }

    [Fact]
    public void ObjectsDrainReservesExactBackingOnce()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<object> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = InvokeObjectsDrain([data], context, span, scratch);
            Assert.Equal(1000, values.Count);
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 8000);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void FrequencyResultRemainsFundedUntilCallerFinishes()
    {
        // Returned frequency results must remain charged until selection/output
        // construction finishes, rather than refunding at the helper boundary.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            var counts = Assert.IsType<List<KeyValuePair<object, int>>>(
                frequency.Invoke(null, [data, scratch, span, context]));
            Assert.Equal(1000, counts.Count);
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 16L * counts.Count);
            Assert.Equal(data[0], counts[0].Key);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    [Fact]
    public void FrequencyMapScratchHonorsBudget()
    {
        // Oversized scratch under a tiny budget denies instead of
        // over-allocating: the temporary reservation is still governed.
        // N12: GetModeCounts now takes the execution context for guest key dispatch.
        var limited = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024) },regexProvider:Utf8RegexProvider.Instance);
        var governor = limited.MemoryGovernor;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        using var scratch = governor.ReserveTemporary(0, span);
        var failure = Assert.Throws<TargetInvocationException>(
            () => frequency.Invoke(null, [data, scratch, span, limited]));
        var denial = Assert.IsType<LythonRuntimeException>(failure.InnerException);
        Assert.Equal("MemoryError", denial.ExceptionType);
    }
    [Fact]
    public void EmptyFrequencyMapChargesNothing()
    {
        // Multimode over empty input allocates nothing, so it stays free even
        // under a zero budget.
        var emptyContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var governor = emptyContext.MemoryGovernor;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");

        using var scratch = governor.ReserveTemporary(0, span);
        var counts = Assert.IsType<List<KeyValuePair<object, int>>>(
            frequency.Invoke(null, [new List<object>(), scratch, span, emptyContext]));

        Assert.Empty(counts);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PairedBuffersRemainFundedForConsumer(bool asynchronous)
    {
        var budget = new LythonMemoryBudget();
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 64).Select(i => (object)(double)i).ToList();
        var module = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)!;
        var method = module.GetMethod(asynchronous ? "GetPairedNumericValuesAsync" : "GetPairedNumericValues", BindingFlags.NonPublic | BindingFlags.Static)!;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            var pair = method.Invoke(null, [new object[] { data, data }, "statistics.test", span, context, scratch])!;
            if (asynchronous)
            {
                var task = (Task)pair.GetType().GetMethod("AsTask")!.Invoke(pair, null)!;
                await task;
                pair = task.GetType().GetProperty("Result")!.GetValue(task)!;
            }
            var x = Assert.IsType<List<double>>(pair.GetType().GetProperty("X")!.GetValue(pair));
            var y = Assert.IsType<List<double>>(pair.GetType().GetProperty("Y")!.GetValue(pair));
            Assert.True(budget.ReservedBytes >= 8L * (x.Capacity + y.Capacity));
            Assert.Equal(126.0, x[^1] + y[^1]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SizedDrainDeniesBeforeAllocatingBacking(bool objects)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Repeat((object)1.0, 65536).ToList();
        for (var pass = 0; pass < 2; pass++)
        {
            var budget = new LythonMemoryBudget(32768);
            using var owner = budget.CreateScope();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
            {
                var error = Assert.Throws<TargetInvocationException>(() =>
                {
                    if (objects) InvokeObjectsDrain([data], context, span, scratch);
                    else InvokeDrain(data, context, span, scratch);
                });
                Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
            }
#if !DEBUG
            if (pass == 1) Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 128000);
#endif
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
