using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG15: the shared numeric drain commits its backing as it grows. Sized
/// inputs pay exactly once up front; lazy inputs pay each doubling. The
/// doubles list has no governed adopter, so the charges stay committed like
/// string payloads. Reflection reaches the private helper; renames fail
/// loudly here by design.
/// </summary>
public sealed class StatisticsDrainAccountingTests
{
    private static List<double> InvokeDrain(object data, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var drain = moduleType.GetMethod("GetNumericValuesFromIterable", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericValuesFromIterable not found.");
        return Assert.IsType<List<double>>(drain.Invoke(null, [data, "statistics.test", span, context]));
    }

    [Fact]
    public void SizedDrainCommitsExactBackingOnce()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var values = InvokeDrain(data, context, span);

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(8000, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }

    [Fact]
    public void LazyDrainCommitsEachDoubling()
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
        var values = InvokeDrain(Lazy(), context, span);

        // List<double> doubles from an initial four; incremental deltas sum to
        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // exactly the final backing size: (4 - 0) + (8 - 4) + ... + (1024 - 512).
        Assert.Equal(8 * 1024, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    private static List<object> InvokeObjectsDrain(object[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var drain = moduleType.GetMethod("GetNumericObjects", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericObjects not found.");
        return Assert.IsType<List<object>>(drain.Invoke(null, [arguments, "statistics.test", span, context]));
    }

    [Fact]
    public void ObjectsDrainCommitsExactBackingOnce()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var values = InvokeObjectsDrain([data], context, span);

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(8000, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }

    [Fact]
    public void ConvertedCopyCommitsAlongsideObjects()
    {
        // Median keeps the objects list and the converted doubles list alive
        // together; both backings stay charged, not just the first.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var convert = moduleType.GetMethod("GetNumericValues", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetNumericValues not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)(double)i).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var values = Assert.IsType<List<double>>(convert.Invoke(null, [new object[] { data }, "statistics.test", span, context]));

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(16000, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    [Fact]
    public void FrequencyMapCommitsExactBackingOnce()
    {
        // The mode frequency table, order list and result list are pre-sized
        // to the input count (distinct keys never exceed it), so the whole
        // structure commits exactly once with no growth.
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
            frequency.Invoke(null, [data, context.MemoryGovernor, span]));

        Assert.Equal(1000, counts.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(96 + (32 * 1000) + 48 + (24 * 1000), context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }

    [Fact]
    public void EmptyFrequencyMapChargesNothing()
    {
        // Multimode over empty input allocates nothing, so it stays free even
        // under a zero budget.
        var governor = new MemoryGovernor(0);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var frequency = moduleType.GetMethod("GetModeCounts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetModeCounts not found.");

        var counts = Assert.IsType<List<KeyValuePair<object, int>>>(
            frequency.Invoke(null, [new List<object>(), governor, span]));

        Assert.Empty(counts);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
