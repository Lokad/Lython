using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// MG11: the per-run identity registry owns one entry per distinct live
// identity (shared across repeated lookups, released with dropped objects);
// denied insertions strand nothing and recovery works after reclamation.
public sealed class IdentityRegistryAccountingTests
{
    private static LythonRuntime.ExecutionContext NewRoot(long? budget, out MemoryGovernor governor)
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        governor = root.MemoryGovernor;
        return root;
    }

    // Element-touching loops live in non-inlined helpers: in Debug builds a
    // last loop temporary merged into the test scope would pin one key and
    // strand its charge.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RegisterMany(LythonRuntime.ExecutionContext root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            root.State.GetObjectId(new object());
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static List<object> BuildRegistered(LythonRuntime.ExecutionContext root, int count)
    {
        var keys = new List<object>();
        for (var i = 0; i < count; i++)
        {
            keys.Add(new object());
        }

        for (var i = 0; i < keys.Count; i++)
        {
            root.State.GetObjectId(keys[i]);
        }

        return keys;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ForgetKeys(List<object> keys)
    {
        keys.Clear();
    }

    [Fact]
    public void DistinctIdentitiesPayOnceEach()
    {
        var root = NewRoot(null, out var governor);
        var keys = new List<object>();
        for (var i = 0; i < 1000; i++)
        {
            keys.Add(new object());
        }

        foreach (var key in keys)
        {
            root.State.GetObjectId(key);
        }

        Assert.Equal(1000 * 128, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void RepeatedLookupsShareCharge()
    {
        var root = NewRoot(null, out var governor);
        var key = new object();
        var first = root.State.GetObjectId(key);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first, root.State.GetObjectId(key));
        }

        Assert.Equal(128, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DistinctObjectsGetDistinctIds()
    {
        var root = NewRoot(null, out _);
        var seen = new HashSet<System.Numerics.BigInteger>();
        for (var i = 0; i < 100; i++)
        {
            Assert.True(seen.Add(root.State.GetObjectId(new object())));
        }
    }

    [Fact]
    public void DroppedIdentitiesRelease()
    {
        var root = NewRoot(null, out var governor);
        RegisterMany(root, 1000);
        Assert.Equal(1000 * 128, governor.CurrentCommittedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        root.State.CallTemporaries.Sweep();
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DeniedInsertionStrandsNothing()
    {
        var root = NewRoot(0, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, root.State.CallTemporaries.Count);
    }

    [Fact]
    public void RecoveryWorksAfterReclamation()
    {
        var root = NewRoot(1024, out var governor);
        var keys = BuildRegistered(root, 8);
        Assert.Equal(1024, governor.CurrentCommittedBytes);
        Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        ForgetKeys(keys);
        keys = null!;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        root.State.CallTemporaries.Sweep();
        Assert.Equal(0, governor.CurrentCommittedBytes);
        root.State.GetObjectId(new object());
        Assert.Equal(128, governor.CurrentCommittedBytes);
    }
}
