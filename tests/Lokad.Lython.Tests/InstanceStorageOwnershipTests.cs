using System.Runtime.CompilerServices;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class InstanceStorageOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(12, 4, 3, 2);
    private static PyType Type() => new("Holder", [], new Dictionary<string, object>());

    [Fact]
    public void ConstructorDenialPreservesAnotherReservation()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(159));
        governor.Reserve(16, Span);
        var error = Assert.Throws<LythonRuntimeException>(() => new PyInstance(Type(), governor, Span));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal(16, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void PopulatedAttachmentDenialLeavesTheOwnerRetryable()
    {
        var instance = new PyInstance(Type());
        var value = new object();
        instance.SetAttribute("first", value);
        instance.SetAttribute("second", value);
        var denied = new MemoryGovernor(new LythonMemoryBudget(287));
        Assert.Throws<LythonRuntimeException>(() => instance.AttachMemoryGovernor(denied, Span));
        Assert.Null(instance.OwnerMemoryGovernor);
        Assert.Null(instance.AllocationSpan);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        Assert.Equal(0, instance.CommittedStorageBytes);
        Assert.True(instance.TryGetOwnAttribute("first", out var retained));
        Assert.Same(value, retained);

        var funded = new MemoryGovernor(new LythonMemoryBudget(288));
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
    public void ClearedInstanceShellStaysOwnedUntilBatchCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        CreateDrop(governor);
        Assert.Equal(160, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateDrop(MemoryGovernor governor)
    {
        var instance = new PyInstance(Type(), governor, Span);
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
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4096) });
        var allocator = new Allocator();
        var type = new PyType("Alias", [], new() { ["__new__"] = allocator });
        var instance = new PyInstance(type, context.MemoryGovernor, Span);
        allocator.Result = instance;
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
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4096) });
        var wanted = new object();
        var type = new PyType("Foreign", [], new() { ["__new__"] = new Allocator { Result = wanted } });
        PinRemaining(context.MemoryGovernor);
        var result = asynchronous ? await type.InvokeAsync([], Span, context) : type.Invoke([], Span, context);
        Assert.Same(wanted, result);
        Assert.Equal(4096, context.MemoryGovernor.CurrentCommittedBytes);
    }

    private static long PinRemaining(MemoryGovernor governor)
    {
        var bytes = 4096 - governor.CurrentAccountedBytes;
        governor.Reserve(bytes, Span);
        governor.Commit(bytes);
        return bytes;
    }

    [Fact]
    public void CancellationDuringDeepcopyReleasesStagingButKeepsPinnedScratch()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { CancellationToken = cancellation.Token });
        var hook = new CancelCopy(cancellation);
        var objectType = Assert.IsType<PyType>(context.State.BuiltinVariables["object"]);
        var hookType = new PyType("Cancel", [objectType], new() { ["__deepcopy__"] = hook });
        var child = new PyInstance(hookType, context.MemoryGovernor, Span);
        var source = new PyInstance(Type(), context.MemoryGovernor, Span);
        source.SetAttribute("child", child);
        context.MemoryGovernor.Reserve(32, Span);
        var memoType = typeof(LythonRuntime).GetNestedType("CopyMemo", BindingFlags.NonPublic)!;
        var memo = (IDisposable)memoType.GetConstructors().Single(c => c.GetParameters().Length == 2).Invoke([context, Span]);
        try
        {
            var copy = typeof(LythonRuntime).GetMethod("CopyValue", BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.Equal(1, source.OwnAttributeCount);
            Assert.Equal(cancellation.Token, context.Limits.CancellationToken);
            Assert.True(PyMemberAccess.TryResolve(child, "__deepcopy__", context, Span, out var resolved));
            Assert.Same(hook, resolved);
            var failure = Record.Exception(() => copy.Invoke(null, [source, LythonRuntime.CopyDepth.Deep, context, Span, memo, 0]));
            Assert.Equal(1, hook.Calls);
            Assert.True(cancellation.IsCancellationRequested);
            var error = Assert.IsType<TargetInvocationException>(failure);
            var canceled = Assert.IsType<LythonRuntimeException>(error.InnerException);
            Assert.Equal("RuntimeError", canceled.ExceptionType);
            Assert.Equal("execution canceled", canceled.Message);
            Assert.Same(Span, canceled.Span);
        }
        finally
        {
            memo.Dispose();
        }
        Assert.Equal(32, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(224, source.CommittedStorageBytes);
        GC.KeepAlive(source);
    }

    private sealed class CancelCopy(CancellationTokenSource cancellation) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            cancellation.Cancel();
            return System.Numerics.BigInteger.One;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => ValueTask.FromResult(Invoke(arguments, span, context));
    }

    private sealed class Allocator : LythonRuntime.ICallable
    {
        public object Result { get; set; } = PyNone.Instance;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => Result;
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => ValueTask.FromResult(Result);
    }
}
