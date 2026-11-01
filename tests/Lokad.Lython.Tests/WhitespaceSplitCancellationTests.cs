using System.Numerics;
using System.Text;
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
    [InlineData(false, -1)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public void CancellationBetweenWordsStopsTheNextNonallocatingScan(bool reverse, int count)
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
            hook.CancelOnPayload(cancellation, Encoding.UTF8.GetByteCount(Word));
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

    [Fact]
    public async Task SuspendedIndexProtocolKeepsCancellationContextThroughTheScan()
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
            context.Variables["arm"] = new Arm(() =>
                hook.CancelOnPayload(cancellation, Encoding.UTF8.GetByteCount(Word)));
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
            await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, context);
            var input = PyString.FromString(Word + " " + Word, context.MemoryGovernor, Span);
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await LythonRuntime.StringMembers.SplitAsync(input, PyNone.Instance, context.Variables["count"], Span, context));
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
        Assert.False(hook.PayloadAdmittedAfterCancellation);
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
        internal bool PayloadAdmittedAfterCancellation { get; private set; }
        internal void CancelOnPayload(CancellationTokenSource cancellation, int payloadBytes)
            => _afterGrowth = bytes =>
            {
                if (bytes < payloadBytes) return;
                if (cancellation.IsCancellationRequested) PayloadAdmittedAfterCancellation = true;
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
