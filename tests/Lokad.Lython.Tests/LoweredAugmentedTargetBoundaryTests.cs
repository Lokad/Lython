using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LoweredAugmentedTargetBoundaryTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan CallSpan = new(1000, 3, 100, 1);

    [Theory]
    [InlineData("member", "get", false)]
    [InlineData("member", "rhs", false)]
    [InlineData("member", "store", false)]
    [InlineData("index", "get", false)]
    [InlineData("index", "rhs", false)]
    [InlineData("index", "store", false)]
    [InlineData("slice", "get", false)]
    [InlineData("slice", "rhs", false)]
    [InlineData("slice", "store", false)]
    [InlineData("member", "get", true)]
    [InlineData("member", "rhs", true)]
    [InlineData("member", "store", true)]
    [InlineData("index", "get", true)]
    [InlineData("index", "rhs", true)]
    [InlineData("index", "store", true)]
    [InlineData("slice", "get", true)]
    [InlineData("slice", "rhs", true)]
    [InlineData("slice", "store", true)]
    public async Task GetRhsAndStoreActuallySuspendAndKeepOwnedValues(string form, string pausedStage, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var source = form == "member" ? """
            class Descriptor:
             def __get__(self,obj,owner):return gettap(obj)
             def __set__(self,obj,value):storetap(obj,value)
            class Target:
             value=Descriptor()
            """ : """
            class Target:
             def __getitem__(self,key):return gettap(self,key)
             def __setitem__(self,key,value):storetap(self,key,value)
            """;
        var target = form == "member" ? "target.value" : form == "index" ? "target[key]" : "target[key:key:key]";
        source += $"\ntarget=Target()\nclass Runner:\n def run(self):{target} += rhs()\n def identity(self):return self\nrunner=Runner()\n";
        var context = await Define(source, true, cancellation.Token);
        var key = new object();
        context.Variables["key"] = key;
        var actualTarget = Assert.IsType<PyInstance>(context.Variables["target"]);
        var actualRunner = Assert.IsType<PyInstance>(context.Variables["runner"]);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var events = new List<string>();
        CallArgumentValue[]? getArguments = null;
        CallArgumentValue[]? storeArguments = null;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var stage in new[] { "get", "rhs", "store" })
        {
            context.Variables[stage == "rhs" ? "rhs" : stage + "tap"] = new AsyncTap(async (arguments, frame) =>
            {
                Assert.Same(context.Services, frame.Services);
                Assert.Same(active, frame.Services.CurrentException);
                events.Add(stage);
                if (stage == "get") getArguments = arguments.ToArray();
                if (stage == "store") storeArguments = arguments.ToArray();
                if (stage != "rhs") Assert.Same(actualTarget, arguments[0].Value);
                else { Assert.Equal("run", frame.FunctionName); Assert.Same(actualRunner, frame.Variables["self"]); }
                if (stage == pausedStage)
                {
                    started.SetResult(frame);
                    await gate.Task.WaitAsync(cancellation.Token);
                }
                return stage == "rhs" ? new BigInteger(2) : BigInteger.One;
            });
        }
        var method = Bind(actualRunner, "run");
        var pending = method.InvokeAsync([], CallSpan, context);
        var pausedFrame = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(pending.IsCompleted);
        var heldDepth = context.Limits.CurrentRecursionDepth;
        Assert.Same(actualRunner, await Bind(actualRunner, "identity").InvokeAsync([], CallSpan, context));
        Assert.Equal(heldDepth, context.Limits.CurrentRecursionDepth);
        Assert.Same(context.Services, pausedFrame.Services);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Equal(Array.IndexOf(new[] { "get", "rhs", "store" }, pausedStage) + 1, events.Count);
        }
        else
        {
            gate.SetResult();
            await pending;
            Assert.Equal(new[] { "get", "rhs", "store" }, events);
            Assert.Equal(new BigInteger(3), storeArguments![^1].Value);
            if (form == "index")
            {
                Assert.Same(key, getArguments![1].Value);
                Assert.Same(key, storeArguments[1].Value);
            }
            if (form == "slice")
            {
                var slice = Assert.IsType<PySlice>(getArguments![1].Value);
                Assert.Same(key, slice.Start);
                Assert.Same(key, slice.Stop);
                Assert.Same(key, slice.Step);
                Assert.Same(slice, storeArguments[1].Value);
            }
        }
        Assert.Same(active, context.Services.CurrentException);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("member")]
    [InlineData("index")]
    [InlineData("slice")]
    public async Task EveryFuelBoundaryPreservesSpansAndUnwindsInBothModes(string form)
    {
        var operation = form switch
        {
            "name" => "global value\n  value += 2",
            "member" => "target.value += 2",
            "index" => "items[index] += 2",
            _ => "items[:] += [2]"
        };
        var source = $"class Target:pass\ntarget=Target()\ntarget.value=1\nitems=[1]\nindex=0\nvalue=1\nclass Runner:\n def run(self):\n  {operation}\nrunner=Runner()\n";
        var snapshots = new List<(long Steps, long Fee)>();
        var allSpans = new List<LythonSourceSpan?[]>();
        LythonSourceSpan?[] expectedSpans = form switch
        {
            "name" => [CallSpan, CallSpan, new(122, 10, 10, 3), new(131, 1, 10, 12)],
            "member" => [CallSpan, CallSpan, new(107, 17, 9, 3), new(107, 6, 9, 3), new(123, 1, 9, 19)],
            "index" => [CallSpan, CallSpan, new(107, 17, 9, 3), new(107, 5, 9, 3), new(113, 5, 9, 9), new(123, 1, 9, 19)],
            _ => [CallSpan, CallSpan, new(107, 15, 9, 3), new(107, 5, 9, 3), new(119, 3, 9, 15),
                new(120, 1, 9, 16), new(119, 3, 9, 15), new(107, 15, 9, 3), new(107, 8, 9, 3), new(107, 8, 9, 3)]
        };
        foreach (var asynchronous in new[] { false, true })
        {
            var context = await Define(source, asynchronous);
            context.Limits.ExecutionStepCount = 0;
            var before = context.MemoryGovernor.CurrentAccountedBytes;
            await Invoke(Bind(Assert.IsType<PyInstance>(context.Variables["runner"]), "run"), context, asynchronous);
            var steps = context.Limits.ExecutionStepCount;
            snapshots.Add((steps, context.MemoryGovernor.CurrentAccountedBytes - before));
            AssertUnwound(context);
            var spans = new List<LythonSourceSpan?>();
            for (var remaining = 0; remaining < steps; remaining++)
            {
                context = await Define(source, asynchronous);
                context.Limits.ExecutionStepCount = 1000 - remaining;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await Invoke(Bind(Assert.IsType<PyInstance>(context.Variables["runner"]), "run"), context, asynchronous));
                Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
                Assert.Equal(1001, context.Limits.ExecutionStepCount);
                Assert.Equal(remaining < 2 ? null : "/augmented-target.py", error.SourcePath);
                spans.Add(error.Span);
                AssertUnwound(context);
            }
            allSpans.Add(spans.ToArray());
            output.WriteLine($"form={form}, async={asynchronous}, snapshot={snapshots[^1]}, spans={string.Join(';', spans)}");
        }
        Assert.Equal(snapshots[0], snapshots[1]);
        Assert.Equal(allSpans[0], allSpans[1]);
        // Pinned from the pre-prototype runtime, independently of its target
        // representation: exact checks/spans and retained slice funding.
        Assert.Equal(((long)expectedSpans.Length, form == "slice" ? 896L : 0L), snapshots[0]);
        Assert.Equal(expectedSpans, allSpans[0]);
    }

    private sealed class AsyncTap(Func<CallArgumentValue[], Context, Task<object>> callback) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The awaited augmented target used a synchronous callable entry.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => await callback(arguments, context);
    }

    private static async Task<Context> Define(string source, bool asynchronous, CancellationToken cancellation = default)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 1000, MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/augmented-target.py", CancellationToken = cancellation
        });
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, context);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, context, default, [], null);
        AssertUnwound(context);
        return context;
    }
    private static PyBoundMethod Bind(PyInstance instance, string name)
    {
        Assert.True(instance.Type.TryGetMember(name, out var member));
        return Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(member).Bind(instance));
    }
    private static ValueTask<object> Invoke(PyBoundMethod method, Context context, bool asynchronous)
        => asynchronous ? method.InvokeAsync([], CallSpan, context) : ValueTask.FromResult(method.Invoke([], CallSpan, context));
    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
    }
}
