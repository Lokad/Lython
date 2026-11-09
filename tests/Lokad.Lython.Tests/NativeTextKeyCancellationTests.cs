using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NativeTextKeyCancellationTests
{
    private static readonly LythonSourceSpan Span = new(13, 4, 2, 6);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitComparisonUsesCurrentCallerAndFirstRefusal(bool refused)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var next = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            try
            {
                var left = PyString.FromString("same text", context.MemoryGovernor, Span);
                var right = PyString.FromString("same text", context.MemoryGovernor, Span);
                var baseline = budget.ReservedBytes;
                var origin = refused ? Assert.Throws<LythonRuntimeException>(() =>
                    context.MemoryGovernor.Reserve(budget.LimitBytes!.Value + 1, Span)) : null;
                cancellation.Cancel();
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                    PyValueComparer.Instance.Equals(left, right, context, Span));
                Assert.Equal(refused ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                    failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
                if (origin is not null) Assert.Same(origin, failure);
                Assert.True(PyValueComparer.Instance.Equals(left, right, next, Span));
                Assert.True(PyValueComparer.Instance.Equals(left, right));
                Assert.Null(PyStructuralGuard.AmbientContext);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); next.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory, Trait("Category", "Stress")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedTextKeyProbeObservesConcurrentCancellation(bool write)
    {
        const int length = 256 * 1024 * 1024;
        var budget = new LythonMemoryBudget(2L * length + 32 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            PyString CreateInput()
            {
                using var storage = context.MemoryGovernor.ReserveTemporary(PyString.EstimateApproximateBytes(length), Span);
                var bytes = new byte[length];
                Array.Fill(bytes, (byte)'a');
                return PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, Span, storage);
            }
            Task<object>? run = null;
            Task? canceller = null;
            using var started = new ManualResetEventSlim();
            using var cancellerReady = new ManualResetEventSlim();
            var dictionary = new PyDict(context.MemoryGovernor, Span);
            try
            {
                var stored = CreateInput();
                var requested = CreateInput();
                dictionary.SetItem(stored, false);
                // Isolate equality: neither probe may spend its cancellable work hashing.
                Assert.Equal(PyHashProtocols.GetProtocolHash(stored, context, Span),
                    PyHashProtocols.GetProtocolHash(requested, context, Span));
                var baseline = budget.ReservedBytes;
                Assert.Null(PyStructuralGuard.AmbientContext);
                canceller = Task.Factory.StartNew(() =>
                {
                    cancellerReady.Set();
                    Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                    Thread.SpinWait(4096);
                    cancellation.Cancel();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(cancellerReady.Wait(TimeSpan.FromSeconds(10)));
                var requestedAtCompletion = false;
                run = Task.Factory.StartNew<object>(() =>
                {
                    started.Set();
                    try
                    {
                        return write ? dictionary.TryWriteKeyWithoutAdmission(requested, true, context, Span)
                            : dictionary.TryGetValue(requested, out _, context, Span);
                    }
                    finally { requestedAtCompletion = cancellation.IsCancellationRequested; }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var observed = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(10)));
                if (observed is null)
                {
                    Assert.False(requestedAtCompletion, "Text-key probe returned after cancellation was requested.");
                    Assert.True(Assert.IsType<bool>(await run));
                }
                else
                {
                    var failure = Assert.IsType<LythonRuntimeException>(observed);
                    Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    Assert.Equal(Span, failure.Span);
                }
                Assert.Single(dictionary);
                Assert.True(dictionary.TryGetValue(stored, out var value));
                Assert.Equal(write && observed is null, Assert.IsType<bool>(value));
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                cancellation.Cancel();
                started.Set();
                try { if (canceller is not null) await canceller; }
                finally
                {
                    if (run is not null)
                        try { await run; }
                        catch (LythonRuntimeException failure) when (failure.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                    context.State.CloseResources();
                }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
