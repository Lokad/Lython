using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG15: counted sampling draws Floyd positions through cumulative bounds, so
/// temporary storage releases after selection; retained results stay funded.
/// </summary>
public sealed class SampleCountsAccountingTests
{
    [Fact]
    public void CountedSamplingCommitsExactBacking()
    {
        // Three pools, two picks: counts slots, cumulative bounds and the
        // distinct-position table reserve as caller-scoped scratch and release
        // on return, so only the result array stays committed, adopted at the
        // PyList boundary. Pools arrive by index with no drain.
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var module = new LythonRuntime.RandomModule(new PyRandomState());
        var population = new PyList([PyString.FromString("a"), PyString.FromString("b"), PyString.FromString("c")], context.MemoryGovernor, span);
        var counts = new PyList([BigInteger.Zero, new BigInteger(5), BigInteger.Zero], context.MemoryGovernor, span);
        Assert.True(module.TryGetMember("sample", out var member));
        var sample = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var result = Assert.IsType<PyList>(sample.Invoke([CallArgumentValue.Positional(population),
            CallArgumentValue.Positional(new BigInteger(2)), CallArgumentValue.Keyword("counts", counts)], span, context));

        Assert.Equal(2, result.Count);
        Assert.Equal("b", Assert.IsType<PyString>(result[0]).AsString());
        Assert.Equal("b", Assert.IsType<PyString>(result[1]).AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // Only the small-list backing adopted by the result PyList stays
        // committed; the counts, cumulative and position-table scratch released.
        Assert.Equal(result.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
    [Fact]
    public void PopulationDrainKeepsBackingFundedUntilCallerDisposes()
    {
        // Keep the same thousand-value input while checking the caller lifetime.
        // Drained backing is reserved scratch, not a durable unowned commit.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("RandomModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RandomModule not found.");
        var drain = moduleType.GetMethod("MaterializeSequence", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("MaterializeSequence not found.");
        var data = Enumerable.Range(0, 1000).Select(static i => (object)new BigInteger(i)).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        using (var scratch = context.MemoryGovernor.ReserveTemporary(0, span))
        {
            var values = Assert.IsType<List<object>>(drain.Invoke(null, [data, span, context, scratch]));
            Assert.Equal(1000, values.Count);
            Assert.Same(data[0], values[0]);
            Assert.Equal(8064, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
    }
}
