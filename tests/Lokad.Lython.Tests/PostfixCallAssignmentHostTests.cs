using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class PostfixCallAssignmentHostTests
{
    private static readonly LythonSourceSpan CallSpan = new(1000, 3, 100, 1);

    private static string[] Stages(string form) => form switch
    {
        "member" => ["rhs", "receiver", "store"],
        "index" => ["rhs", "receiver", "key", "store"],
        _ => ["rhs", "receiver", "start", "stop", "step", "store"]
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var form in new[] { "member", "index", "slice" })
        foreach (var stage in Stages(form))
        foreach (var cancel in new[] { false, true })
        foreach (var parenthesized in new[] { false, true })
            yield return [form, stage, cancel, parenthesized];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RealHostSuspensionPreservesOrderingIdentityReentryAndCleanup(
        string form, string pausedStage, bool cancel, bool parenthesized)
    {
        using var cancellation = new CancellationTokenSource();
        var receiver = parenthesized ? "(receiver())" : "receiver()";
        var target = form switch
        {
            "member" => receiver + ".value",
            "index" => receiver + "[key()]",
            _ => receiver + "[start():stop():step()]"
        };
        var source = """
            saved=[]
            class Descriptor:
                def __set__(self,obj,value):
                    storetap(obj,None,value)
                    saved.append((None,value))
            class Target:
                value=Descriptor()
                def __setitem__(self,key,value):
                    storetap(self,key,value)
                    saved.append((key,value))
            target=Target()
            class Runner:
            """ + $"\n def run(self):\n  {target}=rhs()\n  return 7\n def identity(self):return self\nrunner=Runner()\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            SourcePath = "/postfix-store.py", MaxExecutionMemoryBytes = 1024 * 1024,
            CancellationToken = cancellation.Token
        });
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        var runner = Assert.IsType<PyInstance>(root.Variables["runner"]);
        var actualTarget = Assert.IsType<PyInstance>(root.Variables["target"]);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Services.SetCurrentException(active);
        var events = new List<string>();
        var markers = new Dictionary<string, object> { ["key"] = new(), ["start"] = new(), ["stop"] = new(), ["step"] = new() };
        var started = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PyList? produced = null;
        object? storedKey = null;

        foreach (var stage in Stages(form))
        {
            root.Variables[stage == "store" ? "storetap" : stage] = new AsyncTap(async (arguments, span, frame) =>
            {
                Assert.Same(root.Services, frame.Services);
                Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
                Assert.Same(active, frame.Services.CurrentException);
                events.Add(stage);
                object value;
                if (stage == "rhs")
                {
                    Assert.True(frame.TryResolveOwnLocal("self", out var receiver));
                    Assert.Same(runner, receiver);
                    produced = new PyList([], frame.MemoryGovernor, span);
                    value = produced;
                }
                else if (stage == "receiver") value = actualTarget;
                else if (stage == "store")
                {
                    Assert.Equal(3, arguments.Length);
                    Assert.Same(actualTarget, arguments[0].Value);
                    Assert.Same(produced, arguments[2].Value);
                    storedKey = arguments[1].Value;
                    if (form == "member") Assert.Same(PyNone.Instance, storedKey);
                    else if (form == "index") Assert.Same(markers["key"], storedKey);
                    else
                    {
                        var slice = Assert.IsType<PySlice>(storedKey);
                        Assert.Same(markers["start"], slice.Start);
                        Assert.Same(markers["stop"], slice.Stop);
                        Assert.Same(markers["step"], slice.Step);
                    }
                    value = PyNone.Instance;
                }
                else value = markers[stage];
                if (stage == pausedStage)
                {
                    started.SetResult(frame);
                    await release.Task.WaitAsync(cancellation.Token);
                }
                return value;
            });
        }

        var pending = Bind(runner, "run").InvokeAsync([], CallSpan, root).AsTask();
        try
        {
            var paused = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.NotNull(paused.CurrentExecutableFrame);
            var depth = root.Limits.CurrentInterpreterDepth;
            var recursion = root.Limits.CurrentRecursionDepth;
            Assert.True(depth > 0);
            Assert.True(recursion > 0);
            Assert.Same(runner, await Bind(runner, "identity").InvokeAsync([], CallSpan, root));
            Assert.Equal(depth, root.Limits.CurrentInterpreterDepth);
            Assert.Equal(recursion, root.Limits.CurrentRecursionDepth);
            Assert.Equal(Stages(form).Take(Array.IndexOf(Stages(form), pausedStage) + 1), events);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
                Assert.Equal(Stages(form).Take(Array.IndexOf(Stages(form), pausedStage) + 1), events);
                Assert.Empty(Assert.IsType<PyList>(root.Variables["saved"]));
            }
            else
            {
                release.SetResult();
                Assert.Equal(new BigInteger(7), await pending);
                Assert.Equal(Stages(form), events);
                var saved = Assert.IsType<PyTuple>(Assert.Single(Assert.IsType<PyList>(root.Variables["saved"])));
                Assert.Same(storedKey, saved[0]);
                Assert.Same(produced, saved[1]);
            }
        }
        finally
        {
            release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(root.CurrentExecutableFrame);
        Assert.Same(active, root.Services.CurrentException);
    }

    private static PyBoundMethod Bind(PyInstance instance, string name)
    {
        Assert.True(instance.Type.TryGetOwnMember(name, out var value));
        return Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(value).Bind(instance));
    }

    private sealed class AsyncTap(Func<CallArgumentValue[], LythonSourceSpan, Context, ValueTask<object>> callback) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("This host callback must genuinely await the asynchronous path.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => await callback(arguments, span, context);
    }
}
