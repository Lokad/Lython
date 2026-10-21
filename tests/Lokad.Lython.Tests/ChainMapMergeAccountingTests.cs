using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: ChainMap merged views are live shells over the maps: keys() commits a
/// small shell charge with no merge scratch, and later writes stay visible.
/// </summary>
public sealed class ChainMapMergeAccountingTests
{
    [Fact]
    public void MergedKeysReturnLiveViewsWithoutMergeScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var map = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < 10; i++)
        {
            map.SetItem(new BigInteger(i), new BigInteger(i));
        }
        var chainMap = new PyChainMap(Enumerable.Repeat(map, 2));
        var member = chainMap.TryGetMember("keys", out var value)
            ? value
            : throw new InvalidOperationException("keys member not found.");
        var view = Assert.IsType<ChainMapKeysView>(((LythonRuntime.ICallable)member).Invoke([], span, context));
        // The view borrows the maps instead of copying them: later writes
        // stay visible through the same view object.
        map.SetItem(new BigInteger(10), new BigInteger(10));
        Assert.Equal(11, view.Count);
        // Creating a view does not merge; its later reads observe current maps.
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
