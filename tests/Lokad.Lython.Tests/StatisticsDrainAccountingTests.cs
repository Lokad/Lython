using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// N07: the shared numeric drains reserve caller-scoped scratch as they grow.
/// Sized inputs reserve exactly once up front; lazy inputs reserve each doubling.
/// Nothing commits durably: the reservation releases on every exit path, so the
/// growth math (and denial points) match the old durable commits exactly while
/// discarded calls reclaim. Reflection reaches the private helpers; renames fail
/// loudly here by design.
/// </summary>
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
    public void SizedDrainReservesExactBackingOnce()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<double> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = InvokeDrain(data, context, span, scratch);
            Assert.Equal(1000, values.Count);
            Assert.Equal(8000, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void LazyDrainReservesEachDoubling()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
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
            Assert.Equal(8 * 1024, context.MemoryGovernor.CurrentReservedBytes);
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
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<object> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = InvokeObjectsDrain([data], context, span, scratch);
            Assert.Equal(1000, values.Count);
            Assert.Equal(8000, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ConvertedCopyReservesAlongsideObjects()
    {
        // Median keeps the objects list and the converted doubles list alive
        // together on one shared reservation; both backings stay reserved, not
        // just the first, and both release at scope end.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var convert = moduleType.GetMethod("GetNumericValues", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericValues not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        List<double> values;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            values = Assert.IsType<List<double>>(convert.Invoke(null, [new object[] { data }, "statistics.test", span, context, scratch]));
            Assert.Equal(1000, values.Count);
            Assert.Equal(16000, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }
    [Fact]
    public void FrequencyMapScratchReleasesOnReturn()
    {
        // The mode frequency table, order list and result list are
        // caller-lifetime scratch on a temporary reservation: bounded while
        // held, released on return, so discarded mode/multimode calls reclaim.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var counts = Assert.IsType<List<KeyValuePair<object, int>>>(
            frequency.Invoke(null, [data, context.MemoryGovernor, span, context]));

        Assert.Equal(1000, counts.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    [Fact]
    public void FrequencyMapScratchHonorsBudget()
    {
        // Oversized scratch under a tiny budget denies instead of
        // over-allocating: the temporary reservation is still governed.
        // N12: GetModeCounts now takes the execution context for guest key dispatch.
        var limited = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        var governor = limited.MemoryGovernor;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        var failure = Assert.Throws<TargetInvocationException>(
            () => frequency.Invoke(null, [data, governor, span, limited]));
        var denial = Assert.IsType<LythonRuntimeException>(failure.InnerException);
        Assert.Equal("MemoryError", denial.ExceptionType);
    }
    [Fact]
    public void EmptyFrequencyMapChargesNothing()
    {
        // Multimode over empty input allocates nothing, so it stays free even
        // under a zero budget.
        var emptyContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var governor = emptyContext.MemoryGovernor;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");

        var counts = Assert.IsType<List<KeyValuePair<object, int>>>(
            frequency.Invoke(null, [new List<object>(), governor, span, emptyContext]));

        Assert.Empty(counts);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
