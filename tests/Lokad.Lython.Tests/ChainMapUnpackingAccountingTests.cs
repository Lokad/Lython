using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: ChainMap dict-unpacking and len() reserve the transient merge peak;
/// the reservation releases once the merged keys are built, beside any
/// governed destination.
/// </summary>
public sealed class ChainMapUnpackingAccountingTests
{
    private static PyChainMap SharedChainMap(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var map = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < 10; i++)
        {
            map.SetItem(new BigInteger(i), new BigInteger(i));
        }
        return new PyChainMap(Enumerable.Repeat(map, 2000));
    }

    [Fact]
    public void UnpackingReservesTransientScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var chainMap = SharedChainMap(context, span);
        var method = typeof(LythonRuntime).GetMethod("EnumerateMappingItems", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("EnumerateMappingItems not found.");
        var pairs = (IEnumerable<KeyValuePair<object, object>>)method.Invoke(null, [chainMap, context, span])!;
        Assert.Equal(10, pairs.Count());
        // 1,280,000B merge scratch reserved beside the call; nothing retained.
        Assert.Equal(1280000L, context.MemoryGovernor.PeakReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ChainMapLengthReservesTransientScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var chainMap = SharedChainMap(context, span);
        var method = typeof(LythonRuntime).GetMethod("Len", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Len not found.");
        var length = method.Invoke(null, [new object[] { chainMap }, span, context]);
        Assert.Equal(new BigInteger(10), length);
        // Set-only scratch reuses the merge estimate conservatively.
        Assert.Equal(1280000L, context.MemoryGovernor.PeakReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
