using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class GeneratorPreparedSliceOwnershipTests
{
    private static readonly LythonSourceSpan CallSpan = new(1000, 4, 90, 1);
    private const string Source = "class Target:\n def __getitem__(self,key):return gettap(key)\n def __setitem__(self,key,value):storetap(key,value)\ntarget=Target()\ndef generate():\n target[::1]+=yield 2\n yield 3\ng=generate()\n";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluatedKeyIsOwnedBeforeGetAndRetainedUntilStore(bool asynchronous)
    {
        var fixture = await Define(asynchronous);
        var first = await Next(fixture.Generator, asynchronous);
        Assert.True(first.HasValue);
        Assert.Equal(new BigInteger(2), first.Value);
        var key = Assert.Single(fixture.Tape.Keys);
        Assert.True(fixture.Context.Services.State.CallTemporaries.IsTracked(key));
        Assert.Same(PyNone.Instance, key.Start);
        Assert.Same(PyNone.Instance, key.Stop);
        Assert.Equal(new BigInteger(1), key.Step);
        AssertUnwound(fixture.Context);
        var second = await Resume(fixture, asynchronous);
        Assert.True(second.HasValue);
        Assert.Equal(new BigInteger(3), second.Value);
        Assert.Equal(2, fixture.Tape.Keys.Count);
        Assert.Same(key, fixture.Tape.Keys[1]);
        Assert.All(fixture.Tape.Tracked, Assert.True);
        Assert.Equal(new BigInteger(4), fixture.Tape.Stored);
        AssertUnwound(fixture.Context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedPlainSliceStoreOwnsItsEvaluatedKey(bool asynchronous)
    {
        var fixture = await Define(asynchronous, source: Source.Replace(
            "target[::1]+=yield 2", "target[(yield 2)::1]=4", StringComparison.Ordinal));
        Assert.True((await Next(fixture.Generator, asynchronous)).HasValue);
        Assert.Empty(fixture.Tape.Keys);
        AssertUnwound(fixture.Context);
        var second = await Resume(fixture, asynchronous);
        Assert.True(second.HasValue);
        Assert.Equal(new BigInteger(3), second.Value);
        var key = Assert.Single(fixture.Tape.Keys);
        Assert.True(fixture.Context.Services.State.CallTemporaries.IsTracked(key));
        Assert.Equal(new BigInteger(3), key.Start);
        Assert.Same(PyNone.Instance, key.Stop);
        Assert.Equal(new BigInteger(1), key.Step);
        Assert.Equal(new BigInteger(4), fixture.Tape.Stored);
        AssertUnwound(fixture.Context);
    }

    [Theory]
    [InlineData("get", false)]
    [InlineData("get", true)]
    [InlineData("store", false)]
    [InlineData("store", true)]
    public async Task RealHostSuspensionAndCancellationKeepOwnedSliceState(string phase, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = await Define(true, cancellation.Token);
        fixture.Tape.Pause = phase;
        fixture.Tape.RequireAsync = true;
        if (phase == "store") Assert.True((await Next(fixture.Generator, true)).HasValue);
        var pending = phase == "get" ? Next(fixture.Generator, true).AsTask()
            : Resume(fixture, true).AsTask();
        var callbackFrame = await fixture.Tape.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Same(fixture.Context.Services, callbackFrame.Services);
            Assert.Same(fixture.Context.MemoryGovernor, callbackFrame.MemoryGovernor);
            Assert.Same(fixture.Active, callbackFrame.Services.CurrentException);
            Assert.All(fixture.Tape.Keys, key => Assert.True(fixture.Context.Services.State.CallTemporaries.IsTracked(key)));
            if (phase == "store") Assert.Same(fixture.Tape.Keys[0], fixture.Tape.Keys[1]);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                fixture.Tape.Release.SetResult();
                Assert.True((await pending).HasValue);
            }
        }
        finally
        {
            fixture.Tape.Release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        AssertUnwound(fixture.Context);
        Assert.Same(fixture.Active, fixture.Context.Services.CurrentException);
    }

    private static async ValueTask<PyIterationResult> Resume(Fixture fixture, bool asynchronous)
    {
        Assert.True(fixture.Generator.TryGetMember("send", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(new BigInteger(3))];
        var value = asynchronous ? await callable.InvokeAsync(arguments, CallSpan, fixture.Context)
            : callable.Invoke(arguments, CallSpan, fixture.Context);
        return PyIterationResult.Yield(value);
    }

    private static ValueTask<PyIterationResult> Next(LythonRuntime.PyGenerator generator, bool asynchronous)
        => asynchronous ? generator.TryMoveNextAsync() : new(generator.TryMoveNext(out var value)
            ? PyIterationResult.Yield(value) : PyIterationResult.End);

    private static async Task<Fixture> Define(bool asynchronous, CancellationToken cancellation = default, string source = Source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 1024 * 1024, MaxExecutionSteps = 10000,
            SourcePath = "/generator-slice.py", CancellationToken = cancellation
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Services.SetCurrentException(active);
        var tape = new Tape(root, cancellation);
        root.Variables["gettap"] = new Callback(tape, "get");
        root.Variables["storetap"] = new Callback(tape, "store");
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, root);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, root, default, [], null);
        AssertUnwound(root);
        return new(root, Assert.IsType<LythonRuntime.PyGenerator>(root.Variables["g"]), tape, active);
    }

    private static void AssertUnwound(Context root)
    {
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(root.CurrentExecutableFrame);
    }

    private sealed record Fixture(Context Context, LythonRuntime.PyGenerator Generator, Tape Tape, PyException Active);

    private sealed class Tape(Context root, CancellationToken cancellation)
    {
        public List<PySlice> Keys { get; } = [];
        public List<bool> Tracked { get; } = [];
        public object? Stored { get; private set; }
        public string? Pause { get; set; }
        public bool RequireAsync { get; set; }
        public TaskCompletionSource<Context> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Record(string phase, CallArgumentValue[] arguments, Context frame)
        {
            Assert.Same(root.Services, frame.Services);
            Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
            var key = Assert.IsType<PySlice>(arguments[0].Value);
            Keys.Add(key);
            Tracked.Add(root.Services.State.CallTemporaries.IsTracked(key));
            if (phase == "store") Stored = arguments[1].Value;
            return phase == "get" ? new BigInteger(1) : PyNone.Instance;
        }
        public async ValueTask<object> RecordAsync(string phase, CallArgumentValue[] arguments, Context frame)
        {
            var result = Record(phase, arguments, frame);
            if (phase == Pause)
            {
                Started.SetResult(frame);
                await Release.Task.WaitAsync(cancellation);
            }
            else await Task.Yield();
            return result;
        }
    }

    private sealed class Callback(Tape tape, string phase) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.False(tape.RequireAsync);
            return tape.Record(phase, arguments, context);
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => tape.RecordAsync(phase, arguments, context);
    }
}
