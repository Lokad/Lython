using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class FunctionBodyTemporaryLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(2000, 4, 90, 1);

    [Fact]
    public async Task DiscardedCallResultIsNotRetainedAcrossLaterHostSuspension()
    {
        const string source = "class Runner:\n def run(self):\n  produce()\n  pause()\n  return 7\nrunner=Runner()\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) });
        var tape = new Tape(root);
        root.Variables["produce"] = new Produce(tape);
        root.Variables["pause"] = new Pause(tape);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        var receiver = Assert.IsType<PyInstance>(root.Variables["runner"]);
        Assert.True(receiver.Type.TryGetOwnMember("run", out var value));
        var method = Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(value).Bind(receiver));
        var pending = method.InvokeAsync([], Span, root).AsTask();
        await tape.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.NotNull(tape.Result);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(tape.Result.TryGetTarget(out _));
        }
        finally
        {
            tape.Release.TrySetResult();
            Assert.Equal(new BigInteger(7), await pending);
        }
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Tape(Context root)
    {
        internal Context Root => root;
        internal WeakReference<object>? Result { get; set; }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class Produce(Tape tape) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Same(tape.Root.Services, context.Services);
            var list = new PyList([], context.MemoryGovernor, span);
            tape.Result = new(list);
            return list;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
    }
    private sealed class Pause(Tape tape) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The async body must genuinely await the host.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Same(tape.Root.Services, context.Services);
            tape.Started.SetResult();
            await tape.Release.Task;
            return PyNone.Instance;
        }
    }
}
