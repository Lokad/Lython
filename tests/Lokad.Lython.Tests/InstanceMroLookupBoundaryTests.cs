using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class InstanceMroLookupBoundaryTests
{
    private static readonly LythonSourceSpan Span = new(23, 5, 4, 7);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritedReplacementAndRemovalResolveCurrentMembers(bool asynchronous)
    {
        var context = Root();
        var first = new object();
        var second = new object();
        var parent = Type(context, new() { ["value"] = first });
        var type = new PyType("Child", [parent], []);
        var instance = new PyInstance(type, context.MemoryGovernor, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Same(first, await Read(instance, context, asynchronous));
        Assert.True(parent.TrySetMember("value", second));
        Assert.Same(second, await Read(instance, context, asynchronous));
        Assert.True(type.TrySetMember("value", first));
        Assert.Same(first, await Read(instance, context, asynchronous));
        Assert.True(type.RemoveOwnMember("value"));
        Assert.Same(second, await Read(instance, context, asynchronous));
        Assert.True(parent.RemoveOwnMember("value"));
        var missing = await Resolve(instance, "value", context, asynchronous);
        Assert.False(missing.Found);
        Assert.Same(PyNone.Instance, missing.Value);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnAttributeShadowsNonDataDescriptorUntilRemoved(bool asynchronous)
    {
        var context = Root();
        var descriptorValue = new object();
        var own = new object();
        var descriptor = new Getter(context, _ => descriptorValue);
        var instance = Instance(context, descriptor);
        instance.SetAttribute("value", own);
        var charged = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Same(own, await Read(instance, context, asynchronous));
        Assert.Equal(0, descriptor.Calls);
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(instance.RemoveAttribute("value"));
        Assert.Same(descriptorValue, await Read(instance, context, asynchronous));
        Assert.Equal(1, descriptor.Calls);
        Assert.Equal(charged - 64 + 16, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetterMutationReentersActualReceiverWithFreshLookup(bool asynchronous)
    {
        var context = Root();
        var first = new object();
        var replacement = new object();
        var descriptor = new Getter(context, receiver =>
        {
            Assert.True(receiver.Type.TrySetMember("value", replacement));
            Assert.True(PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(
                receiver, "value", context, Span, out var nested));
            Assert.Same(replacement, nested);
            return first;
        });
        var instance = Instance(context, descriptor);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Same(first, await Read(instance, context, asynchronous));
        Assert.Same(replacement, await Read(instance, context, asynchronous));
        Assert.Equal(1, descriptor.Calls);
        Assert.Equal(before + 16, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataDescriptorWinsThenOwnFieldWinsAfterReplacement(bool asynchronous)
    {
        var context = Root();
        var descriptorValue = new object();
        var own = new object();
        var descriptor = new DataGetter(context, descriptorValue);
        var instance = Instance(context, descriptor);
        instance.SetAttribute("value", own);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Same(descriptorValue, await Read(instance, context, asynchronous));
        Assert.True(instance.Type.TrySetMember("value", new object()));
        Assert.Same(own, await Read(instance, context, asynchronous));
        Assert.Equal(1, descriptor.Calls);
        Assert.Equal(before + 16, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingAndSpecialClassAccessKeepExactCosts(bool asynchronous)
    {
        var context = Root();
        var instance = Instance(context, new object());
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var missing = await Resolve(instance, "absent", context, asynchronous);
        Assert.False(missing.Found);
        Assert.Same(PyNone.Instance, missing.Value);
        var type = await Resolve(instance, "__class__", context, asynchronous);
        Assert.True(type.Found);
        Assert.Same(instance.Type, type.Value);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DescriptorFailureKeepsActualExceptionAndContext(bool asynchronous)
    {
        var context = Root();
        var active = new PyException("ValueError", "active", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var wanted = new LythonRuntimeException("ValueError", "getter", Span);
        var descriptor = new Getter(context, _ => throw wanted);
        var instance = Instance(context, descriptor);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Read(instance, context, asynchronous));
        Assert.Same(wanted, error);
        Assert.Same(Span, error.Span);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(before + 16, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(1, descriptor.Calls);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DescriptorChecksCancellation(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var context = Root(new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(8192),             CancellationToken = cancellation.Token
        });
        var descriptor = new Getter(context, _ => new object());
        var instance = Instance(context, descriptor);
        context.CheckExecution(Span);
        cancellation.Cancel();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Read(instance, context, asynchronous));
        Assert.StartsWith("execution canceled", error.Message, StringComparison.Ordinal);
        Assert.Same(Span, error.Span);
        Assert.Equal(0, descriptor.Calls);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Fact]
    public async Task AwaitedHostReadKeepsActualContextAcrossClassMutation()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/payload", "payload");
        var context = new Context(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192)});
        var first = new object();
        var replacement = new object();
        var descriptor = new HostGetter(context, first);
        var instance = Instance(context, descriptor);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var pending = Read(instance, context, true);
        await descriptor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        Assert.True(instance.Type.TrySetMember("value", replacement));
        host.OnTextReadCompleted = () =>
        {
            Assert.True(PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, "value", context, Span, out var nested));
            Assert.Same(replacement, nested);
        };
        descriptor.Release.SetResult();
        Assert.Same(first, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(replacement, await Read(instance, context, true));
        Assert.Equal(1, host.CompletedAsynchronously);
        Assert.Equal(before + 16, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    [Fact]
    public async Task AwaitedHostCancellationKeepsSpanAndNextLookupFresh()
    {
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var parked = host.PauseReadUntilCancellation("/payload");
        var context = new Context(host, new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(8192), CancellationToken = cancellation.Token
        });
        var replacement = new object();
        var descriptor = new HostGetter(context, new object());
        var instance = Instance(context, descriptor);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var pending = Read(instance, context, true);
        descriptor.Release.SetResult();
        await parked.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        Assert.True(instance.Type.TrySetMember("value", replacement));
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.StartsWith("execution canceled", error.Message, StringComparison.Ordinal);
        Assert.Same(Span, error.Span);
        Assert.Same(replacement, await Read(instance, context, true));
        Assert.Equal(before + 16, context.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(context);
    }

    private static Context Root(LythonRunOptions? options = null)
        => new(new MockLythonHost(), options ?? new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192)});
    private static PyType Type(Context context, Dictionary<string, object> members)
        => new("C", [Assert.IsType<PyType>(context.State.BuiltinVariables["object"])], members);
    private static PyInstance Instance(Context context, object member)
        => new(Type(context, new() { ["value"] = member }), context.MemoryGovernor, Span);
    private static async Task<(bool Found, object Value)> Resolve(PyInstance instance, string name, Context context, bool asynchronous)
        => asynchronous
            ? await PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallbackAsync(instance, name, context, Span)
            : (PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, name, context, Span, out var value), value!);
    private static async Task<object> Read(PyInstance instance, Context context, bool asynchronous)
    {
        var result = await Resolve(instance, "value", context, asynchronous);
        Assert.True(result.Found);
        return result.Value;
    }
    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
    }
    private class Getter(Context expected, Func<PyInstance, object> action) : IPyDescriptor
    {
        public int Calls { get; private set; }
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
        {
            var receiver = Assert.IsType<PyInstance>(instance);
            Assert.Same(receiver.Type, owner);
            Assert.Same(expected, context);
            Assert.Same(Span, span);
            context!.CheckExecution(span);
            context.MemoryGovernor.Reserve(16, span);
            context.MemoryGovernor.Commit(16);
            Calls++;
            return action(receiver);
        }
    }
    private sealed class DataGetter(Context context, object value) : Getter(context, _ => value), IPySettableDescriptor
    {
        public void Set(PyInstance instance, object value, Context context, LythonSourceSpan span)
            => throw new InvalidOperationException("Lookup must not invoke the setter.");
    }
    private sealed class HostGetter(Context expected, object value) : IPyDescriptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
            => throw new InvalidOperationException("The awaited descriptor must use GetAsync.");
        public async ValueTask<object> GetAsync(object? instance, PyType owner, Context? context, LythonSourceSpan? span)
        {
            Assert.Same(Assert.IsType<PyInstance>(instance).Type, owner);
            Assert.Same(expected, context);
            Assert.Same(Span, span);
            context!.CheckExecution(span);
            context.MemoryGovernor.Reserve(16, span);
            context.MemoryGovernor.Commit(16);
            Started.SetResult();
            await Release.Task.WaitAsync(context.Limits.CancellationToken);
            var payload = await context.ReadTextUtf8Async("/payload", span);
            Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(payload.Span));
            return value;
        }
    }
}
