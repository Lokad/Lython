using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class PropertyConstructorHostTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void AliasedNumericFieldsCarryAllowancesAndRejectedUpdateKeepsValue()
    {
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        object scalar = new BigInteger(7);
        var property = PyProperty.Create(scalar, scalar, scalar, scalar, root, Span);
        Assert.Equal(PyProperty.ShellBytes + 4 * RuntimeMemoryEstimates.ScalarStorageBytes, property.CommittedBytes);
        Assert.True(property.TryGetMember("fget", out var getter));
        Assert.Same(scalar, getter);
        Assert.True(property.TrySetMember("__doc__", PyNone.Instance));
        Assert.Equal(PyProperty.ShellBytes + 3 * RuntimeMemoryEstimates.ScalarStorageBytes, property.CommittedBytes);
        using (root.MemoryGovernor.ReserveTemporary(((LythonMemoryBudget)root.MemoryGovernor.Budget!).LimitBytes!.Value - root.MemoryGovernor.CurrentAccountedBytes, Span))
        {
            var error = Assert.Throws<LythonRuntimeException>(() => property.TrySetMember("__doc__", new BigInteger(9)));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.True(property.TryGetMember("__doc__", out var doc));
            Assert.Same(PyNone.Instance, doc);
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(property);
    }

    [Fact]
    public void ConstructorFieldDenialCommitsNoUnpublishedStorage()
    {
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024) },regexProvider:Utf8RegexProvider.Instance);
        var before = root.MemoryGovernor.CurrentCommittedBytes;
        using (root.MemoryGovernor.ReserveTemporary(1024 - root.MemoryGovernor.CurrentAccountedBytes - PyProperty.ShellBytes, Span))
        {
            Assert.Throws<LythonRuntimeException>(() => PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, new BigInteger(9), root, Span));
            Assert.Equal(before, root.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CollectedDescriptorStaysBatchOwnedUntilCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var weak = MakeDiscarded(root);
        var funded = budget.ReservedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(funded, budget.ReservedBytes);
        Assert.True(funded >= 384);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> MakeDiscarded(Context root)
    {
        object scalar = new BigInteger(9);
        return new WeakReference<object>(PyProperty.Create(scalar, scalar, scalar, scalar, root, Span));
    }

    [Theory]
    [InlineData("get", false, false)]
    [InlineData("get", true, false)]
    [InlineData("set", false, false)]
    [InlineData("set", true, false)]
    [InlineData("delete", false, false)]
    [InlineData("delete", true, false)]
    [InlineData("get", false, true)]
    [InlineData("get", true, true)]
    [InlineData("set", false, true)]
    [InlineData("set", true, true)]
    [InlineData("delete", false, true)]
    [InlineData("delete", true, true)]
    public async Task CallableAccessorAwaitsWithActualReceiverAndReusableArgumentLease(string kind, bool explicitMethod, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(1024 * 1024), CancellationToken = cancellation.Token
        },regexProvider:Utf8RegexProvider.Instance);
        var started = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Variables["tap"] = new Tap(async (args, context) =>
        {
            Assert.Same(root.Services, context.Services);
            Assert.Same(root.MemoryGovernor, context.MemoryGovernor);
            Assert.Same(active, context.Services.CurrentException);
            Assert.Equal(3, args.Length);
            Assert.Same(root.Variables["accessor"], args[0].Value);
            Assert.Same(root.Variables["target"], args[1].Value);
            if (kind == "set") Assert.Same(root.Variables["payload"], args[2].Value);
            else Assert.Same(PyNone.Instance, args[2].Value);
            started.TrySetResult(context);
            await release.Task;
            Assert.Same(root.Variables["target"], args[1].Value);
            if (kind == "set") Assert.Same(root.Variables["payload"], args[2].Value);
            cancellation.Token.ThrowIfCancellationRequested();
            return new BigInteger(7);
        });
        var propertyArgs = kind switch { "get" => "accessor", "set" => "None,accessor", _ => "None,None,accessor" };
        var operation = explicitMethod ? kind switch
        {
            "get" => "return Target.p.__get__(target,Target)",
            "set" => "Target.p.__set__(target,payload)\n return 7",
            _ => "Target.p.__delete__(target)\n return 7"
        } : kind switch
        {
            "get" => "return target.p",
            "set" => "target.p=payload\n return 7",
            _ => "del target.p\n return 7"
        };
        var source = """
            class Accessor:
                def __get__(self,obj,owner):raise ValueError('accessor must not bind')
                def __call__(self,obj,value=None):return tap(self,obj,value)
            accessor=Accessor()
            """ + "\nclass Target:\n p=property(" + propertyArgs + ")\ntarget=Target()\npayload=[]\ndef alias():return target\ndef run():\n " + operation + "\n";
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        root.Services.SetCurrentException(active);
        var run = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["run"]);
        var alias = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["alias"]);
        var pending = run.InvokeAsync([], Span, root).AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            var paused = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotSame(root, paused);
            Assert.Same(root.Variables["target"], await alias.InvokeAsync([], Span, root));
            if (cancel) cancellation.Cancel();
            release.TrySetResult();
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            else
            {
                Assert.Equal(new BigInteger(7), await pending);
                Assert.Equal(new BigInteger(7), await run.InvokeAsync([], Span, root));
            }
        }
        finally
        {
            release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Same(active, root.Services.CurrentException);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GetterDocumentationReallyAwaitsDuringConstructionAndDerivation(bool derivative, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(1024 * 1024), CancellationToken = cancellation.Token
        },regexProvider:Utf8RegexProvider.Instance);
        var doc = new PyList([], root.MemoryGovernor, Span);
        var enabled = false;
        var started = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        root.Variables["doctap"] = new Tap(async (args, context) =>
        {
            Assert.Same(root.Services, context.Services);
            Assert.Same(root.MemoryGovernor, context.MemoryGovernor);
            Assert.Same(root.Variables["accessor"], Assert.Single(args).Value);
            if (enabled)
            {
                Assert.Same(active, context.Services.CurrentException);
                started.TrySetResult(context);
                await release.Task;
                cancellation.Token.ThrowIfCancellationRequested();
            }
            return doc;
        });
        var source = """
            class Accessor:
                def __getattribute__(self,name):
                    if name=='__doc__':return doctap(self)
                    return object.__getattribute__(self,name)
                def __call__(self,obj):return 7
            accessor=Accessor()
            original=property(accessor)
            def alias():return accessor
            """ + "\ndef build():return " + (derivative ? "original.setter(accessor)" : "property(accessor)") + "\n";
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        var build = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["build"]);
        var alias = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["alias"]);
        root.Services.SetCurrentException(active);
        enabled = true;
        var pending = build.InvokeAsync([], Span, root).AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            var paused = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotSame(root, paused);
            Assert.Same(root.Variables["accessor"], await alias.InvokeAsync([], Span, root));
            if (cancel) cancellation.Cancel();
            release.TrySetResult();
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            else
            {
                var property = Assert.IsType<PyProperty>(await pending);
                Assert.True(LythonRuntime.TryResolveRuntimeMember(property, "__doc__", root, Span, out var actual));
                Assert.Same(doc, actual);
                Assert.True(LythonRuntime.TryResolveRuntimeMember(property, "fget", root, Span, out var getter));
                Assert.Same(root.Variables["accessor"], getter);
            }
        }
        finally
        {
            release.TrySetResult();
            try { await pending; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Same(active, root.Services.CurrentException);
        Assert.Null(root.CurrentExecutableFrame);
    }

    private sealed class Tap(Func<CallArgumentValue[], Context, ValueTask<object>> callback) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The metadata callback must use the asynchronous host path.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context)
            => callback(args, context);
    }
}
