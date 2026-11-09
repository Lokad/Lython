using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ScalarComparisonCancellationTests
{
    private static readonly LythonSourceSpan Span = new(13, 4, 2, 6);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComparisonsUseTheCurrentTokenAndPreserveFirstRefusal(bool refused)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var next = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var left = PyString.FromString("left", context.MemoryGovernor, Span);
            var right = PyString.FromString("right", context.MemoryGovernor, Span);
            var members = new[] { "__eq__", "__ne__", "__lt__", "__le__", "__gt__", "__ge__" }
                .Select(name =>
                {
                    Assert.True(LythonRuntime.StringMembers.TryGetMember(left, name, out var member));
                    return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
                }).ToArray();
            var leftNested = new PyTuple([new PyList([left], context.MemoryGovernor, Span)], context.MemoryGovernor, Span);
            var rightNested = new PyTuple([new PyList([right], context.MemoryGovernor, Span)], context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            LythonRuntimeException? origin = refused ? Assert.Throws<LythonRuntimeException>(() =>
                context.MemoryGovernor.Reserve(budget.LimitBytes!.Value + 1, Span)) : null;
            cancellation.Cancel();
            try
            {
                foreach (var op in new[] { BinaryOperatorSyntax.Equal, BinaryOperatorSyntax.NotEqual,
                    BinaryOperatorSyntax.Less, BinaryOperatorSyntax.LessEqual, BinaryOperatorSyntax.Greater, BinaryOperatorSyntax.GreaterEqual })
                {
                    var sync = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.EvaluateBinaryOperator(op, left, right, context, Span));
                    var async = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                        await LythonRuntime.EvaluateBinaryOperatorAsync(op, left, right, context, Span));
                    foreach (var failure in new[] { sync, async })
                    {
                        Assert.Equal(refused ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                        Assert.Equal(Span, failure.Span);
                        if (origin is not null) Assert.Same(origin, failure);
                    }
                }
                foreach (var member in members)
                    CheckFailure(() => member.Invoke([CallArgumentValue.Positional(right)], Span, context));
                CheckFailure(() => left.Equals(right, context, Span));
                CheckFailure(() => left.Equals(left, context, Span));
                CheckFailure(() => left.Equals(null, context, Span));
                CheckFailure(() => PyString.CompareOrdinal(left, right, context, Span));
                CheckFailure(() => PyString.CompareOrdinal(left, left, context, Span));
                using (PyStructuralGuard.PushAmbient(context, Span))
                {
                    CheckFailure(() => PyEquality.AreEqual(leftNested, rightNested));
                    CheckFailure(() => PyComparison.Compare(leftNested, rightNested, Span));
                    using (PyStructuralGuard.SuppressGuestDispatch())
                    {
                        CheckFailure(() => PyEquality.AreEqual(left, right));
                        CheckFailure(() => PyComparison.Compare(left, right, Span));
                    }
                }
                // Values and cached bound methods retain no previous caller token.
                Assert.False(left.Equals(right));
                Assert.True(PyString.CompareOrdinal(left, right) < 0);
                Assert.False(Assert.IsType<bool>(members[0].Invoke([CallArgumentValue.Positional(right)], Span, next)));
                Assert.False(Assert.IsType<bool>(LythonRuntime.EvaluateBinaryOperator(BinaryOperatorSyntax.Equal, left, right, next, Span)));
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Null(PyStructuralGuard.AmbientContext);
                Assert.False(PyStructuralGuard.GuestDispatchSuppressed);

                void CheckFailure(Func<object> operation)
                {
                    var failure = Assert.Throws<LythonRuntimeException>(operation);
                    Assert.Equal(refused ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    Assert.Equal(Span, failure.Span);
                    if (origin is not null) Assert.Same(origin, failure);
                }
            }
            finally { context.State.CloseResources(); next.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory, Trait("Category", "Stress")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveStringComparisonStopsBeforeReturningItsResult(bool ordered)
    {
        // Keep a real nonallocating comparison longer than a scheduler quantum.
        // This is a concurrent observation, not a deterministic scheduling proof.
        const int length = 256 * 1024 * 1024;
        var budget = new LythonMemoryBudget(2L * length + 32 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            PyString CreateInput()
            {
                using var storage = context.MemoryGovernor.ReserveTemporary(PyString.EstimateApproximateBytes(length), Span);
                var bytes = new byte[length];
                Array.Fill(bytes, (byte)'a');
                return PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, Span, storage);
            }
            var left = CreateInput();
            var right = CreateInput();
            var baseline = budget.ReservedBytes;
            var comparison = ordered ? BinaryOperatorSyntax.Less : BinaryOperatorSyntax.Equal;
            _ = LythonRuntime.EvaluateBinaryOperator(comparison, PyString.FromString("warm"), PyString.FromString("warm"), context, Span);
            using var started = new ManualResetEventSlim();
            using var cancellerReady = new ManualResetEventSlim();
            var canceller = Task.Factory.StartNew(() =>
            {
                cancellerReady.Set();
                Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                Thread.Sleep(1);
                cancellation.Cancel();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task<object>? run = null;
            var requestedAtCompletion = false;
            try
            {
                Assert.True(cancellerReady.Wait(TimeSpan.FromSeconds(10)));
                run = Task.Factory.StartNew(() =>
                {
                    started.Set();
                    try { return LythonRuntime.EvaluateBinaryOperator(comparison, left, right, context, Span); }
                    finally { requestedAtCompletion = cancellation.IsCancellationRequested; }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var observed = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(10)));
                if (observed is null) Assert.Fail($"Comparison returned normally; cancellation requested before completion: {requestedAtCompletion}.");
                var failure = Assert.IsType<LythonRuntimeException>(observed);
                Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
            }
            finally
            {
                cancellation.Cancel();
                started.Set();
                try { await canceller; }
                finally
                {
                    if (run is not null)
                        try { await run; }
                        catch (LythonRuntimeException failure) when (failure.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                    context.State.CloseResources();
                }
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
