using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MethodBodyControlBoundaryTests
{
    private static readonly LythonSourceSpan CallSpan = new(2000, 4, 90, 1);
    private static readonly string[] OriginalSnapshots = ["value:0,3,1", "continue:0,4,1", "value:0,3,1"];
    private const string Source = """
        class Runner:
         def run(self):
          result=0
          for i in range(3):
           if i==1:
            try:continue
            finally:stamp('continue',i)
           while True:
            result+=stamp('value',i)
            break
          else:result+=10
          return result
         def identity(self):return self
        runner=Runner()
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MethodBodiesPreserveEffectsAndScopeCleanup(bool asynchronous)
    {
        var full = await Define(asynchronous);
        var before = full.Root.MemoryGovernor.CurrentAccountedBytes;
        Assert.Equal(new BigInteger(12), await Invoke(full.Method, full.Root, asynchronous));
        var funded = full.Root.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.True(funded > 0);
        Assert.Equal(OriginalSnapshots, full.Tape.Snapshots);
        Assert.Equal(new[] { "value:0", "continue:1", "value:2" }, full.Tape.Events);
        AssertUnwound(full.Root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealHostPauseSupportsSameRunReentryAndCancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = await Define(true, cancellation.Token);
        fixture.Tape.Pause = true;
        var pending = Invoke(fixture.Method, fixture.Root, true).AsTask();
        var frame = await fixture.Tape.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Same(fixture.Root.Services, frame.Services);
            Assert.Same(fixture.Root.MemoryGovernor, frame.MemoryGovernor);
            Assert.Same(fixture.Active, frame.Services.CurrentException);
            Assert.Null(frame.CurrentExecutableFrame);
            var depth = frame.Limits.CurrentInterpreterDepth;
            var recursion = frame.Limits.CurrentRecursionDepth;
            Assert.True(depth > 1);
            Assert.True(recursion > 0);
            Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Root, true));
            Assert.Equal(depth, frame.Limits.CurrentInterpreterDepth);
            Assert.Equal(recursion, frame.Limits.CurrentRecursionDepth);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                fixture.Tape.Release.SetResult();
                Assert.Equal(new BigInteger(12), await pending);
            }
        }
        finally
        {
            fixture.Tape.Release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        AssertUnwound(fixture.Root);
        Assert.Same(fixture.Active, fixture.Root.Services.CurrentException);
    }

    private sealed class Tape(Context root, PyException active, CancellationToken cancellation) : LythonRuntime.ICallable
    {
        internal List<string> Events { get; } = [];
        internal List<string> Snapshots { get; } = [];
        internal bool Pause { get; set; }
        internal TaskCompletionSource<Context> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context frame)
        {
            Assert.False(Pause);
            Assert.Same(root.Services, frame.Services);
            Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
            Assert.Same(active, frame.Services.CurrentException);
            Assert.Null(frame.CurrentExecutableFrame);
            var phase = arguments[0].Value.ToString()!;
            Events.Add($"{phase}:{arguments[1].Value}");
            Snapshots.Add($"{phase}:{frame.MemoryGovernor.CurrentReservedBytes},{frame.Limits.CurrentInterpreterDepth},{frame.Limits.CurrentRecursionDepth}");
            return arguments[1].Value;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context frame)
        {
            var pause = Pause;
            Pause = false;
            var value = Invoke(arguments, span, frame);
            Pause = pause;
            if (pause && !Started.Task.IsCompleted)
            {
                Started.SetResult(frame);
                await Release.Task.WaitAsync(cancellation);
            }
            else await Task.Yield();
            return value;
        }
    }

    private sealed record Fixture(Context Root, PyInstance Receiver, PyBoundMethod Method, Tape Tape, PyException Active);
    private static async Task<Fixture> Define(bool asynchronous, CancellationToken cancellation = default)
    {
        var frontend = LythonFrontend.Compile(Source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/method-control.py", CancellationToken = cancellation
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Services.SetCurrentException(active);
        var tape = new Tape(root, active, cancellation);
        root.Variables["stamp"] = tape;
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, root);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, root, default, null);
        var receiver = Assert.IsType<PyInstance>(root.Variables["runner"]);
        AssertUnwound(root);
        return new(root, receiver, Bind(receiver, "run"), tape, active);
    }
    private static PyBoundMethod Bind(PyInstance receiver, string name)
    {
        Assert.True(receiver.Type.TryGetOwnMember(name, out var member));
        return Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(member).Bind(receiver));
    }
    private static ValueTask<object> Invoke(LythonRuntime.ICallable method, Context root, bool asynchronous)
        => asynchronous ? method.InvokeAsync([], CallSpan, root) : ValueTask.FromResult(method.Invoke([], CallSpan, root));
    private static void AssertUnwound(Context root)
    {
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(root.CurrentExecutableFrame);
    }
}
