using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeAggregateStateTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void AggregateShellFundingCoversItsMeasuredStorageAndRetiresItsReferences()
    {
        var context = Context();
        var create = CreateAggregate();
        var source = new PyList();
        var total = PyString.FromString("retained");
        using ((IDisposable)create(source, total, context, Span)) { }
        var initial = context.MemoryGovernor.CurrentReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var operation = create(source, total, context, Span);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var admitted = context.MemoryGovernor.CurrentReservedBytes - initial;
        Assert.True(admitted >= allocated, $"admitted {admitted}, allocated {allocated}");
        before = GC.GetAllocatedBytesForCurrentThread();
        ((IDisposable)operation).Dispose();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        Assert.Equal(initial, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Same(PyNone.Instance, ((LythonRuntime.NativeIterableOperation)operation).Source);
        Assert.Same(PyNone.Instance, ((LythonRuntime.NativeIterableOperation)operation).Result);
        Assert.Same(PyNone.Instance, ((LythonRuntime.NativeIterableOperation)operation).Current);
    }

    [Fact]
    public void DeniedAggregateAdmissionPreservesTheExecutableFrame()
    {
        var context = Context();
        var create = CreateAggregate();
        var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        var funding = context.MemoryGovernor.MaxAccountedBytes!.Value - context.MemoryGovernor.CurrentCommittedBytes;
        context.MemoryGovernor.Reserve(funding, Span);
        var initial = context.MemoryGovernor.CurrentReservedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() => create(PyNone.Instance, BigInteger.Zero, context, Span));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Same(previous, context.CurrentExecutableFrame);
        Assert.Equal(initial, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.ReleaseReserved(funding);
        context.LeaveExecutableSlots(null);
    }

    private static Func<object, object, LythonRuntime.ExecutionContext, LythonSourceSpan, LythonRuntime.NativeIterableOperation> CreateAggregate()
        => typeof(LythonRuntime).GetNestedType("NativeIterableOperation", BindingFlags.NonPublic)!
            .GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<object, object, LythonRuntime.ExecutionContext, LythonSourceSpan, LythonRuntime.NativeIterableOperation>>();

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
    public async Task IteratorCleanupKeepsOperationFundingUntilCompletion(bool asynchronous, bool fail)
    {
        var context = Context();
        var source = new CleanupSource(context, fail);
        var sum = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["sum"]);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        if (fail)
        {
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(async () => await sum.InvokeAsync(arguments, Span, context));
            else Assert.Throws<InvalidOperationException>(() => sum.Invoke(arguments, Span, context));
        }
        else Assert.Equal(BigInteger.One, asynchronous ? await sum.InvokeAsync(arguments, Span, context) : sum.Invoke(arguments, Span, context));
        Assert.True(source.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultRegistrationKeepsOperationFundingUntilCompletion(bool asynchronous, bool fail)
    {
        var context = Context();
        var sum = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["sum"]);
        var value = new RegisteredValue(context, fail);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(new PyList()), CallArgumentValue.Positional(value)];
        if (fail)
        {
            var failure = asynchronous ? await Assert.ThrowsAsync<InvalidOperationException>(async () => await sum.InvokeAsync(arguments, Span, context))
                : Assert.Throws<InvalidOperationException>(() => sum.Invoke(arguments, Span, context));
            Assert.Equal("controlled registration failure", failure.Message);
        }
        else Assert.Same(value, asynchronous ? await sum.InvokeAsync(arguments, Span, context) : sum.Invoke(arguments, Span, context));
        Assert.True(value.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IteratorFailureRefundsOperationWithoutReplacingTheExecutableFrame(bool asynchronous)
    {
        var context = Context();
        var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        var sum = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["sum"]);
        var source = new FailingSource(context);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        if (asynchronous) await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sum.InvokeAsync(arguments, Span, context));
        else Assert.ThrowsAny<OperationCanceledException>(() => sum.Invoke(arguments, Span, context));
        Assert.Same(previous, context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(source.Checked);
        context.LeaveExecutableSlots(null);
    }

    private static object? Run(ExecutableScript code, LythonRuntime.ExecutionContext context)
        => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);

    private static ExecutableScript Compile(string source)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.Empty(parsed.Diagnostics);
        return ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
    }

    private static LythonRuntime.ExecutionContext Context()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
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

    private sealed class RegisteredValue(LythonRuntime.ExecutionContext context, bool fail) : IPyOwnershipSnapshot
    {
        internal bool Checked;
        public bool TrySnapshotOwnership(out long chargeBytes)
        {
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 128);
            Checked = true;
            if (fail) throw new InvalidOperationException("controlled registration failure");
            chargeBytes = 0;
            return false;
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

    private sealed class CleanupSource(LythonRuntime.ExecutionContext context, bool fail) : IPyAsyncIterableValue
    {
        internal bool Checked;
        private void Check()
        {
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 128);
            Checked = true;
            if (fail) throw new InvalidOperationException("controlled iterator cleanup failure");
        }
        public IEnumerable<object> Iterate()
        {
            try { yield return BigInteger.One; }
            finally { Check(); }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            try
            {
                await Task.Yield();
                yield return BigInteger.One;
            }
            finally { Check(); }
        }
    }

    private sealed class FailingSource(LythonRuntime.ExecutionContext context) : IPyAsyncIterableValue
    {
        internal bool Checked;
        private void Check()
        {
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 128);
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
