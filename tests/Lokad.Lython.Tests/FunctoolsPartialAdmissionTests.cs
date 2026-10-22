using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class FunctoolsPartialAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static LythonRuntime.ICallable Create(LythonRuntime.ICallable target, int count, LythonRuntime.ExecutionContext context)
    {
        var arguments = new CallArgumentValue[count + 1];
        arguments[0] = CallArgumentValue.Positional(target);
        Array.Fill(arguments, CallArgumentValue.Positional(PyNone.Instance), 1, count);
        return (LythonRuntime.ICallable)LythonRuntime.PartialFactory.Instance.Invoke(arguments, Span, context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeniedWorkingStorageDoesNotAllocateArgumentsOrReachTarget(bool asynchronous)
    {
        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var owner = operation.CreateScope())
        {
            var inputContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = inputOwner });
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var target = new SuspendedTarget();
            var partial = Create(target, 10000, inputContext);
            var inputBaseline = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (asynchronous) partial.InvokeAsync([], Span, context).GetAwaiter().GetResult();
                else partial.Invoke([], Span, context);
            });
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("MemoryError", error.ExceptionType);
#if !DEBUG
            Assert.True(allocated < 32768, $"Allocated {allocated} bytes before refusal.");
#endif
            Assert.Equal(0, target.Calls);
            Assert.Equal(0, operation.ReservedBytes);
            Assert.Equal(inputBaseline, inputs.ReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingTargetKeepsWorkingStorageFundedAndUnwinds(bool cancel)
    {
        var budget = new LythonMemoryBudget(32768);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var target = new SuspendedTarget(cancellation.Token);
            var partial = Create(target, 4, context);
            var baseline = budget.ReservedBytes;
            var pending = partial.InvokeAsync([], Span, context).AsTask();
            try
            {
                await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(context.MemoryGovernor.CurrentReservedBytes > 0);
                Assert.True(budget.ReservedBytes > baseline);
                if (cancel) cancellation.Cancel();
                else target.Resume.TrySetResult();
                if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                else Assert.Same(PyNone.Instance, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                cancellation.Cancel();
                target.Resume.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReplacedMetadataKeepsOldAliasFunded()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var partial = Create(new SuspendedTarget(), 1, context);
            var attributes = (IPyContextualDynamicAttributes)partial;
            Assert.True(attributes.TryGetMember("__dict__", context, Span, out var old));
            var oldDictionary = Assert.IsType<PyDict>(old);
            oldDictionary.SetItem(PyString.FromString("value", context.MemoryGovernor, Span), PyNone.Instance);
            var replacement = new PyDict(context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            Assert.True(((IPyMutableDynamicAttributes)partial).TrySetMember("__dict__", replacement));
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Single(oldDictionary);
            Assert.True(attributes.TryGetMember("__dict__", context, Span, out var current));
            Assert.Same(replacement, current);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RenderingDeniesLargeOutputBeforeBuildingIt()
    {
        var inputs = new LythonMemoryBudget();
        var output = new LythonMemoryBudget(4096);
        using (var inputOwner = inputs.CreateScope())
        using (var outputOwner = output.CreateScope())
        {
            var inputContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = inputOwner });
            var outputContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = outputOwner });
            var text = PyString.FromString(new string('x', 20000), inputContext.MemoryGovernor, Span);
            var partial = (IPyRenderableValue)LythonRuntime.PartialFactory.Instance.Invoke(
                [CallArgumentValue.Positional(LythonRuntime.PartialFactory.Instance), CallArgumentValue.Positional(text)], Span, inputContext);
            var baseline = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => partial.RenderPython(new PyRenderingContext(outputContext)));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("MemoryError", error.ExceptionType);
#if !DEBUG
            Assert.True(allocated < 32768, $"Allocated {allocated} bytes before refusal.");
#endif
            Assert.Equal(0, outputContext.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(baseline, inputs.ReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, output.ReservedBytes);
    }

    [Fact]
    public async Task KeywordEditsDuringSuspensionDoNotChangeThePendingCall()
    {
        var budget = new LythonMemoryBudget(32768);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var target = new SuspendedTarget(expectedLength: 1);
            var partial = (LythonRuntime.ICallable)LythonRuntime.PartialFactory.Instance.Invoke(
                [CallArgumentValue.Positional(target), CallArgumentValue.Keyword("value", PyNone.Instance)], Span, context);
            Assert.True(((IPyContextualDynamicAttributes)partial).TryGetMember("keywords", context, Span, out var value));
            var keywords = Assert.IsType<PyDict>(value);
            var pending = partial.InvokeAsync([], Span, context).AsTask();
            try
            {
                await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                keywords.SetItem(PyString.FromString("value", context.MemoryGovernor, Span), true);
                target.Resume.TrySetResult();
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(PyNone.Instance, target.LastArgument);
            }
            finally
            {
                target.Resume.TrySetResult();
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class SuspendedTarget(CancellationToken token = default, int expectedLength = 4) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object? LastArgument { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        { Calls++; return PyNone.Instance; }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(token);
            Assert.Equal(expectedLength, arguments.Length);
            LastArgument = arguments[^1].Value;
            return PyNone.Instance;
        }
    }
}
