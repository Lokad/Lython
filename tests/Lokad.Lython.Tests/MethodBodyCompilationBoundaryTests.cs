using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MethodBodyCompilationBoundaryTests
{
    private static readonly LythonSourceSpan CallSpan = new(4000, 4, 99, 3);
    private const string ClosureSource = """
        class Runner:
         def make(self):
          saved=stamp('saved',[17])
          @decorate('factory')
          def middle(value=stamp('default',saved)):
           def leaf():return value,saved,__class__
           return stamp('leaf',leaf)
          return middle()
         def identity(self):return self
        runner=Runner()
        """;
    private const string GenericSource = """
        class Runner:
         def make(self):
          captured=[17]
          class Inner[T]:
           @decorate('top')
           @decorate('bottom')
           def run[U](self,value:stamp('argument',U)=stamp('default',captured))->stamp('result',T):
            'method documentation'
            def leaf():return captured,value,__class__
            return stamp('body',leaf)
          return Inner
         def identity(self):return self
        runner=Runner()
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedMethodClosuresKeepFundingAndAliases(bool asynchronous)
    {
        var fixture = await Define(ClosureSource, asynchronous);
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        var leaf = Assert.IsAssignableFrom<PyFunctionBase>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.True(funded > 0);
        Assert.Equal(new[] { "saved", "factory", "default", "factory-apply", "leaf" }, fixture.Tape.Events);
        AssertUnwound(fixture.Context);
        var retained = Assert.IsType<PyTuple>(await Invoke(leaf, fixture.Context, asynchronous));
        Assert.Same(fixture.Tape.Saved, retained[0]);
        Assert.Same(retained[0], retained[1]);
        Assert.Same(fixture.Type, retained[2]);
        Assert.Equal(new BigInteger(17), Assert.Single(Assert.IsType<PyList>(retained[0])));
        Assert.Same(fixture.Context.MemoryGovernor, fixture.Tape.Saved!.OwnerMemoryGovernor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericMethodDefinitionsRetainDefaultsTypeScopesAndRealClassCells(bool asynchronous)
    {
        var fixture = await Define(GenericSource, asynchronous);
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        var inner = Assert.IsType<PyType>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.True(funded > 0);
        Assert.Equal(new[] { "top", "bottom", "default", "argument", "result", "bottom-apply", "top-apply" }, fixture.Tape.Events);
        var expectedSnapshots = new[]
        {
            // Decorators own 96 bytes; the definition's one-default map
            // owns another 128 until publication finishes. Frames remain active.
            "top:96,3,1", "bottom:96,3,1", "default:224,3,1",
            "argument:224,3,1", "result:224,3,1",
            "bottom-apply:224,2,1", "top-apply:224,2,1"
        };
        Assert.True(expectedSnapshots.SequenceEqual(fixture.Tape.Snapshots), string.Join("\n", fixture.Tape.Snapshots));
        var receiver = Assert.IsType<PyInstance>(inner.Invoke([], CallSpan, fixture.Context));
        var method = Bind(receiver, "run");
        var function = Assert.IsAssignableFrom<PyFunctionBase>(method.Function);
        Assert.Same(inner, function.OwnerType);
        Assert.True(function.TryGetMember("__doc__", out var doc));
        Assert.Equal("method documentation", doc.ToString());
        Assert.True(function.TryGetMember("__type_params__", out var parameters));
        Assert.Same(fixture.Tape.Values["argument"], Assert.IsType<PyTuple>(parameters)[0]);
        Assert.True(inner.TryGetOwnMember("__type_params__", out var classParameters));
        Assert.Same(fixture.Tape.Values["result"], Assert.IsType<PyTuple>(classParameters)[0]);
        Assert.True(function.TryGetMember("__annotations__", out var annotations));
        var mapping = Assert.IsType<PyDict>(annotations);
        Assert.Same(fixture.Tape.Values["argument"], mapping.GetItem(Lokad.Lython.Runtime.Text.PyString.FromString("value")));
        Assert.Same(fixture.Tape.Values["result"], mapping.GetItem(Lokad.Lython.Runtime.Text.PyString.FromString("return")));
        var leaf = Assert.IsAssignableFrom<PyFunctionBase>(await Invoke(method, fixture.Context, asynchronous));
        var retained = Assert.IsType<PyTuple>(await Invoke(leaf, fixture.Context, asynchronous));
        Assert.Same(fixture.Tape.Saved, retained[0]);
        Assert.Same(retained[0], retained[1]);
        Assert.Same(inner, retained[2]);
        Assert.Equal(new BigInteger(17), Assert.Single(Assert.IsType<PyList>(retained[0])));
        AssertUnwound(fixture.Context);
    }

    [Theory]
    [InlineData("top", false)]
    [InlineData("bottom", false)]
    [InlineData("default", false)]
    [InlineData("argument", false)]
    [InlineData("result", false)]
    [InlineData("bottom-apply", false)]
    [InlineData("top-apply", false)]
    [InlineData("body", false)]
    [InlineData("top", true)]
    [InlineData("bottom", true)]
    [InlineData("default", true)]
    [InlineData("argument", true)]
    [InlineData("result", true)]
    [InlineData("bottom-apply", true)]
    [InlineData("top-apply", true)]
    [InlineData("body", true)]
    public async Task DefinitionAndBodyCallbacksActuallySuspendWithTheirRealFrames(string pausedPhase, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = await Define(GenericSource, true, cancellation.Token);
        fixture.Tape.PausedPhase = pausedPhase;
        fixture.Tape.RequireAsync = true;
        var pending = Invoke(fixture.Method, fixture.Context, true);
        PyInstance? innerReceiver = null;
        if (pausedPhase == "body")
        {
            var inner = Assert.IsType<PyType>(await pending);
            innerReceiver = Assert.IsType<PyInstance>(inner.Invoke([], CallSpan, fixture.Context));
            pending = Invoke(Bind(innerReceiver, "run"), fixture.Context, true);
        }
        var frame = await fixture.Tape.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(pending.IsCompleted);
        Assert.NotSame(fixture.Context, frame);
        Assert.Same(fixture.Context.Services, frame.Services);
        Assert.Equal("/method-body.py", frame.SourcePath);
        Assert.Null(frame.CurrentExecutableFrame);
        Assert.True(frame.Limits.CurrentRecursionDepth > 0);
        var depth = frame.Limits.CurrentRecursionDepth;
        var phaseCount = fixture.Tape.Events.Count;
        Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, true));
        Assert.Equal(depth, frame.Limits.CurrentRecursionDepth);
        Assert.Equal(phaseCount, fixture.Tape.Events.Count);
        if (innerReceiver is not null) Assert.Same(innerReceiver, frame.Variables["self"]);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Equal(phaseCount, fixture.Tape.Events.Count);
        }
        else
        {
            fixture.Tape.Release.SetResult();
            Assert.NotNull(await pending);
        }
        AssertUnwound(fixture.Context);
        Assert.Same(fixture.Active, fixture.Context.Services.CurrentException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedMethodGeneratorsKeepTheirOriginalDefaultAndClassCell(bool asynchronous)
    {
        const string source = "class Runner:\n def make(self):\n  saved=stamp('saved',[17])\n  def values(value=stamp('default',saved)):\n   yield value\n   yield __class__\n  return values()\n def identity(self):return self\nrunner=Runner()\n";
        var fixture = await Define(source, asynchronous);
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        var generator = Assert.IsType<LythonRuntime.PyGenerator>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.True(funded > 0);
        Assert.Same(fixture.Context.MemoryGovernor, generator.OwnerMemoryGovernor);
        Assert.Equal(new[] { "saved", "default" }, fixture.Tape.Events);
        AssertUnwound(fixture.Context);
        var first = asynchronous ? await generator.TryMoveNextAsync()
            : generator.TryMoveNext(out var firstValue) ? PyIterationResult.Yield(firstValue) : PyIterationResult.End;
        Assert.True(first.HasValue);
        Assert.Same(fixture.Tape.Saved, first.Value);
        Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, asynchronous));
        var second = asynchronous ? await generator.TryMoveNextAsync()
            : generator.TryMoveNext(out var secondValue) ? PyIterationResult.Yield(secondValue) : PyIterationResult.End;
        Assert.True(second.HasValue);
        Assert.Same(fixture.Type, second.Value);
        var final = asynchronous ? await generator.TryMoveNextAsync()
            : generator.TryMoveNext(out var finalValue) ? PyIterationResult.Yield(finalValue) : PyIterationResult.End;
        Assert.False(final.HasValue);
        AssertUnwound(fixture.Context);
        Assert.Same(fixture.Active, fixture.Context.Services.CurrentException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitionBudgetDenialsReleaseReservationsAndKeepCallerBindings(bool asynchronous)
    {
        foreach (var allowance in new long[] { 0, 1024, 12288 })
        {
            var fixture = await Define(GenericSource, asynchronous);
            var governor = fixture.Context.MemoryGovernor;
            var padding = 1024 * 1024 - governor.CurrentAccountedBytes - allowance;
            governor.Reserve(padding, CallSpan);
            governor.Commit(padding);
            LythonRuntimeException? error = null;
            try
            {
                try { await Invoke(fixture.Method, fixture.Context, asynchronous); }
                catch (LythonRuntimeException denied) { error = denied; }
                if (allowance == 12288)
                {
                    Assert.Null(error);
                    Assert.Equal(new[] { "top", "bottom", "default", "argument", "result", "bottom-apply", "top-apply" }, fixture.Tape.Events);
                }
                else
                {
                    Assert.NotNull(error);
                    Assert.Equal("MemoryError", error.ExceptionType);
                    Assert.Equal("shared memory budget exceeded (1048576)", error.Message);
                    if (error.Span is { } span)
                        Assert.True(span == CallSpan || (span.Start >= 0 && span.Start + span.Length <= GenericSource.Length));
                    var phases = new[] { "top", "bottom", "default", "argument", "result", "bottom-apply", "top-apply" };
                    Assert.Equal(phases.Take(fixture.Tape.Events.Count), fixture.Tape.Events);
                }
                AssertUnwound(fixture.Context);
                Assert.Same(fixture.Receiver, fixture.Context.Variables["runner"]);
                Assert.Same(fixture.Active, fixture.Context.Services.CurrentException);
            }
            finally { governor.Release(padding); }
            if (governor.AdmissionFailure is null)
                Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, asynchronous));
            else
                Assert.Same(governor.AdmissionFailure, await Assert.ThrowsAsync<LythonRuntimeException>(
                    async () => await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, asynchronous)));
            AssertUnwound(fixture.Context);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompoundDepthDenialPrecedesConditionEffectsAndRestoresAdmission(bool asynchronous)
    {
        const string source = "class Runner:\n def make(self):\n  while stamp('condition',False):pass\n def identity(self):return self\nrunner=Runner()\n";
        var fixture = await Define(source, asynchronous);
        fixture.Context.Limits.CurrentInterpreterDepth = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        try
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Invoke(fixture.Method, fixture.Context, asynchronous));
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal("maximum interpreter stack depth exceeded", error.Message);
            Assert.Equal(new LythonSourceSpan(33, 35, 3, 3), error.Span);
            Assert.Empty(fixture.Tape.Events);
            Assert.Equal(LythonRuntime.ExecutionLimits.MaxInterpreterDepth, fixture.Context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, fixture.Context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, fixture.Context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { fixture.Context.Limits.CurrentInterpreterDepth = 0; }
        Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, asynchronous));
        AssertUnwound(fixture.Context);
    }

    private sealed class Tape(Context root, PyException active, CancellationToken cancellation)
    {
        public List<string> Events { get; } = [];
        public List<string> Snapshots { get; } = [];
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
        public PyList? Saved { get; private set; }
        public string? PausedPhase { get; set; }
        public bool RequireAsync { get; set; }
        public TaskCompletionSource<Context> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Record(string phase, object value, Context frame)
        {
            Assert.Same(root.Services, frame.Services);
            Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
            Assert.Same(active, frame.Services.CurrentException);
            Assert.Null(frame.CurrentExecutableFrame);
            Events.Add(phase);
            Values[phase] = value;
            Snapshots.Add($"{phase}:{frame.MemoryGovernor.CurrentReservedBytes},{frame.Limits.CurrentInterpreterDepth},{frame.Limits.CurrentRecursionDepth}");
            if (phase is "saved" or "default") Saved = Assert.IsType<PyList>(value);
            return value;
        }
        public async ValueTask<object> RecordAsync(string phase, object value, Context frame)
        {
            Record(phase, value, frame);
            if (phase == PausedPhase)
            {
                Started.SetResult(frame);
                await Release.Task.WaitAsync(cancellation);
            }
            else await Task.Yield();
            return value;
        }
        public sealed class Callback(Tape tape, bool decorator, string? applying = null) : LythonRuntime.ICallable
        {
            private (string Phase, object Value) Input(CallArgumentValue[] arguments)
            {
                if (applying is not null) return (applying + "-apply", arguments[0].Value);
                var phase = arguments[0].Value.ToString()!;
                return (phase, decorator ? new Callback(tape, false, phase) : arguments[1].Value);
            }
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            {
                Assert.False(tape.RequireAsync);
                var input = Input(arguments);
                return tape.Record(input.Phase, input.Value, context);
            }
            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            {
                var input = Input(arguments);
                return tape.RecordAsync(input.Phase, input.Value, context);
            }
        }
    }

    private sealed record Fixture(Context Context, PyType Type, PyInstance Receiver, PyBoundMethod Method, Tape Tape, PyException Active);
    private static async Task<Fixture> Define(string source, bool asynchronous, CancellationToken cancellation = default)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(1024 * 1024),
            SourcePath = "/method-body.py", CancellationToken = cancellation
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var tape = new Tape(context, active, cancellation);
        context.Variables["stamp"] = new Tape.Callback(tape, false);
        context.Variables["decorate"] = new Tape.Callback(tape, true);
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, context);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, context, default, null);
        var type = Assert.IsType<PyType>(context.Variables["Runner"]);
        var receiver = Assert.IsType<PyInstance>(context.Variables["runner"]);
        AssertUnwound(context);
        return new(context, type, receiver, Bind(receiver, "make"), tape, active);
    }
    private static PyBoundMethod Bind(PyInstance receiver, string name)
    {
        Assert.True(receiver.Type.TryGetOwnMember(name, out var value));
        return Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(value).Bind(receiver));
    }
    private static ValueTask<object> Invoke(LythonRuntime.ICallable callable, Context context, bool asynchronous)
        => asynchronous ? callable.InvokeAsync([], CallSpan, context) : ValueTask.FromResult(callable.Invoke([], CallSpan, context));
    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
    }
}
