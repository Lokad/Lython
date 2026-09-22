using System.Linq;
using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG14: cache entry infrastructure (slots, recency nodes, records) pays per
/// entry; eviction and clear release exactly what they drop. Reflection reaches
/// the private wrapper type; renames fail loudly here by design.
/// </summary>
public sealed class FunctoolsCacheAccountingTests
{
    private static object NewCacheWrapper(LythonRuntime.ExecutionContext context, int? maxSize)
    {
        var wrapperType = typeof(LythonRuntime).GetNestedType("PyLruCacheWrapper", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PyLruCacheWrapper not found.");
        var keyModeType = typeof(LythonRuntime).GetNestedType("CacheKeyMode", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CacheKeyMode not found.");
        var ctor = wrapperType.GetConstructors().Single();
        return ctor.Invoke([null, maxSize, Enum.ToObject(keyModeType, 0), context.MemoryGovernor, context.Services.State.CallTemporaries]);
    }

    private static void InvokeStore(object wrapper, object key, object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var method = wrapper.GetType().GetMethod("Store", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Store not found.");
        method.Invoke(wrapper, [key, 0L, value, span, context]);
    }

    private static void InvokeClear(object wrapper)
    {
        var method = wrapper.GetType().GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Clear not found.");
        method.Invoke(wrapper, []);
    }

    [Fact]
    public void UnboundedEntriesAccumulateAndClearReleases()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var wrapper = NewCacheWrapper(context, null);
        for (var i = 0; i < 100; i++)
        {
            InvokeStore(wrapper, new BigInteger(i), new BigInteger(i), span, context);
        }

        Assert.Equal((100L * 128L) + 128L + 32L, context.MemoryGovernor.CurrentCommittedBytes); // +128 registry/+32 pool backing
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        InvokeClear(wrapper);
        Assert.Equal(128L + 32L, context.MemoryGovernor.CurrentCommittedBytes); // live entry + pool backing
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void BoundedEvictionKeepsExactlyLiveEntries()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var wrapper = NewCacheWrapper(context, 8);
        for (var i = 0; i < 20; i++)
        {
            InvokeStore(wrapper, new BigInteger(i), new BigInteger(i), span, context);
        }

        Assert.Equal((8L * 128L) + 128L + 32L, context.MemoryGovernor.CurrentCommittedBytes); // +128 registry/+32 backing
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
