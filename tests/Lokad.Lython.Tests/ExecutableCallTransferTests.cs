using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Post-completion checks inspect returned argument buffers before another
// execution can rent those thread-local arrays again.
[Collection("AllocationSensitive")]
public sealed class ExecutableCallTransferTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

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
        var stack = frame.GetType().GetField("_stack", Fields)!.GetValue(frame)!;
        var items = (object?[])stack.GetType().GetField("_items", Fields)!.GetValue(stack)!;
        var count = (int)stack.GetType().GetProperty("Count")!.GetValue(stack)!;
        // The physical tail must not retain withdrawn values either.
        Assert.All(items.Skip(count), Assert.Null);
        return items.Take(count).Select(item => item!).ToArray();
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
