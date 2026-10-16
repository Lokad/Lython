using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MethodBodyControlBoundaryTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan CallSpan = new(2000, 4, 90, 1);
    private const int MaximumSteps = 10000;
    // Fixed on original production f813ff54 before candidate qualification.
    private static readonly LythonSourceSpan[] OriginalCheckpoints =
    [
        new(2000,4,90,1), new(2000,4,90,1), new(32,8,3,3), new(39,1,3,10),
        new(43,151,4,3), new(52,8,4,12), new(52,5,4,12), new(52,8,4,12),
        new(52,8,4,12), new(58,1,4,18), new(52,8,4,12), new(52,8,4,12),
        new(43,151,4,3), new(65,57,5,4), new(68,4,5,7), new(68,1,5,7),
        new(71,1,5,10), new(126,50,8,4), new(132,4,8,10), new(142,24,9,5),
        new(150,16,9,13), new(150,5,9,13), new(150,16,9,13), new(150,16,9,13),
        new(156,7,9,19), new(156,7,9,19), new(164,1,9,27), new(43,151,4,3),
        new(65,57,5,4), new(68,4,5,7), new(68,1,5,7), new(71,1,5,10),
        new(78,44,6,5), new(103,19,7,13), new(103,5,7,13), new(103,19,7,13),
        new(103,19,7,13), new(109,10,7,19), new(109,10,7,19), new(120,1,7,30),
        new(43,151,4,3), new(65,57,5,4), new(68,4,5,7), new(68,1,5,7),
        new(71,1,5,10), new(126,50,8,4), new(132,4,8,10), new(142,24,9,5),
        new(150,16,9,13), new(150,5,9,13), new(150,16,9,13), new(150,16,9,13),
        new(156,7,9,19), new(156,7,9,19), new(164,1,9,27), new(184,10,11,8),
        new(192,2,11,16), new(204,6,12,10),
    ];
    private static readonly string[] OriginalEffects = Enumerable.Repeat("", 27).Concat(Enumerable.Repeat("value:0", 13)).Concat(Enumerable.Repeat("value:0,continue:1", 15)).Concat(Enumerable.Repeat("value:0,continue:1,value:2", 3)).ToArray();
    private static readonly string[] OriginalSnapshots = ["value:27,0,3,1", "continue:40,0,4,1", "value:55,0,3,1"];
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
    public async Task EveryFuelCutoffPreservesSourceEffectsAndScopeCleanup(bool asynchronous)
    {
        var full = await Define(asynchronous);
        full.Root.Limits.ExecutionStepCount = 0;
        var before = full.Root.MemoryGovernor.CurrentAccountedBytes;
        Assert.Equal(new BigInteger(12), await Invoke(full.Method, full.Root, asynchronous));
        var steps = full.Root.Limits.ExecutionStepCount;
        var funded = full.Root.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.Equal(58, steps);
        Assert.True(funded > 0);
        Assert.Equal(OriginalSnapshots, full.Tape.Snapshots);
        Assert.Equal(new[] { "value:0", "continue:1", "value:2" }, full.Tape.Events);
        AssertUnwound(full.Root);
        var spans = new List<LythonSourceSpan>();
        var effects = new List<string>();
        var paths = new List<string?>();
        var counts = new List<long>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            var fixture = await Define(asynchronous);
            fixture.Root.Limits.ExecutionStepCount = MaximumSteps - remaining;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Invoke(fixture.Method, fixture.Root, asynchronous));
            Assert.Equal($"maximum execution step count exceeded ({MaximumSteps})", error.Message);
            counts.Add(fixture.Root.Limits.ExecutionStepCount);
            paths.Add(error.SourcePath);
            Assert.Equal(MaximumSteps + 1, fixture.Root.Limits.ExecutionStepCount);
            Assert.Equal(remaining < 2 ? null : "/method-control.py", error.SourcePath);
            var span = error.Span.RequireNotNull();
            spans.Add(span);
            effects.Add(string.Join(',', fixture.Tape.Events));
            AssertUnwound(fixture.Root);
            Assert.Same(fixture.Active, fixture.Root.Services.CurrentException);
        }
        Assert.Equal(OriginalCheckpoints, spans);
        Assert.Equal(OriginalEffects, effects);
        output.WriteLine("CONTROL_QUALIFIED:" + JsonSerializer.Serialize(new
        {
            asynchronous, steps, funded, snapshots = full.Tape.Snapshots, spans, effects, paths, counts
        }));
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
            Snapshots.Add($"{phase}:{frame.Limits.ExecutionStepCount},{frame.MemoryGovernor.CurrentReservedBytes},{frame.Limits.CurrentInterpreterDepth},{frame.Limits.CurrentRecursionDepth}");
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
            MaxExecutionSteps = MaximumSteps, MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/method-control.py", CancellationToken = cancellation
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Services.SetCurrentException(active);
        var tape = new Tape(root, active, cancellation);
        root.Variables["stamp"] = tape;
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, root);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, root, default, [], null);
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
