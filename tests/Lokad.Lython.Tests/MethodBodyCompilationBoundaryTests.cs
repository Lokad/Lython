using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MethodBodyCompilationBoundaryTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan CallSpan = new(4000, 4, 99, 3);
    private const int MaximumSteps = 10000;
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
    // Recorded on production f006f62c before any method-body compiler change.
    private static readonly LythonSourceSpan[] ClosureCheckpoints =
    [
        CallSpan, CallSpan, new(33,25,3,3), new(39,19,3,9), new(39,5,3,9),
        new(39,19,3,9), new(39,19,3,9), new(45,7,3,15), new(45,7,3,15),
        new(53,4,3,23), new(54,2,3,24), new(53,4,3,23), new(84,113,5,3),
        new(62,19,4,4), new(62,8,4,4), new(62,19,4,4), new(62,19,4,4),
        new(71,9,4,13), new(71,9,4,13), new(101,22,5,20), new(101,5,5,20),
        new(101,22,5,20), new(101,22,5,20), new(107,9,5,26), new(107,9,5,26),
        new(117,5,5,36), new(207,8,8,10), new(207,6,8,10), new(207,8,8,10),
        new(207,8,8,10), new(207,8,8,10), new(207,8,8,10), new(129,39,6,4),
        new(179,18,7,11), new(179,5,7,11), new(179,18,7,11), new(179,18,7,11),
        new(185,6,7,17), new(185,6,7,17), new(192,4,7,24)
    ];
    private static readonly LythonSourceSpan[] GenericCheckpoints =
    [
        CallSpan, CallSpan, new(33,13,3,3), new(42,4,3,12), new(43,2,3,13),
        new(42,4,3,12), new(49,254,4,3), new(111,192,7,4), new(69,15,5,5),
        new(69,8,5,5), new(69,15,5,5), new(69,15,5,5), new(78,5,5,14),
        new(78,5,5,14), new(89,18,6,5), new(89,8,6,5), new(89,18,6,5),
        new(89,18,6,5), new(98,8,6,14), new(98,8,6,14), new(153,25,7,46),
        new(153,5,7,46), new(153,25,7,46), new(153,25,7,46), new(159,9,7,52),
        new(159,9,7,52), new(169,8,7,62), new(133,19,7,26), new(133,5,7,26),
        new(133,19,7,26), new(133,19,7,26), new(139,10,7,32), new(139,10,7,32),
        new(150,1,7,43), new(133,19,7,26), new(133,19,7,26), new(133,19,7,26),
        new(181,17,7,74), new(181,5,7,74), new(181,17,7,74), new(181,17,7,74),
        new(187,8,7,80), new(187,8,7,80), new(196,1,7,89), new(181,17,7,74),
        new(181,17,7,74), new(181,17,7,74), new(313,5,11,10)
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedMethodClosuresKeepFundingAndEveryFuelBoundary(bool asynchronous)
    {
        var fixture = await Define(ClosureSource, asynchronous);
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        fixture.Context.Limits.ExecutionStepCount = 0;
        var leaf = Assert.IsAssignableFrom<PyFunctionBase>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var steps = fixture.Context.Limits.ExecutionStepCount;
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.Equal(40, steps);
        Assert.Equal(2410, funded);
        Assert.Equal(new[] { "saved", "factory", "default", "factory-apply", "leaf" }, fixture.Tape.Events);
        AssertUnwound(fixture.Context);
        var retained = Assert.IsType<PyTuple>(await Invoke(leaf, fixture.Context, asynchronous));
        Assert.Same(fixture.Tape.Saved, retained[0]);
        Assert.Same(retained[0], retained[1]);
        Assert.Same(fixture.Type, retained[2]);
        Assert.Equal(new BigInteger(17), Assert.Single(Assert.IsType<PyList>(retained[0])));
        Assert.Same(fixture.Context.MemoryGovernor, fixture.Tape.Saved!.OwnerMemoryGovernor);
        var spans = new List<LythonSourceSpan?>();
        var effects = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            fixture = await Define(ClosureSource, asynchronous);
            fixture.Context.Limits.ExecutionStepCount = MaximumSteps - remaining;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Invoke(fixture.Method, fixture.Context, asynchronous));
            Assert.Equal($"maximum execution step count exceeded ({MaximumSteps})", error.Message);
            Assert.Equal(MaximumSteps + 1, fixture.Context.Limits.ExecutionStepCount);
            Assert.Equal(remaining < 2 ? null : "/method-body.py", error.SourcePath);
            spans.Add(error.Span);
            effects.Add(string.Join(',', fixture.Tape.Events));
            AssertUnwound(fixture.Context);
        }
        output.WriteLine($"async={asynchronous}, steps={steps}, funded={funded}");
        output.WriteLine("spans=" + string.Join(';', spans.Select(s => s is null ? "null" : $"{s.Start},{s.Length},{s.Line},{s.Column}")));
        output.WriteLine("effects=" + string.Join(';', effects));
        Assert.Equal(ClosureCheckpoints, spans);
        Assert.Equal(Enumerable.Repeat("", 12).Concat(Enumerable.Repeat("saved", 7))
            .Concat(Enumerable.Repeat("saved,factory", 7))
            .Concat(Enumerable.Repeat("saved,factory,default,factory-apply", 14)), effects);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericMethodDefinitionsRetainDefaultsTypeScopesAndRealClassCells(bool asynchronous)
    {
        var fixture = await Define(GenericSource, asynchronous);
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        fixture.Context.Limits.ExecutionStepCount = 0;
        var inner = Assert.IsType<PyType>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var steps = fixture.Context.Limits.ExecutionStepCount;
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.Equal(48, steps);
        Assert.Equal(10864, funded);
        Assert.Equal(new[] { "top", "bottom", "default", "argument", "result", "bottom-apply", "top-apply" }, fixture.Tape.Events);
        Assert.Equal(new[]
        {
            "top:14,6810,96,3,1", "bottom:20,6810,96,3,1", "default:27,6810,96,3,1",
            "argument:34,10946,96,3,1", "result:44,11335,96,3,1",
            "bottom-apply:47,11661,96,2,1", "top-apply:47,11661,96,2,1"
        }, fixture.Tape.Snapshots);
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
        output.WriteLine($"generic async={asynchronous}, steps={steps}, funded={funded}, phases={string.Join(';', fixture.Tape.Snapshots)}");
        var spans = new List<LythonSourceSpan?>();
        var effects = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            fixture = await Define(GenericSource, asynchronous);
            fixture.Context.Limits.ExecutionStepCount = MaximumSteps - remaining;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Invoke(fixture.Method, fixture.Context, asynchronous));
            Assert.Equal($"maximum execution step count exceeded ({MaximumSteps})", error.Message);
            Assert.Equal(MaximumSteps + 1, fixture.Context.Limits.ExecutionStepCount);
            Assert.Equal(remaining < 2 ? null : "/method-body.py", error.SourcePath);
            spans.Add(error.Span);
            effects.Add(string.Join(',', fixture.Tape.Events));
            AssertUnwound(fixture.Context);
        }
        output.WriteLine("generic spans=" + string.Join(';', spans.Select(s => s is null ? "null" : $"{s.Start},{s.Length},{s.Line},{s.Column}")));
        output.WriteLine("generic effects=" + string.Join(';', effects));
        Assert.Equal(GenericCheckpoints, spans);
        Assert.Equal(Enumerable.Repeat("", 14).Concat(Enumerable.Repeat("top", 6))
            .Concat(Enumerable.Repeat("top,bottom", 7))
            .Concat(Enumerable.Repeat("top,bottom,default", 7))
            .Concat(Enumerable.Repeat("top,bottom,default,argument", 10))
            .Concat(Enumerable.Repeat("top,bottom,default,argument,result", 3))
            .Concat(["top,bottom,default,argument,result,bottom-apply,top-apply"]), effects);
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
        fixture.Context.Limits.ExecutionStepCount = 0;
        var before = fixture.Context.MemoryGovernor.CurrentAccountedBytes;
        var generator = Assert.IsType<LythonRuntime.PyGenerator>(await Invoke(fixture.Method, fixture.Context, asynchronous));
        var steps = fixture.Context.Limits.ExecutionStepCount;
        var funded = fixture.Context.MemoryGovernor.CurrentAccountedBytes - before;
        Assert.Equal(26, steps);
        Assert.Equal(2950, funded);
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
        output.WriteLine($"generator async={asynchronous}, steps={steps}, funded={funded}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitionBudgetDenialsReleaseReservationsAndKeepCallerBindings(bool asynchronous)
    {
        foreach (var allowance in new long[] { 0, 128, 512, 1024, 4096, 8192, 12288 })
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
                    Assert.Equal("execution memory budget exceeded (1048576)", error.Message);
                    Assert.Equal(allowance <= 128 ? new LythonSourceSpan(42, 4, 3, 12)
                        : allowance <= 4096 ? new LythonSourceSpan(49, 254, 4, 3) : null, error.Span);
                    Assert.Equal(allowance == 8192 ? ["top", "bottom", "default"] : Array.Empty<string>(), fixture.Tape.Events);
                }
                AssertUnwound(fixture.Context);
                Assert.Same(fixture.Receiver, fixture.Context.Variables["runner"]);
                Assert.Same(fixture.Active, fixture.Context.Services.CurrentException);
                output.WriteLine($"budget async={asynchronous}, allowance={allowance}, error={error?.Message ?? "success"}, span={error?.Span}, phases={string.Join(',', fixture.Tape.Events)}");
            }
            finally { governor.Release(padding); }
            Assert.Same(fixture.Receiver, await Invoke(Bind(fixture.Receiver, "identity"), fixture.Context, asynchronous));
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
        fixture.Context.Limits.ExecutionStepCount = 0;
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
            Assert.Equal(3, fixture.Context.Limits.ExecutionStepCount);
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
            Snapshots.Add($"{phase}:{frame.Limits.ExecutionStepCount},{frame.MemoryGovernor.CurrentAccountedBytes},{frame.MemoryGovernor.CurrentReservedBytes},{frame.Limits.CurrentInterpreterDepth},{frame.Limits.CurrentRecursionDepth}");
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
            MaxExecutionSteps = MaximumSteps, MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/method-body.py", CancellationToken = cancellation
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var tape = new Tape(context, active, cancellation);
        context.Variables["stamp"] = new Tape.Callback(tape, false);
        context.Variables["decorate"] = new Tape.Callback(tape, true);
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, context);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, context, default, [], null);
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
