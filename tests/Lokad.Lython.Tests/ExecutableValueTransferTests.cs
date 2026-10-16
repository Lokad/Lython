using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableValueTransferTests
{
    private static TheoryData<string, string, int> Cases => new()
    {
        { "return (23, left() + right())\n", "__add__", 3 },
        { "return (23, -left())\n", "__neg__", 2 },
        { "return (23, left()[right()])\n", "__getitem__", 3 },
        { "return (23, left()[1:2:3])\n", "__getitem__", 5 },
        { "v = left()\nv += right()\nreturn v\n", "__iadd__", 2 },
        { "for v in left():\n    return v\n", "__iter__", 1 },
    };

    public static IEnumerable<object[]> Operations => Cases.SelectMany(test => new[]
    {
        new object[] { test[0], test[1], test[2], false },
        new object[] { test[0], test[1], test[2], true },
    });

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task InputsRemainInActualSlotsAcrossCallbacks(string source, string method, int count, bool asynchronous)
    {
        var (code, context, probe) = PrepareOperation(source, method, count);
        var result = asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : Run(code, context);
        Assert.True(probe.Checked);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Empty(Operands(probe.Frame!));
        if (result is PyTuple tuple) Assert.Equal(new BigInteger(7), tuple[1]);
        else Assert.Equal(new BigInteger(7), result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FailedOrCancelledOperationReleasesFrameInputs(bool asynchronous, bool cancellation)
    {
        foreach (var augmented in new[] { false, true })
        {
            var (code, context, probe) = augmented
                ? PrepareOperation("v = left()\nv += right()\nreturn v\n", "__iadd__", 2)
                : PrepareOperation("return (23, left() + right())\n", "__add__", 3);
            probe.Failure = cancellation ? new OperationCanceledException() : new LythonRuntimeException("ValueError", "controlled operation failure", null);
            async Task Execute()
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                else Run(code, context);
            }
            if (cancellation) await Assert.ThrowsAnyAsync<OperationCanceledException>(Execute);
            else Assert.Same(probe.Failure, await Assert.ThrowsAsync<LythonRuntimeException>(Execute));
            Assert.True(probe.Checked);
            // The callback checked its live inputs; terminal frame cleanup also
            // withdraws the surrounding expression after cancellation.
            Assert.Empty(Operands(probe.Frame!));
            if (augmented)
            {
                var frame = Assert.IsAssignableFrom<LythonRuntime.ExecutableFrameState>(probe.Frame);
                Assert.True(frame.TryResolveOwnLocal("v", out var original));
                Assert.Same(probe.Left, original);
            }
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultReplacesInputsBeforeObservationAndDeniedResultIsWithdrawn(bool asynchronous, bool deny)
    {
        var (code, context, probe) = PrepareOperation("return (23, left() + right())\n", "__add__", 3, 100000);
        var result = new ObservedResult(context, deny);
        probe.Result = result;
        if (deny)
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                : Assert.Throws<LythonRuntimeException>(() => Run(code, context));
            Assert.Equal("MemoryError", failure.ExceptionType);
        }
        else Assert.Same(result, Assert.IsType<PyTuple>(asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context) : Run(code, context))[1]);
        Assert.True(result.Observed);
        Assert.Empty(Operands(probe.Frame!));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext, Probe) PrepareOperation(string source, string method, int count, long? cap = null)
    {
        var (code, context) = Prepare(source, cap);
        var probe = new Probe(context, count);
        if (method == "__iter__") probe.Result = new OneValueIterator();
        var type = new PyType("Operand", Array.Empty<PyType>(), new()
        {
            [method] = probe,
            ["__getattribute__"] = new Lookup(method, probe),
        });
        var left = new PyInstance(type);
        var payload = PyString.FromString(new string('x', 16000), context.MemoryGovernor);
        left.SetAttribute("payload", payload);
        probe.Left = left;
        probe.Payload = payload;
        context.Variables["left"] = new Factory(left);
        context.Variables["right"] = new Factory(new BigInteger(7));
        return (code, context, probe);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, long? cap = null)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.Empty(parsed.Diagnostics);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        context.Variables.Clear();
        return (ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!)), context);
    }

    private static object? Run(ExecutableScript code, LythonRuntime.ExecutionContext context)
        => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);

    private static object[] Operands(object frame)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var stack = frame.GetType().GetField("_stack", flags)!.GetValue(frame)!;
        var items = (object?[])stack.GetType().GetField("_items", flags)!.GetValue(stack)!;
        var count = (int)stack.GetType().GetProperty("Count")!.GetValue(stack)!;
        Assert.All(items.Skip(count), Assert.Null);
        return items.Take(count).Select(item => item!).ToArray();
    }

    private sealed class Factory(object value) : LythonRuntime.ICallable
    {
        // Test-control references are deliberately outside the guest graph.

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => value;
    }

    private sealed class Lookup(string method, Probe probe) : LythonRuntime.ICallable
    {

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => Assert.IsType<PyString>(arguments[0].Value).AsString() == method ? probe
                : throw new LythonRuntimeException("AttributeError", "missing test member", span);
    }

    private sealed class Probe(LythonRuntime.ExecutionContext context, int count) : LythonRuntime.ICallable
    {
        internal PyInstance Left = null!;
        internal PyString Payload = null!;
        internal object? Frame;
        internal bool Checked;
        internal Exception? Failure;
        internal object Result = new BigInteger(7);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext caller)
        {
            Frame = context.CurrentExecutableFrame;
            var operands = Operands(Frame!);
            Assert.Equal(count, operands.Length);
            Assert.Contains(operands, value => ReferenceEquals(value, Left));
            Checked = true;
            if (Failure is not null) throw Failure;
            return Result;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext caller)
        {
            await Task.Yield();
            return Invoke(arguments, span, caller);
        }
    }

    private sealed class ObservedResult(LythonRuntime.ExecutionContext context, bool deny) : IPyGovernedValue
    {
        internal bool Observed;
        public MemoryGovernor? OwnerMemoryGovernor
        {
            get
            {
                if (!Observed)
                {
                    var values = Operands(context.CurrentExecutableFrame!);
                    Assert.Equal(2, values.Length);
                    Assert.Equal(new BigInteger(23), values[0]);
                    Assert.Same(this, values[1]);
                    Observed = true;
                    if (deny) context.MemoryGovernor.Reserve(100000, null);
                }
                return null;
            }
        }
        public LythonSourceSpan? AllocationSpan => null;
    }

    private sealed class OneValueIterator : IPyIteratorValue
    {
        private bool _advanced;
        public bool TryMoveNext(out object value)
        {
            value = new BigInteger(7);
            if (_advanced) return false;
            _advanced = true;
            return true;
        }
        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value)) yield return value;
        }
    }
}
