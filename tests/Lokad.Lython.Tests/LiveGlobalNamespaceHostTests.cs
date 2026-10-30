using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class LiveGlobalNamespaceHostTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    private static async Task<Context> Setup(string source)
    {
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        await Execute(source, root);
        return root;
    }

    private static async Task Execute(string source, Context root)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
    }

    private static MemoryGovernor.TemporaryMemoryReservation PinRemaining(Context root)
        => root.MemoryGovernor.ReserveTemporary(((LythonMemoryBudget)root.MemoryGovernor.Budget!).LimitBytes!.Value - root.MemoryGovernor.CurrentAccountedBytes, Span);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PublishedNamespaceFundingCannotBeTakenByAnotherBudgetUser(int initialNames)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var observed = new CompetingBudget(owner, budget);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = observed });
        var storage = new LythonRuntime.GlobalNamespaceStorage(context, Span);
        for (var i = 0; i < initialNames; i++) storage.AddNew(PyString.FromString("existing" + i), BigInteger.One);
        observed.Armed = true;
        try
        {
            storage.AddNew(PyString.FromString("new_name"), new BigInteger(42));
            Assert.Equal(new BigInteger(42), context.Variables["new_name"]);
            Assert.Equal(new BigInteger(42), storage.GetRequired(PyString.FromString("new_name")));
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(context.MemoryGovernor.CurrentCommittedBytes + (observed.Competitor?.Bytes ?? 0), budget.ReservedBytes);
        }
        finally { observed.Competitor?.Dispose(); }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class CompetingBudget(ILythonMemoryBudget owner, LythonMemoryBudget root) : ILythonMemoryBudget
    {
        private LythonMemoryBudget Root => root;
        internal bool Armed;
        internal ILythonMemoryReservation? Competitor;
        public ILythonMemoryScope CreateScope() => owner.CreateScope();
        public bool TryReserve(long bytes, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!owner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new Reservation(this, admitted);
            return true;
        }
        private sealed class Reservation(CompetingBudget probe, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = Bytes;
                var accepted = inner.TryResize(bytes);
                if (accepted && bytes < previous && probe.Armed)
                {
                    probe.Armed = false;
                    Assert.True(probe.Root.TryReserve(probe.Root.LimitBytes!.Value - probe.Root.ReservedBytes, out probe.Competitor));
                }
                return accepted;
            }
            public void Dispose() => inner.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedNewBuiltinShadowKeepsActualNamespaceAndReleasesReservation(bool sourceStore)
    {
        var root = await Setup("scope=globals()\n");
        var scope = Assert.IsType<PyDict>(root.Variables["scope"]);
        var prior = LythonRuntime.ResolveName("len", Span, root);
        var replacement = new PyList([], root.MemoryGovernor, Span);
        using (PinRemaining(root))
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (sourceStore) LythonRuntime.StoreName("len", replacement, root, Span);
                else scope.SetItem(PyString.FromString("len"), replacement);
            });
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.False(root.Variables.ContainsKey("len"));
            Assert.Same(prior, LythonRuntime.ResolveName("len", Span, root));
            Assert.False(scope.ContainsKey(PyString.FromString("len")));
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(replacement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacingExistingBuiltinAliasReusesItsFundedBinding(bool sourceStore)
    {
        var root = await Setup("scope=globals()\nscope['len']=len\n");
        var scope = Assert.IsType<PyDict>(root.Variables["scope"]);
        var replacement = new PyList([], root.MemoryGovernor, Span);
        using (PinRemaining(root))
        {
            if (sourceStore) LythonRuntime.StoreName("len", replacement, root, Span);
            else scope.SetItem(PyString.FromString("len"), replacement);
            Assert.Same(replacement, root.Variables["len"]);
            Assert.Same(replacement, scope.GetItem(PyString.FromString("len")));
            Assert.Null(root.MemoryGovernor.AdmissionFailure);
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(replacement);
    }

    [Fact]
    public async Task DeniedNewNameAndClearLeaveBindingsAndIterationIntact()
    {
        var root = await Setup("value=3\nscope=globals()\n");
        var scope = Assert.IsType<PyDict>(root.Variables["scope"]);
        var key = PyString.FromString("new_name", root.MemoryGovernor, Span);
        var keys = scope.Keys.ToArray();
        using (PinRemaining(root))
        {
            Assert.Throws<LythonRuntimeException>(() => scope.SetItem(key, PyNone.Instance));
            Assert.False(root.Variables.ContainsKey("new_name"));
            Assert.False(scope.ContainsKey(key));
            Assert.Throws<LythonRuntimeException>(() => scope.Clear());
            Assert.Equal(keys, scope.Keys);
            Assert.Equal(new BigInteger(3), root.Variables["value"]);
        }
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(key);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("keys")]
    [InlineData("values")]
    [InlineData("items")]
    public async Task SourceStoreInvalidatesAlreadyCreatedDictionaryAndViewIterators(string name)
    {
        var root = await Setup("scope=globals()\nkeys=scope.keys()\nvalues=scope.values()\nitems=scope.items()\n");
        using var cursor = PyIteration.Cursor.Create(root.Variables[name], Span, root);
        LythonRuntime.StoreName("new_name", new BigInteger(9), root, Span);
        var error = Assert.Throws<LythonRuntimeException>(() => cursor.TryMoveNext(out _));
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Equal("dictionary changed size during iteration", error.Message);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public async Task RemovingBindingDoesNotKeepGuestValueInNamespaceMetadata()
    {
        var root = await Setup("value=[1]\nscope=globals()\n");
        var scope = Assert.IsType<PyDict>(root.Variables["scope"]);
        var value = RemoveValue(root, scope);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(value.TryGetTarget(out _));
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.True(root.MemoryGovernor.CurrentCommittedBytes >= scope.CommittedStorageBytes);
        GC.KeepAlive(scope);
        GC.KeepAlive(root);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RemoveValue(Context root, PyDict scope)
    {
        var value = Assert.IsType<PyList>(root.Variables["value"]);
        var weak = new WeakReference<object>(value);
        Assert.True(scope.Remove(PyString.FromString("value")));
        Assert.False(root.Variables.ContainsKey("value"));
        return weak;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedFunctionSharesActualModuleViewAcrossReentryAndCancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(1024 * 1024), CancellationToken = cancellation.Token
        },regexProvider:Utf8RegexProvider.Instance);
        var started = new TaskCompletionSource<Context>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        root.Variables["pause"] = new Pause(async context =>
        {
            Assert.Same(root.Services, context.Services);
            Assert.Same(root.MemoryGovernor, context.MemoryGovernor);
            started.TrySetResult(context);
            await release.Task;
            context.CheckExecution(Span);
            return PyNone.Instance;
        });
        await Execute("value=1\nscope=globals()\ndef read():return value\ndef run():\n local=7\n pause()\n return (value,globals())\n", root);
        var scope = Assert.IsType<PyDict>(root.Variables["scope"]);
        var run = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["run"]);
        var read = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["read"]);
        var pending = run.InvokeAsync([], Span, root).AsTask();
        var paused = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.NotSame(root, paused);
            scope.SetItem(PyString.FromString("value"), new BigInteger(9));
            Assert.Equal(new BigInteger(9), await read.InvokeAsync([], Span, root));
            Assert.False(scope.ContainsKey(PyString.FromString("local")));
            if (cancel) cancellation.Cancel();
            release.TrySetResult();
            if (cancel)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Equal("RuntimeError", error.ExceptionType);
                Assert.Equal("execution canceled", error.Message);
            }
            else
            {
                var returned = Assert.IsType<PyTuple>(await pending);
                Assert.Equal(new BigInteger(9), returned[0]);
                Assert.Same(scope, returned[1]);
            }
        }
        finally
        {
            release.TrySetResult();
            try { await pending; }
            catch (LythonRuntimeException error) when (cancellation.IsCancellationRequested && error.Message == "execution canceled") { }
        }
        Assert.Same(scope, root.GlobalNamespace);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Null(root.CurrentExecutableFrame);
    }

    private sealed class Pause(Func<Context, ValueTask<object>> callback) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The host callback must suspend asynchronously.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => callback(context);
    }
}
