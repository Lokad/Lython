using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeAggregateStateTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void DeniedListAdmissionPreservesTheExecutableFrame()
    {
        var context = Context();
        var list = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["list"]);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(PyTuple.Empty)];
        var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        var funding = ((LythonMemoryBudget)context.MemoryGovernor.Budget!).LimitBytes!.Value - context.MemoryGovernor.CurrentCommittedBytes;
        context.MemoryGovernor.Reserve(funding, Span);
        var initial = context.MemoryGovernor.CurrentReservedBytes;
        try
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => list.Invoke(arguments, Span, context));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Same(previous, context.CurrentExecutableFrame);
            Assert.Equal(initial, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            context.MemoryGovernor.ReleaseReserved(funding);
            context.LeaveExecutableSlots(null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedSumRestoresTheOuterAccumulator(bool asynchronous)
    {
        var context = Context();
        var probe = new Probe();
        context.Variables["probe"] = probe;
        context.Variables["sum"] = context.State.BuiltinVariables["sum"];
        var code = Compile("class Total:\n    def __init__(self):\n        self.payload = 'x' * 16000\n    def __add__(self, item):\n        sum([1, 2, 3])\n        probe()\n        return self\nreturn sum([1], Total()).payload\n");
        var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : Run(code, context);
        Assert.Equal(16000, Assert.IsType<PyString>(result).Utf8Bytes.Length);
        Assert.True(probe.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvolvingAccumulatorSurvivesGeneratorReentry(bool asynchronous)
    {
        var context = Context();
        var probe = new Probe();
        context.Variables["probe"] = probe;
        context.Variables["sum"] = context.State.BuiltinVariables["sum"];
        var code = Compile("class Total:\n    def __init__(self, payload):\n        self.payload = payload\n    def __add__(self, item):\n        return self\nclass First:\n    def __radd__(self, ignored):\n        return Total('x' * 16000)\ndef values():\n    yield First()\n    probe()\n    yield 1\nreturn sum(values()).payload\n");
        var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : Run(code, context);
        Assert.Equal(16000, Assert.IsType<PyString>(result).Utf8Bytes.Length);
        Assert.True(probe.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdditionProtocolReceivesTheCurrentItem(bool asynchronous)
    {
        var context = Context();
        var probe = new ItemProbe();
        context.Variables["probe"] = probe;
        context.Variables["sum"] = context.State.BuiltinVariables["sum"];
        var code = Compile("class Total:\n    def __add__(self, item):\n        probe()\n        return item\nreturn sum(['x' * 16000], Total())\n");
        var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : Run(code, context);
        Assert.Equal(16000, Assert.IsType<PyString>(result).Utf8Bytes.Length);
        Assert.True(probe.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task IteratorCleanupKeepsRetainedStorageFundedUntilCompletion(bool asynchronous, bool fail)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = Context(owner);
            var source = new CleanupSource(context, budget, fail);
            var sum = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["sum"]);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
            if (asynchronous)
            {
                var pending = sum.InvokeAsync(arguments, Span, context).AsTask();
                try
                {
                    await source.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes >= source.MinimumFunding);
                    source.ReleaseCleanup.SetResult();
                    if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
                    else Assert.Equal(BigInteger.One, await pending);
                }
                finally
                {
                    source.ReleaseCleanup.TrySetResult();
                    try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (InvalidOperationException) when (fail) { }
                }
            }
            else if (fail) Assert.Throws<InvalidOperationException>(() => sum.Invoke(arguments, Span, context));
            else Assert.Equal(BigInteger.One, sum.Invoke(arguments, Span, context));
            Assert.True(source.Checked);
            Assert.Equal(1, source.CleanupCalls);
            Assert.Equal(4096, source.Retained.Utf8Bytes.Length);
            Assert.True(budget.ReservedBytes >= source.MinimumFunding);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IteratorFailurePreservesFundingAndTheExecutableFrame(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = Context(owner);
            var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
            context.EnterExecutableSlots(previous);
            var sum = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["sum"]);
            var source = new FailingSource(context, budget);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
            try
            {
                if (asynchronous) await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sum.InvokeAsync(arguments, Span, context));
                else Assert.ThrowsAny<OperationCanceledException>(() => sum.Invoke(arguments, Span, context));
                Assert.Same(previous, context.CurrentExecutableFrame);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.True(source.Checked);
                Assert.True(budget.ReservedBytes >= source.MinimumFunding);
            }
            finally { context.LeaveExecutableSlots(null); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object? Run(ExecutableScript code, LythonRuntime.ExecutionContext context)
        => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);

    private static ExecutableScript Compile(string source)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.Empty(parsed.Diagnostics);
        return ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
    }

    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget? owner = null)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        context.Variables.Clear();
        return context;
    }



    private sealed class Probe : LythonRuntime.ICallable
    {
        internal bool Checked;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Checked = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class ItemProbe : LythonRuntime.ICallable
    {
        internal bool Checked;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Checked = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class CleanupSource(LythonRuntime.ExecutionContext context, LythonMemoryBudget budget, bool fail) : IPyAsyncIterableValue
    {
        internal bool Checked;
        internal int CleanupCalls;
        internal readonly PyString Retained = PyString.FromString(new string('x', 4096), context.MemoryGovernor, Span);
        internal readonly long MinimumFunding = budget.ReservedBytes;
        internal readonly TaskCompletionSource CleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private void Check()
        {
            Assert.True(budget.ReservedBytes >= MinimumFunding);
            Assert.Equal(new string('x', 4096), Retained.AsString());
            Checked = true;
            if (fail) throw new InvalidOperationException("controlled iterator cleanup failure");
        }
        public IEnumerable<object> Iterate()
        {
            try { yield return BigInteger.One; }
            finally { CleanupCalls++; Check(); }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            try
            {
                await Task.Yield();
                yield return BigInteger.One;
            }
            finally
            {
                CleanupCalls++;
                CleanupStarted.SetResult();
                await ReleaseCleanup.Task.ConfigureAwait(false);
                Check();
            }
        }
    }

    private sealed class FailingSource(LythonRuntime.ExecutionContext context, LythonMemoryBudget budget) : IPyAsyncIterableValue
    {
        internal bool Checked;
        internal readonly PyString Retained = PyString.FromString(new string('x', 4096), context.MemoryGovernor, Span);
        internal readonly long MinimumFunding = budget.ReservedBytes;
        private void Check()
        {
            Assert.True(budget.ReservedBytes >= MinimumFunding);
            Assert.Equal(new string('x', 4096), Retained.AsString());
            Checked = true;
            throw new OperationCanceledException();
        }
        public IEnumerable<object> Iterate()
        {
            Check();
            yield return BigInteger.One;
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.Yield();
            Check();
            yield return BigInteger.One;
        }
    }
}
