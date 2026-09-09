using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG15: counted sampling draws Floyd positions through cumulative bounds, so
/// only pools, one bound array and the pick table commit. Reflection reaches
/// the private sampler; renames fail loudly here by design.
/// </summary>
public sealed class SampleCountsAccountingTests
{
    [Fact]
    public void CountedSamplingCommitsExactBacking()
    {
        // Three pools, two picks: counts slots plus cumulative bounds plus
        // the distinct-position table commit exactly once; the result array
        // adopts its own charge at the PyList boundary.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("RandomModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RandomModule not found.");
        var sample = moduleType.GetMethod("SampleCountedPositions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("SampleCountedPositions not found.");
        var state = new PyRandomState();
        var population = new List<object>
        {
            PyString.FromString("a"),
            PyString.FromString("b"),
            PyString.FromString("c"),
        };
        var counts = new List<object> { new BigInteger(0), new BigInteger(5), new BigInteger(0) };

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var result = Assert.IsType<PyList>(
            sample.Invoke(null, [population, counts, 2, state, span, context]));

        Assert.Equal(2, result.Count);
        Assert.Equal("b", Assert.IsType<PyString>(result[0]).AsString());
        Assert.Equal("b", Assert.IsType<PyString>(result[1]).AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // Counts slots plus cumulative bounds plus the position table, plus
        // the small-list backing adopted by the result PyList.
        Assert.Equal(8 * 3 + (24 + (8 * 3)) + (80 + (24 * 2)) + 192, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    [Fact]
    public void PopulationDrainCommitsExactBackingOnce()
    {
        // The shared population/weights drain commits reference slots exactly
        // once up front for sized inputs; payloads stay owned elsewhere.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("RandomModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RandomModule not found.");
        var drain = moduleType.GetMethod("MaterializeSequence", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("MaterializeSequence not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var values = Assert.IsType<List<object>>(drain.Invoke(null, [data, span, context]));

        Assert.Equal(1000, values.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(8000, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
}
