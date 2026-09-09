using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: the ChainMap maps attribute list owns its backing through the visible
/// map governor; fully ungoverned maps stay free.
/// </summary>
public sealed class ChainMapTablesAccountingTests
{
    private static PyList ReadMaps(PyChainMap chainMap)
    {
        var member = chainMap.TryGetMember("maps", out var value)
            ? value
            : throw new InvalidOperationException("maps member not found.");
        return (PyList)member;
    }

    [Fact]
    public void MapsAttributeCommitsTableBacking()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var map = new PyDict(context.MemoryGovernor, span);
        map.SetItem(new BigInteger(0), new BigInteger(0));
        var chainMap = new PyChainMap(Enumerable.Repeat(map, 20000));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var maps = ReadMaps(chainMap);
        Assert.Equal(20000, maps.Count);
        Assert.Same(context.MemoryGovernor, maps.OwnerMemoryGovernor);
        Assert.Equal(320064L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedMapsAttributeStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var chainMap = new PyChainMap(Enumerable.Repeat(new PyDict(), 100));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var maps = ReadMaps(chainMap);
        Assert.Equal(100, maps.Count);
        Assert.Equal(0L, context.MemoryGovernor.CurrentCommittedBytes - before);
    }
}
