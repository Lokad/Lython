using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG15: statistics.mean streams its input instead of materializing parallel
/// object/double lists. Allocation must not scale with the input (R42-style
/// differential windows); prebuilt values behind a lazy adapter exclude
/// per-pull source garbage from the measurement. Reflection reaches the
/// private builtin; renames fail loudly here by design.
/// </summary>
public sealed class StatisticsStreamingTests
{
    private static object InvokeMean(IEnumerable<object> data, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var moduleType = typeof(LythonRuntime).GetNestedType("StatisticsModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StatisticsModule not found.");
        var mean = moduleType.GetMethod("Mean", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Mean not found.");
        return mean.Invoke(null, [new object[] { data }, span, context])
            ?? throw new InvalidOperationException("Mean returned null.");
    }

    private static IEnumerable<object> Cycle(object[] palette, int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return palette[i & 1023];
        }
    }

    private static double ExpectedMean(int count)
    {
        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            total += (double)(i & 1023);
        }

        return total / count;
    }

    [Fact]
    public void MeanAllocationDoesNotScaleWithInput()
    {
        // 200K doubles need ~1.6MB per materialized list; the 2K control
        // needs ~32KB. If mean materialized, the windows would differ by megabytes.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var palette = Enumerable.Range(0, 1024).Select(static i => (object)(double)i).ToArray();
        for (var i = 0; i < 5; i++)
        {
            InvokeMean(Cycle(palette, 2000), span, context);
            InvokeMean(Cycle(palette, 200000), span, context);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var beforeSmall = GC.GetAllocatedBytesForCurrentThread();
        var smallResult = InvokeMean(Cycle(palette, 2000), span, context);
        var smallAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSmall;
        var beforeBig = GC.GetAllocatedBytesForCurrentThread();
        var bigResult = InvokeMean(Cycle(palette, 200000), span, context);
        var bigAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeBig;

        Assert.Equal(ExpectedMean(2000), Assert.IsType<double>(smallResult));
        Assert.Equal(ExpectedMean(200000), Assert.IsType<double>(bigResult));
        var delta = bigAllocated >= smallAllocated ? bigAllocated - smallAllocated : 0;
        Assert.True(delta < 1048576, $"mean allocation scales with input: small={smallAllocated}, big={bigAllocated}");
    }
}
