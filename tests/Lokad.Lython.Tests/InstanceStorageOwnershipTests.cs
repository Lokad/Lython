using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class InstanceStorageOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(12, 4, 3, 2);
    private static PyType Type() => new("Holder", [], new Dictionary<string, object>());

    [Fact]
    public void ConstructorDenialPreservesAnotherReservation()
    {
        var governor = new MemoryGovernor(159);
        governor.Reserve(16, Span);
        var error = Assert.Throws<LythonRuntimeException>(() => new PyInstance(Type(), governor, Span));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal(16, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void FreshRegistrationDenialRefundsOnlyItsShell()
    {
        var governor = new MemoryGovernor(4096);
        var pool = new ChargeReclamationPool(governor);
        governor.Reserve(3936, Span);
        governor.Commit(3936);
        var instance = new PyInstance(Type(), governor, Span);
        var error = Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshMutable(instance, instance.CommittedStorageBytes, Span));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.False(pool.IsTracked(instance));
        Assert.Equal(3936, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(instance);
    }

    [Fact]
    public void PopulatedAttachmentDenialLeavesTheOwnerRetryable()
    {
        var instance = new PyInstance(Type());
        var value = new object();
        instance.SetAttribute("first", value);
        instance.SetAttribute("second", value);
        var denied = new MemoryGovernor(287);
        Assert.Throws<LythonRuntimeException>(() => instance.AttachMemoryGovernor(denied, Span));
        Assert.Null(instance.OwnerMemoryGovernor);
        Assert.Null(instance.AllocationSpan);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        Assert.Equal(0, instance.CommittedStorageBytes);
        Assert.True(instance.TryGetOwnAttribute("first", out var retained));
        Assert.Same(value, retained);

        var funded = new MemoryGovernor(288);
        instance.AttachMemoryGovernor(funded, Span);
        instance.AttachMemoryGovernor(denied, new LythonSourceSpan(0, 0, 0, 0));
        Assert.Same(funded, instance.OwnerMemoryGovernor);
        Assert.Same(Span, instance.AllocationSpan);
        Assert.Equal(288, funded.CurrentCommittedBytes);
        Assert.True(instance.RemoveAttribute("first"));
        Assert.True(instance.RemoveAttribute("second"));
        Assert.Equal(160, funded.CurrentCommittedBytes);
        Assert.Equal(0, funded.CurrentReservedBytes);
    }

    [Fact]
    public void DroppedOwnerReclaimsItsShellAfterAllAttributesAreDeleted()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        CreateDrop(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        pool.Sweep(full: true);
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateDrop(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var instance = new PyInstance(Type(), governor, Span);
        pool.TrackFreshMutable(instance, instance.CommittedStorageBytes, Span);
        instance.SetAttribute("first", 1);
        instance.SetAttribute("second", 2);
        instance.SetAttribute("first", 3);
        Assert.Equal(288, instance.CommittedStorageBytes);
        instance.RemoveAttribute("first");
        instance.RemoveAttribute("second");
        Assert.Equal(160, instance.CommittedStorageBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllocatorAliasNeedsNoDiscardedPlaceholder(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });
        var allocator = new Allocator();
        var type = new PyType("Alias", [], new() { ["__new__"] = allocator });
        var instance = new PyInstance(type, context.MemoryGovernor, Span);
        allocator.Result = instance;
        context.State.CallTemporaries.TrackFreshMutable(instance, instance.CommittedStorageBytes, Span);
        PinRemaining(context.MemoryGovernor);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        var result = asynchronous ? await type.InvokeAsync([], Span, context) : type.Invoke([], Span, context);
        Assert.Same(instance, result);
        Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllocatorForeignResultNeedsNoInstanceFunding(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });
        var wanted = new object();
        var type = new PyType("Foreign", [], new() { ["__new__"] = new Allocator { Result = wanted } });
        PinRemaining(context.MemoryGovernor);
        var result = asynchronous ? await type.InvokeAsync([], Span, context) : type.Invoke([], Span, context);
        Assert.Same(wanted, result);
        Assert.Equal(4096, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllocatorAliasRegistrationDenialKeepsItsLiveStorage(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });
        var allocator = new Allocator();
        var type = new PyType("Alias", [], new() { ["__new__"] = allocator });
        var instance = new PyInstance(type, context.MemoryGovernor, Span);
        allocator.Result = instance;
        var pinned = PinRemaining(context.MemoryGovernor);
        await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await type.InvokeAsync([], Span, context);
            else type.Invoke([], Span, context);
        });
        Assert.Equal(4096, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.Release(pinned);
        var result = asynchronous ? await type.InvokeAsync([], Span, context) : type.Invoke([], Span, context);
        Assert.Same(instance, result);
        Assert.True(context.State.CallTemporaries.IsTracked(instance));
    }

    private static long PinRemaining(MemoryGovernor governor)
    {
        var bytes = 4096 - governor.CurrentAccountedBytes;
        governor.Reserve(bytes, Span);
        governor.Commit(bytes);
        return bytes;
    }

    private sealed class Allocator : LythonRuntime.ICallable
    {
        public object Result { get; set; } = PyNone.Instance;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => Result;
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => ValueTask.FromResult(Result);
    }
}
