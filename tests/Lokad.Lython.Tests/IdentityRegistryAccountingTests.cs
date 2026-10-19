using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Python id() retains reference-identity semantics. Its metadata estimate stays
// conservatively owned by the finite execution, not refunded by collection.
[Collection("AllocationSensitive")]
public sealed class IdentityRegistryAccountingTests
{
    private static LythonRuntime.ExecutionContext NewRoot(long? budget, out MemoryGovernor governor, ILythonMemoryBudget? shared = null)
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = shared },regexProvider:Utf8RegexProvider.Instance);
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

        Assert.Equal(1000 * 64, governor.CurrentCommittedBytes);
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

        Assert.Equal(64, governor.CurrentCommittedBytes);
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
    public void CollectedIdentitiesKeepMetadataChargedUntilBatchCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var root = NewRoot(null, out var governor, owner);
        RegisterMany(root, 16);
        var funded = budget.ReservedBytes;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.Equal(funded, budget.ReservedBytes);
        Assert.Equal(16 * 64, governor.CurrentCommittedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeniedInsertionStrandsNothing()
    {
        var root = NewRoot(0, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void NextExecutionReusesAllowanceAfterIdentityOwnerCleanup()
    {
        var budget = new LythonMemoryBudget(512);
        using (var owner = budget.CreateScope())
        {
            var root = NewRoot(null, out _, owner);
            var keys = BuildRegistered(root, 8);
            Assert.Equal(512, budget.ReservedBytes);
            Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
            ForgetKeys(keys);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.Equal(512, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using var next = budget.CreateScope();
        var resumed = NewRoot(null, out var governor, next);
        resumed.State.GetObjectId(new object());
        Assert.Equal(64, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void IdentityShellAdmissionFailureStrandsNothing()
    {
        // Admission must fail before publishing a new identity.
        var root = NewRoot(63, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedIdentityAdmissionStaysDenied()
    {
        // A denied registration publishes nothing, so retrying the same key
        // denies again instead of returning an untracked ID.
        var root = NewRoot(63, out var governor);
        var key = new object();
        Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(key));
        var retry = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(key));
        Assert.Equal("MemoryError", retry.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        GC.KeepAlive(key);
    }
}
