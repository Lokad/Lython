using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class WhitespaceSplitCancellationTests
{
    private static readonly LythonSourceSpan Span = new(17, 3, 2, 1);
    private static readonly string Word = new('α', 16384);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeparatedOutputAdmissionCancellationUnwindsBeforeConstruction(bool reverse)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString(Word + "::" + Word, context.MemoryGovernor, Span);
            var separator = PyString.FromString("::");
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            hook.CancelOnGrowth(cancellation);
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            var error = Assert.Throws<LythonRuntimeException>(() => reverse
                ? PyStringOps.RSplit(input, separator, -1, context.MemoryGovernor, Span)
                : PyStringOps.Split(input, separator, -1, context.MemoryGovernor, Span));
            AssertCancelled(error, context, cancellation, hook);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(Word + "::" + Word, input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CasedOutputAdmissionCancellationUnwindsBeforeConstruction(bool upper)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString(Word + " " + Word, context.MemoryGovernor, Span);
            hook.CancelOnGrowth(cancellation);
            var error = Assert.Throws<LythonRuntimeException>(() =>
                PyStringOps.SplitCasedWhitespace(input, upper, context.MemoryGovernor, Span, context));
            AssertCancelled(error, context, cancellation, hook);
            Assert.Equal(Word + " " + Word, input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void PreparedCasingStopsBeforeGrowingBufferAfterHeaderCancellation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString("ΑΒΓ Σ", context.MemoryGovernor, Span);
            var fundedInput = budget.ReservedBytes;
            hook.CancelOnGrowth(cancellation);
            try
            {
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                    PyStringOps.PrepareCasedWhitespaceIteration(input, false, context, Span));
                AssertCancelled(failure, context, cancellation, hook);
                Assert.Equal("ΑΒΓ Σ", input.AsString());
            }
            finally { context.State.CloseResources(); }
            Assert.Equal(fundedInput, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CasedScratchRefundCanInterruptPublication(bool refuse)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var governor = context.MemoryGovernor;
            governor.EndWorkspaceReuse();
            var text = new string('Α', 180) + " Σ";
            var input = PyString.FromString(text, governor, Span);
            var baseline = budget.ReservedBytes;
            LythonRuntimeException? first = null;
            hook.AfterShrink = _ =>
            {
                hook.AfterShrink = null;
                if (refuse) first = governor.RefuseAdmission("refused during scratch cleanup", 37, Span);
                else cancellation.Cancel();
            };

            var failure = Assert.Throws<LythonRuntimeException>(() =>
                PyStringOps.SplitCasedWhitespace(input, false, governor, Span, context));
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                failure.Attribution?.Reason);
            if (refuse) Assert.Same(first, failure);
            Assert.Equal(Span, failure.Span);
            Assert.Equal(text, input.AsString());
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public void WhitespaceOutputAdmissionCancellationUnwindsBeforeConstruction(bool reverse, int count)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var text = Word + "\u2003" + Word;
            var input = PyString.FromString(text, context.MemoryGovernor, Span);
            hook.CancelOnGrowth(cancellation);
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (reverse)
                {
                    using var ambient = PyStructuralGuard.PushAmbient(context, Span);
                    PyStringOps.RSplitWhitespace(input, count, context.MemoryGovernor, Span);
                }
                else LythonRuntime.StringMembers.Split(input, PyNone.Instance, new BigInteger(count), Span, context);
            });
            AssertCancelled(error, context, cancellation, hook);
            Assert.Equal(text, input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void WhitespaceOnlyScansObserveTheActiveToken(bool reverse, int count)
    {
        var root = new LythonMemoryBudget(65536);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var input = PyString.FromString(new string('\u2003', 1024), context.MemoryGovernor, Span);
            cancellation.Cancel();
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (reverse) PyStringOps.RSplitWhitespace(input, count, context.MemoryGovernor, Span);
                else PyStringOps.SplitWhitespace(input, count, context.MemoryGovernor, Span);
            });
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(Span, error.Span);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SuspendedIndexProtocolKeepsCancellationAndAdmissionUnwind(int mode)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/ready", "ready");
            var hook = new AdmissionHook(owner);
            var context = new Context(host, new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            context.Variables["arm"] = new Arm(() => hook.CancelOnGrowth(cancellation));
            var frontend = LythonFrontend.Compile("""
                class Count:
                    def __index__(self):
                        with open('/ready') as source:
                            source.read()
                        arm()
                        return 1
                count = Count()
                """);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            await LythonRuntime.ExecuteExecutableCodeObjectAsync(ExecutableScript.Compile(frontend.Script.RequireNotNull()).EntryPoint, context);
            var input = PyString.FromString(Word + " " + Word, context.MemoryGovernor, Span);
            var separator = PyString.FromString(" ");
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (mode == 2)
                    await LythonRuntime.StringMembers.GetMethod("rsplit")!.InvokeAsync(input,
                        new object?[] { separator, context.Variables["count"] }, Span, context);
                else await LythonRuntime.StringMembers.SplitAsync(input, mode == 0 ? PyNone.Instance : separator,
                    context.Variables["count"], Span, context);
            });
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(host.ClosedFileCount > 0);
            AssertCancelled(error, context, cancellation, hook);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private static void AssertCancelled(LythonRuntimeException error, Context context,
        CancellationTokenSource cancellation, AdmissionHook hook)
    {
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(Span, error.Span);
        Assert.False(hook.FurtherGrowthAfterCancellation);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Null(context.CurrentExecutableFrame);
    }

    private sealed class Arm(Action action) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            action();
            return PyNone.Instance;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => new(Invoke(arguments, span, context));
    }

    private sealed class AdmissionHook(ILythonMemoryBudget parent) : ILythonMemoryBudget
    {
        private Action<long>? _afterGrowth;
        internal bool FurtherGrowthAfterCancellation { get; private set; }
        internal void CancelOnGrowth(CancellationTokenSource cancellation)
            => _afterGrowth = bytes =>
            {
                if (bytes <= 0) return;
                if (cancellation.IsCancellationRequested) FurtherGrowthAfterCancellation = true;
                else cancellation.Cancel();
            };
        public ILythonMemoryScope CreateScope() => parent.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!parent.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new Reservation(this, admitted);
            _afterGrowth?.Invoke(bytes);
            return true;
        }
        private sealed class Reservation(AdmissionHook hook, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (bytes > previous) hook._afterGrowth?.Invoke(bytes - previous);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
