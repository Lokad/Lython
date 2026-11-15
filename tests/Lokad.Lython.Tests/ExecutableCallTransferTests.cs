using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Invocation-private packets stay live through suspension and release their
// inputs when the actual call finishes, independently of CLR collection.
public sealed class ExecutableCallTransferTests
{

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CallInputsStayPublishedUntilCompletion(bool asynchronous, bool fail)
    {
        var (code, context) = Prepare("marker = 23\nreturn (marker, probe('x' * 16000, amount=7))\n");
        var probe = new Probe(fail);
        context.Variables["probe"] = probe;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            probe.Check(context);
            probe.Release.SetResult();
            if (fail) Assert.Equal("ValueError", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).ExceptionType);
            else CheckResult(await pending);
        }
        else if (fail)
            Assert.Equal("ValueError", Assert.Throws<LythonRuntimeException>(() => Run(code, context)).ExceptionType);
        else CheckResult(Run(code, context));
        Assert.NotNull(probe.Frame);
        Assert.Empty(Operands(probe.Frame!));
        probe.AssertReleasedArguments();
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public async Task CancelledSuspensionReleasesFrameInputs()
    {
        var (code, context) = Prepare("marker = 23\nreturn (marker, probe('x' * 16000, amount=7))\n");
        var probe = new Probe(false);
        context.Variables["probe"] = probe;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        probe.Release.SetCanceled();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        // The call checked its live inputs during suspension. Terminal cleanup
        // must also withdraw the surrounding expression before frame exit.
        Assert.Empty(Operands(probe.Frame!));
        probe.AssertReleasedArguments();
        Assert.Null(context.CurrentExecutableFrame);
    }

    private static (ExecutableScript Code, LythonRuntime.ExecutionContext Context) Prepare(string source, long? cap = null)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return (ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())),
            new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) }));
    }

    private static object? Run(ExecutableScript code, LythonRuntime.ExecutionContext context)
        => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);

    private static void CheckResult(object? result)
    {
        var tuple = Assert.IsType<PyTuple>(result);
        Assert.Equal(new BigInteger(23), tuple[0]);
        Assert.Equal(new BigInteger(16007), tuple[1]);
    }

    private static object[] Operands(object frame)
    {
        var context = (LythonRuntime.ExecutionContext)frame;
        var stack = context._execution.Stack;
        var items = stack.Values;
        var count = stack.LiveExtent;
        if (items is null)
        {
            Assert.Equal(0, count);
            Assert.Empty(context._execution.Locals);
            Assert.Null(context._execution.Code);
            return [];
        }
        var first = context._execution.Code.FirstTemporarySlot;
        // Inspect the actual backing before projecting live values.
        Assert.All(items.Skip(count), item => Assert.Null(item.Reference));
        var values = new object[count - first];
        for (var i = 0; i < values.Length; i++) values[i] = items[first + i].Object.RequireNotNull();
        return values;
    }

    private sealed class Probe(bool fail) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal object? Frame;
        private CallArgumentValue[]? _arguments;

        internal void AssertReleasedArguments()
            => Assert.All(_arguments!, argument => Assert.True(argument.Value is null or PyNone));

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _arguments = arguments;
            Check(context);
            return Complete(span);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _arguments = arguments;
            try { Check(context); }
            finally { Started.TrySetResult(); }
            await Release.Task.ConfigureAwait(false);
            Check(context);
            return Complete(span);
        }

        internal void Check(LythonRuntime.ExecutionContext context)
        {
            Assert.NotNull(_arguments);
            Assert.Equal(2, _arguments.Length);
            Assert.True(_arguments[0].IsPositional);
            Assert.Equal(16000, Assert.IsType<PyString>(_arguments[0].Value).AsString().Length);
            Assert.Equal("amount", _arguments[1].KeywordName);
            Assert.Equal(new BigInteger(7), _arguments[1].Value);
            Frame = context.CurrentExecutableFrame;
            Assert.NotNull(Frame);
            var values = Operands(Frame!);
            Assert.Equal(4, values.Length);
            Assert.Equal(new BigInteger(23), values[0]);
            Assert.Same(this, values[1]);
            Assert.Same(_arguments[0].Value, values[2]);
            Assert.Equal(new BigInteger(7), values[3]);
        }

        private object Complete(LythonSourceSpan span) => fail
            ? throw new LythonRuntimeException("ValueError", "callback failure", span)
            : new BigInteger(16007);
    }
}
