using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class UserSliceMutationOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
    private static readonly string[] Forms = ["delete", "loop", "unpack-loop", "starred-loop", "unpack", "store"];

    public static IEnumerable<object[]> Routes()
    {
        foreach (var form in Forms)
        foreach (var asynchronous in new[] { false, true })
            yield return [form, asynchronous];
    }

    public static IEnumerable<object[]> Pauses()
    {
        foreach (var form in Forms)
        foreach (var cancel in new[] { false, true })
            yield return [form, cancel];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task KeyIsOwnedBeforeUserMutationAndEscapesWithRawBounds(string form, bool asynchronous)
    {
        var fixture = await Define(form);
        await Invoke(fixture, asynchronous);
        var key = Assert.Single(fixture.Tape.Keys);

        Assert.Same(fixture.Root.Variables["start_value"], key.Start);
        Assert.Same(fixture.Root.Variables["stop_value"], key.Stop);
        Assert.Same(fixture.Root.Variables["step_value"], key.Step);
        Assert.True(fixture.Target.TryGetAttribute("saved", fixture.Root, Span, out var saved));
        Assert.Same(key, saved);
        Assert.Equal(new[] { "receiver", "start", "stop", "step", "mutation" }, fixture.Tape.Events);
        AssertUnwound(fixture.Root);
    }

    [Theory]
    [MemberData(nameof(Pauses))]
    public async Task HostSuspensionKeepsActualKeyArgumentsAndSupportsReentry(string form, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = await Define(form, cancellation.Token);
        fixture.Tape.Pause = true;
        var pending = Invoke(fixture, true);
        try
        {
            var frame = await fixture.Tape.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.NotSame(fixture.Root, frame);
            Assert.Same(fixture.Root.Services, frame.Services);
            Assert.Same(fixture.Root.MemoryGovernor, frame.MemoryGovernor);
            Assert.Same(fixture.Target, LythonRuntime.ResolveName("self", Span, frame));
            Assert.Same(fixture.Active, frame.Services.CurrentException);
            var key = Assert.Single(fixture.Tape.Keys);
            var alias = Assert.IsAssignableFrom<PyFunctionBase>(fixture.Root.Variables["alias"]);
            Assert.Same(fixture.Target, await alias.InvokeAsync([], Span, fixture.Root));
            if (cancel) cancellation.Cancel();
            else fixture.Tape.Release.TrySetResult();
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            else await pending;
        }
        finally
        {
            fixture.Tape.Release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        Assert.Equal(new[] { "receiver", "start", "stop", "step", "mutation" }, fixture.Tape.Events);
        Assert.Same(fixture.Active, fixture.Root.Services.CurrentException);
        AssertUnwound(fixture.Root);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ExhaustedBudgetRejectsFreshKeyBeforeHostMutation(string form, bool asynchronous)
    {
        var fixture = await Define(form, direct: true);
        fixture.Tape.PinAtLastBound = true;
        try
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => Invoke(fixture, asynchronous));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Empty(fixture.Tape.Keys);
            Assert.Equal(new[] { "receiver", "start", "stop", "step" }, fixture.Tape.Events);
        }
        finally { fixture.Tape.Pin?.Dispose(); }
        AssertUnwound(fixture.Root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExhaustedBudgetStillAllowsAnExistingIndexKey(bool asynchronous)
    {
        var fixture = await Define("index", direct: true);
        fixture.Tape.PinAtLastBound = true;
        try
        {
            await Invoke(fixture, asynchronous);
            Assert.True(fixture.Tape.IndexMutated);
            Assert.Equal(new[] { "receiver", "step", "mutation" }, fixture.Tape.Events);
        }
        finally { fixture.Tape.Pin?.Dispose(); }
        AssertUnwound(fixture.Root);
    }

    [Theory]
    [InlineData("delete", false)]
    [InlineData("delete", true)]
    [InlineData("loop", true)]
    [InlineData("unpack-loop", true)]
    [InlineData("starred-loop", true)]
    [InlineData("unpack", true)]
    [InlineData("store", true)]
    public async Task EscapedKeyLifetimeIsIndependentOfRetainedAttributeCapacity(string form, bool asynchronous)
    {
        var fixture = await Define(form);
        var weak = InvokeAndDropHostReferences(fixture, asynchronous);
        Collect();
        Assert.True(IsAlive(weak));
        var before = fixture.Root.MemoryGovernor.CurrentCommittedBytes;
        var deletion = LythonFrontend.Compile("del target.saved\n",regexProvider:Utf8RegexProvider.Instance);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(deletion.Script.RequireNotNull()).Statements, fixture.Root);
        Collect();
        Assert.False(IsAlive(weak));
        // Actual CLR reachability ends; reusable attribute capacity stays funded
        // until its existing execution owner ends.
        Assert.Equal(before, fixture.Root.MemoryGovernor.CurrentCommittedBytes);
        AssertUnwound(fixture.Root);
        GC.KeepAlive(fixture);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> InvokeAndDropHostReferences(Fixture fixture, bool asynchronous)
    {
        Invoke(fixture, asynchronous).GetAwaiter().GetResult();
        var weak = new WeakReference<object>(Assert.Single(fixture.Tape.Keys));
        fixture.Tape.Keys.Clear();
        return weak;
    }

    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<object> weak) => weak.TryGetTarget(out _);

    private static async Task Invoke(Fixture fixture, bool asynchronous)
    {
        if (asynchronous) await fixture.Run.InvokeAsync([], Span, fixture.Root);
        else fixture.Run.Invoke([], Span, fixture.Root);
    }

    private static async Task<Fixture> Define(string form, CancellationToken cancellation = default, bool direct = false)
    {
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            SourcePath = "/user-slice-ownership.py", MemoryBudget = new LythonMemoryBudget(1024 * 1024),
            CancellationToken = cancellation
        },regexProvider:Utf8RegexProvider.Instance);
        var tape = new Tape(root, cancellation, direct, form == "delete", form == "index");
        root.Variables["mutate"] = new Callback(tape, "mutation");
        foreach (var stage in new[] { "receiver", "start", "stop", "step" })
            root.Variables[stage] = new Callback(tape, stage);
        var target = "receiver()[start():stop():step()]";
        var operation = form switch
        {
            "delete" => "del " + target,
            "loop" => "for " + target + " in values:\n  pass",
            "unpack-loop" => "for " + target + ",other in pairs:\n  pass",
            "starred-loop" => "for " + target + ",*other in pairs:\n  pass",
            "unpack" => target + ",other=pair",
            "index" => "receiver()[step()]=payload",
            _ => target + "=payload"
        };
        var methods = direct ? " __delitem__=mutate\n __setitem__=mutate\n" : """
             def __delitem__(self,key):
              mutate(self,key)
              self.saved=key
             def __setitem__(self,key,value):
              mutate(self,key,value)
              self.saved=key
            """ + "\n";
        var source = "class Target:\n" + methods + """
            target=Target()
            start_value=[]
            stop_value=[]
            step_value=[]
            payload=[]
            values=[payload]
            pair=(payload,None)
            pairs=[pair]
            def alias():return target
            """ + "\ndef run():\n " + operation + "\n";
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Services.SetCurrentException(active);
        return new(root, Assert.IsType<PyInstance>(root.Variables["target"]),
            Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["run"]), tape, active);
    }

    private static void AssertUnwound(Context root)
    {
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(root.CurrentExecutableFrame);
    }

    private sealed record Fixture(Context Root, PyInstance Target, PyFunctionBase Run, Tape Tape, PyException Active);

    private sealed class Tape(Context root, CancellationToken cancellation, bool direct, bool deletion, bool index)
    {
        public List<string> Events { get; } = [];
        public List<PySlice> Keys { get; } = [];

        public bool IndexMutated { get; private set; }
        public bool PinAtLastBound { get; set; }
        public IDisposable? Pin { get; private set; }
        public bool Pause { get; set; }
        public TaskCompletionSource<Context> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object Record(string stage, CallArgumentValue[] args, Context frame)
        {
            Assert.Same(root.Services, frame.Services);
            Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
            Events.Add(stage);
            if (stage == "mutation")
            {
                var offset = direct ? 0 : 1;
                Assert.Equal((deletion ? 1 : 2) + offset, args.Length);
                if (!direct) Assert.Same(root.Variables["target"], args[0].Value);
                if (!deletion) Assert.Same(root.Variables["payload"], args[offset + 1].Value);
                if (index)
                {
                    Assert.Same(root.Variables["step_value"], args[offset].Value);
                    IndexMutated = true;
                }
                else
                {
                    var key = Assert.IsType<PySlice>(args[offset].Value);
                    Keys.Add(key);

                    Assert.Same(root.Variables["start_value"], key.Start);
                    Assert.Same(root.Variables["stop_value"], key.Stop);
                    Assert.Same(root.Variables["step_value"], key.Step);
                }
                return PyNone.Instance;
            }
            Assert.Empty(args);
            if (stage == "step" && PinAtLastBound)
            {
                Pin = root.MemoryGovernor.ReserveTemporary(((LythonMemoryBudget)root.MemoryGovernor.Budget!).LimitBytes!.Value - root.MemoryGovernor.CurrentAccountedBytes, Span);
            }
            return root.Variables[stage == "receiver" ? "target" : stage + "_value"];
        }

        public async ValueTask<object> RecordAsync(string stage, CallArgumentValue[] args, Context frame)
        {
            var result = Record(stage, args, frame);
            if (stage == "mutation" && Pause)
            {
                var original = args.Select(a => a.Value).ToArray();
                Started.TrySetResult(frame);
                await Release.Task.WaitAsync(cancellation);
                Assert.Equal(original.Length, args.Length);
                for (var i = 0; i < args.Length; i++) Assert.Same(original[i], args[i].Value);
            }
            return result;
        }
    }

    private sealed class Callback(Tape tape, string stage) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context)
        {
            Assert.False(tape.Pause);
            return tape.Record(stage, args, context);
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context)
            => tape.RecordAsync(stage, args, context);
    }
}
