using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeActivationRootTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CallbackAndSuspensionPublishArgumentsAndClearCompletedActivations(bool asynchronous, bool fail)
    {
        var (code, context) = Prepare("marker = 1\nreturn probe('x' * 16000)\n");
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
            else Assert.Equal(new BigInteger(16000), await pending);
        }
        else if (fail) Assert.Equal("ValueError", Assert.Throws<LythonRuntimeException>(() => Run(code, context)).ExceptionType);
        else Assert.Equal(new BigInteger(16000), Run(code, context));
        CheckEmpty(context);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvolvingNativeAccumulatorRemainsPublishedAcrossGeneratorCallbacks(bool asynchronous)
    {
        var (code, context) = Prepare("""
class Total:
    def __init__(self):
        self.payload = 'x' * 16000
    def __add__(self, ignored):
        return self
class First:
    def __radd__(self, ignored):
        return Total()
def values():
    yield First()
    probe()
    yield 0
return len(sum(values()).payload)
""");
        var probe = new Probe(false, accumulator: true);
        context.Variables["probe"] = probe;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            probe.Check(context);
            probe.Release.SetResult();
            Assert.Equal(new BigInteger(16000), await pending);
        }
        else Assert.Equal(new BigInteger(16000), Run(code, context));
        CheckEmpty(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultIsPublishedBeforeRegistrationDenialAndAllRootsUnwind(bool asynchronous)
    {
        var (code, context) = Prepare("marker = 1\nreturn getattr(source, 'payload')\n", 4096);
        var value = new Snapshot(context);
        context.MemoryGovernor.Reserve(64, null);
        context.MemoryGovernor.Commit(64);
        context.Variables["source"] = new Source(value, context);
        var error = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
            : Assert.Throws<LythonRuntimeException>(() => Run(code, context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.True(value.SawPendingResult);
        Assert.True(value.SawBoundArguments);
        CheckEmpty(context);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static (ExecutableScript Code, LythonRuntime.ExecutionContext Context) Prepare(string source, long cap = 2_000_000)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.DoesNotContain(parsed.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return (ExecutableScript.Compile(LoweredScript.Lower(parsed.Script.RequireNotNull())),
            new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap }));
    }

    private static object? Run(ExecutableScript code, LythonRuntime.ExecutionContext context)
        => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);

    private static object[] Activations(LythonRuntime.ExecutionContext context)
    {
        var type = context.State.GetType();
        var count = type.GetField("_nativeActivationCount", Fields)?.GetValue(context.State) as int?;
        Assert.NotNull(count);
        var frames = (Array)type.GetField("_nativeActivations", Fields)!.GetValue(context.State)!;
        return Enumerable.Range(0, count!.Value).Select(i => frames.GetValue(i)!).ToArray();
    }

    private static object? Field(object frame, string name) => frame.GetType().GetField(name, Fields)!.GetValue(frame);

    private static void CheckEmpty(LythonRuntime.ExecutionContext context)
    {
        Assert.Empty(Activations(context));
        var frames = (Array)context.State.GetType().GetField("_nativeActivations", Fields)!.GetValue(context.State)!;
        foreach (var frame in frames) foreach (var field in frame!.GetType().GetFields(Fields)) Assert.Null(field.GetValue(frame));
        Assert.Null(context.State.GetType().GetProperty("ActiveExecutableFrame", Fields)?.GetValue(context.State));
    }

    private sealed class Probe(bool fail, bool accumulator = false) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private object? _argument;
        private LythonRuntime.ExecutionContext? _caller;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _argument = arguments.FirstOrDefault().Value;
            _caller = context;
            Check(context);
            return Finish(span);
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _argument = arguments.FirstOrDefault().Value;
            _caller = context;
            try { Check(context); }
            finally { Started.TrySetResult(); }
            await Release.Task.ConfigureAwait(false);
            Check(context);
            return Finish(span);
        }
        internal void Check(LythonRuntime.ExecutionContext context)
        {
            var frames = Activations(context);
            var current = Assert.Single(frames, f => ReferenceEquals(Field(f, "Target"), this));
            Assert.Same(_caller, Field(current, "Context"));
            var args = (CallArgumentValue[])Field(current, "Arguments")!;
            if (!accumulator) Assert.Same(_argument, Assert.Single(args).Value);
            else
            {
                var sum = Assert.Single(frames, f => Field(f, "Target") is INamedRuntimeCallable { Name: "sum" });
                var total = Assert.IsType<PyInstance>(Field(sum, "WorkingStorage"));
                var attributes = (Dictionary<string, object>)typeof(PyInstance).GetField("_attributes", Fields)!.GetValue(total)!;
                Assert.Equal(16000, Assert.IsType<PyString>(attributes["payload"]).Length);
                Assert.NotNull(Field(sum, "BoundArguments"));
            }
        }
        private object Finish(LythonSourceSpan span) => fail ? throw new LythonRuntimeException("ValueError", "native callback failed", span) : new BigInteger(16000);
    }

    private sealed class Source(Snapshot value, LythonRuntime.ExecutionContext context) : IPyDynamicAttributes
    {
        public bool TryGetMember(string name, out object result)
        {
            result = value;
            var remaining = context.MemoryGovernor.MaxAccountedBytes!.Value - context.MemoryGovernor.CurrentAccountedBytes;
            context.MemoryGovernor.Reserve(remaining, null);
            context.MemoryGovernor.Commit(remaining);
            return name == "payload";
        }
    }
    private sealed class Snapshot(LythonRuntime.ExecutionContext context) : IPyOwnershipSnapshot
    {
        internal bool SawPendingResult;
        internal bool SawBoundArguments;
        public bool TrySnapshotOwnership(out long bytes)
        {
            var frame = Activations(context).Last();
            Assert.Same(this, Field(frame, "PendingResult"));
            SawPendingResult = true;
            Assert.NotNull(Field(frame, "BoundArguments"));
            SawBoundArguments = true;
            bytes = 64;
            return true;
        }
    }
}
