using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ProductAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(20, 8, 3, 4);
    private static readonly Type Math = typeof(LythonRuntime).GetNestedType("MathModule", BindingFlags.NonPublic)!;
    private static readonly Func<object[], LythonSourceSpan, Context, object> Product = Math
        .GetMethod("Prod", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, object>>();
    private static readonly Func<object[], LythonSourceSpan, Context, ValueTask<object>> ProductAsync = Math
        .GetMethod("ProdAsync", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, ValueTask<object>>>();

    [Theory, InlineData(false), InlineData(true)]
    public async Task ApprovedWorkingStorageFundsTheRetainedProduct(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook });
            var factor = BigInteger.One << 1024;
            _ = LythonRuntime.OwnFreshInteger(factor, context.MemoryGovernor, Span);
            var input = new PyTuple([factor], context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var reached = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                reached = true;
                // One granted working lease must remain usable until the
                // result takes ownership, even if no further growth is allowed.
                hook.RefuseGrowth = _ => true;
            };
            try
            {
                var result = await Invoke(input, factor, context, asynchronous);
                Assert.True(reached);
                Assert.Equal(factor * factor, Assert.IsType<BigInteger>(result));
                var resultBytes = RuntimeMemoryEstimates.EstimateBigIntegerBytes((BigInteger)result);
                Assert.Equal(committed + resultBytes, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(held + resultBytes, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(factor, Assert.IsType<BigInteger>(input[0]));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(1024 * 1024, out var reusable));
        reusable!.Dispose();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task CancellationAtAdmissionDoesNotCommitAProduct(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var factor = BigInteger.One << 1024;
            _ = LythonRuntime.OwnFreshInteger(factor, context.MemoryGovernor, Span);
            var input = new PyTuple([factor], context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var reached = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                reached = true;
                cancellation.Cancel();
            };
            try
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => Invoke(input, factor, context, asynchronous));
                Assert.True(reached);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Equal(Span, error.Span);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(factor, Assert.IsType<BigInteger>(input[0]));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task RefusalPreservesInputAndTheFirstFailure(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook });
            var factor = BigInteger.One << 1024;
            _ = LythonRuntime.OwnFreshInteger(factor, context.MemoryGovernor, Span);
            var input = new PyTuple([factor], context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            try
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => Invoke(input, factor, context, asynchronous));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(Span, error.Span);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(factor, Assert.IsType<BigInteger>(input[0]));
                Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(1, Span)));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async Task<object> Invoke(object input, object start, Context context, bool asynchronous)
        => asynchronous ? await ProductAsync([input, start], Span, context)
            : Product([input, start], Span, context);
}
