using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N08 white-box: explicit key/infra/metadata ownership with exact release on
// eviction, clear, insertion denial and wrapper drops.
public sealed class FunctoolsCacheOwnershipInvariantTests
{
    private static object NewWrapper(LythonRuntime.ExecutionContext context, int? maxSize)
    {
        var wrapperType = typeof(LythonRuntime).GetNestedType("PyLruCacheWrapper", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PyLruCacheWrapper not found.");
        var keyModeType = typeof(LythonRuntime).GetNestedType("CacheKeyMode", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CacheKeyMode not found.");
        var ctor = wrapperType.GetConstructors().Single();
        return ctor.Invoke([null, maxSize, Enum.ToObject(keyModeType, 0), context.MemoryGovernor, context.Services.State.CallTemporaries]);
    }

    private static LythonRuntime.ExecutionContext NewContext()
        => new(new MockLythonHost(), new LythonRunOptions());

    private static LythonSourceSpan Span() => new(0, 0, 0, 0);

    private static PyTuple NewKey(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int value)
    {
        var key = new PyTuple([(object)new BigInteger(value)], context.MemoryGovernor, span);
        return key;
    }

    private static void InvokeStore(object wrapper, object key, long keyCharge, object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var method = wrapper.GetType().GetMethod("Store", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Store not found.");
        method.Invoke(wrapper, [key, keyCharge, value, span, context]);
    }

    [Fact]
    public void EvictionReleasesEvictedKeyGraphs()
    {
        var context = NewContext();
        var span = Span();
        var wrapper = NewWrapper(context, 1);
        var baseline = context.MemoryGovernor.CurrentCommittedBytes;
        var k1 = NewKey(context, span, 1);
        var c1 = k1.CommittedStorageBytes;
        InvokeStore(wrapper, k1, c1, new BigInteger(1), span, context);
        Assert.Equal(128L + c1 + 128L + 32L, context.MemoryGovernor.CurrentCommittedBytes - baseline); // infra+key+registry+backing
        var k2 = NewKey(context, span, 2);
        var c2 = k2.CommittedStorageBytes;
        InvokeStore(wrapper, k2, c2, new BigInteger(2), span, context);
        Assert.Equal(128L + c2 + 128L + 32L, context.MemoryGovernor.CurrentCommittedBytes - baseline); // evicted key released
    }

    [Fact]
    public void InsertionDenialReleasesFreshKey()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 128 });
        var span = Span();
        var wrapper = NewWrapper(context, 1);
        var baseline = context.MemoryGovernor.CurrentCommittedBytes;
        var key = new PyTuple([(object)new BigInteger(1)], context.MemoryGovernor, span);
        var keyCharge = key.CommittedStorageBytes;
        var store = wrapper.GetType().GetMethod("Store", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Store not found.");
        // 128B infra cannot fit beside the fresh key in 256B: denial must leave exact charges.
        var failure = Assert.Throws<TargetInvocationException>(() => store.Invoke(wrapper, [key, keyCharge, new BigInteger(1), span, context]));
        Assert.IsType<LythonRuntimeException>(failure.InnerException);
        Assert.Equal("MemoryError", ((LythonRuntimeException)failure.InnerException!).ExceptionType);
        Assert.Equal(baseline, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void DroppedWrapperSweepsCoupon()
    {
        var context = NewContext();
        var pool = context.Services.State.CallTemporaries;
        var baseline = context.MemoryGovernor.CurrentCommittedBytes;
        AbandonPopulatedWrapper(context);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Coupon (infra + key) plus the pool entry charge release; tier backing persists.
        Assert.Equal(128L + 48L + 128L, pool.Sweep(full: true));
        Assert.Equal(baseline + 32L, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AbandonPopulatedWrapper(LythonRuntime.ExecutionContext context)
    {
        var span = Span();
        var wrapper = NewWrapper(context, null);
        var key = NewKey(context, span, 1);
        InvokeStore(wrapper, key, key.CommittedStorageBytes, new BigInteger(1), span, context);
    }
}
