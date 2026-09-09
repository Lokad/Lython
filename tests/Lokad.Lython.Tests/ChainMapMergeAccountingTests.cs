using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: ChainMap merged views reserve their transient list plus dedup-set peak;
/// the reservation releases when the governed copy takes over.
/// </summary>
public sealed class ChainMapMergeAccountingTests
{
    [Fact]
    public void MergedKeysReserveTransientScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var map = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < 10; i++)
        {
            map.SetItem(new BigInteger(i), new BigInteger(i));
        }
        var chainMap = new PyChainMap(Enumerable.Repeat(map, 2000));
        var member = chainMap.TryGetMember("keys", out var value)
            ? value
            : throw new InvalidOperationException("keys member not found.");
        _ = ((LythonRuntime.ICallable)member).Invoke([], span, context);
        // 1,280,000B merge scratch plus the ten-key governed copy reserved
        // beside it; the copy alone peaks at 320B without the reservation.
        Assert.Equal(1280224L, context.MemoryGovernor.PeakReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
