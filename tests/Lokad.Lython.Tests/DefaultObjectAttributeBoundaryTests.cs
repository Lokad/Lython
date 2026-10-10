using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DefaultObjectAttributeBoundaryTests
{
    private static readonly LythonSourceSpan Span = new(41, 6, 5, 3);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultSlotsKeepValuesChargesAndExecutionState(bool asynchronous)
    {
        var context = Root();
        var instance = Instance(context);
        var first = new object();
        var second = new object();
        var active = new PyException("ValueError", "active", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var backingBefore = context.State.CallTemporaries.CommittedBackingBytes;
        Assert.True(asynchronous
            ? await PyMemberAccess.TryAssignAsync(instance, "value", first, context, Span)
            : PyMemberAccess.TryAssign(instance, "value", first, context, Span));
        Assert.Same(context.MemoryGovernor, instance.OwnerMemoryGovernor);
        Assert.Equal(64, instance.CommittedAttributeBytes);
        Assert.Equal(before + 64 + ChargeReclamationPool.EntryChargeBytes +
            context.State.CallTemporaries.CommittedBackingBytes - backingBefore,
            context.MemoryGovernor.CurrentCommittedBytes);
        var charged = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Same(first, await Read(instance, "value", context, asynchronous));
        Assert.True(asynchronous
            ? await PyMemberAccess.TryAssignAsync(instance, "value", second, context, Span)
            : PyMemberAccess.TryAssign(instance, "value", second, context, Span));
        Assert.Same(second, await Read(instance, "value", context, asynchronous));
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(64, instance.CommittedAttributeBytes);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(0, context.Limits.ExecutionStepCount);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedSlotGrowthKeepsReservationSpanAndUnwrittenValue(bool asynchronous)
    {
        var context = Root();
        var instance = Instance(context);
        var pressure = 8192 - context.MemoryGovernor.CurrentAccountedBytes - 63;
        context.MemoryGovernor.Reserve(pressure, Span);
        context.MemoryGovernor.Commit(pressure);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await PyMemberAccess.TryAssignAsync(instance, "new", new object(), context, Span);
            else PyMemberAccess.TryAssign(instance, "new", new object(), context, Span);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal(64, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.False(instance.TryGetOwnAttribute("new", out _));
        Assert.Equal(0, instance.CommittedAttributeBytes);
        Assert.Equal(0, context.Limits.ExecutionStepCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataDescriptorsKeepContextSpanChargesAndCallOrder(bool asynchronous)
    {
        var context = Root();
        var descriptor = new RecordingDescriptor(context);
        var instance = Instance(context, new Dictionary<string, object> { ["value"] = descriptor });
        var wanted = new object();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(asynchronous
            ? await PyMemberAccess.TryAssignAsync(instance, "value", wanted, context, Span)
            : PyMemberAccess.TryAssign(instance, "value", wanted, context, Span));
        Assert.Same(wanted, await Read(instance, "value", context, asynchronous));
        Assert.Equal(new[] { "set", "get" }, descriptor.Calls);
        Assert.Same(instance, descriptor.Receiver);
        Assert.Equal(before + 48, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, instance.CommittedAttributeBytes);
        Assert.Equal(2, context.Limits.ExecutionStepCount);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DescriptorChecksFuelBeforeCancellation(bool asynchronous, bool fuelDenied)
    {
        using var cancellation = new CancellationTokenSource();
        var context = Root(new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 8192, MaxExecutionSteps = fuelDenied ? 1 : 2,
            CancellationToken = cancellation.Token
        });
        var descriptor = new RecordingDescriptor(context);
        var instance = Instance(context, new Dictionary<string, object> { ["value"] = descriptor });
        context.CheckExecution(Span);
        cancellation.Cancel();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Read(instance, "value", context, asynchronous));
        Assert.StartsWith(fuelDenied ? "maximum execution step count exceeded (1)" : "execution canceled", error.Message, StringComparison.Ordinal);
        Assert.Same(Span, error.Span);
        Assert.Equal(2, context.Limits.ExecutionStepCount);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Empty(descriptor.Calls);
    }

    private static Context Root(LythonRunOptions? options = null)
        => new(new MockLythonHost(), options ?? new LythonRunOptions { MaxExecutionMemoryBytes = 8192, MaxExecutionSteps = 1000 });

    private static PyInstance Instance(Context context, Dictionary<string, object>? members = null)
        => new(new PyType("C", [Assert.IsType<PyType>(context.State.BuiltinVariables["object"])], members ?? []), context.MemoryGovernor, Span);

    private static async ValueTask<object> Read(PyInstance instance, string name, Context context, bool asynchronous)
    {
        if (asynchronous)
        {
            var resolved = await PyAttributeLookup.TryResolveInstanceMemberAsync(instance, name, context, Span);
            Assert.True(resolved.Found);
            return resolved.Value;
        }
        Assert.True(PyAttributeLookup.TryResolveInstanceMember(instance, name, context, Span, out var value));
        return value;
    }

    private sealed class RecordingDescriptor(Context expected) : IPyDescriptor, IPySettableDescriptor
    {
        public List<string> Calls { get; } = [];
        public PyInstance? Receiver { get; private set; }
        private object _value = PyNone.Instance;
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
        {
            Assert.Same(expected, context);
            Assert.Same(Span, span);
            Assert.Same(Assert.IsType<PyInstance>(instance).Type, owner);
            context!.CheckExecution(span);
            context.MemoryGovernor.Reserve(16, span);
            context.MemoryGovernor.Commit(16);
            Calls.Add("get");
            Receiver = (PyInstance)instance!;
            return _value;
        }
        public void Set(PyInstance instance, object value, Context context, LythonSourceSpan span)
        {
            Assert.Same(expected, context);
            Assert.Same(Span, span);
            context.CheckExecution(span);
            context.MemoryGovernor.Reserve(32, span);
            context.MemoryGovernor.Commit(32);
            Calls.Add("set");
            Receiver = instance;
            _value = value;
        }
    }
}
