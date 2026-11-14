using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LiteralTupleOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData("slice", false)]
    [InlineData("concat", false)]
    [InlineData("slice", true)]
    [InlineData("concat", true)]
    public void DerivedStorageUsesCurrentExecutionAndStopsAtRefusedOrCancelledAdmission(string operation, bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var literalOwner = budget.CreateScope())
        using (var executionOwner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            // Like a compiled immutable literal: its borrowed bytes are funded,
            // without making that owner the allocator for subsequent operations.
            Assert.True(literalOwner.TryReserve(PyTuple.EstimateApproximateBytes(2048), out _));
            var items = Enumerable.Repeat<object>(PyNone.Instance, 2048).ToArray();
            var literal = PyTuple.FromOwnedArray(items);
            var hook = new AdmissionHookMemoryBudget(executionOwner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var baseline = budget.ReservedBytes;
            if (cancel) hook.AfterGrowth = _ => cancellation.Cancel();
            else hook.RefuseGrowth = _ => true;
            try
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Derive(literal, operation, context));
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(2048, literal.Count);
                Assert.All(literal, item => Assert.Same(PyNone.Instance, item));
            }
            finally { hook.AfterGrowth = null; hook.RefuseGrowth = null; context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("slice")]
    [InlineData("concat")]
    public void DerivedResultHasCurrentOwnerAndKeepsOriginalAliases(string operation)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var literalOwner = budget.CreateScope())
        using (var executionOwner = budget.CreateScope())
        {
            Assert.True(literalOwner.TryReserve(4096, out _));
            var marker = new object();
            var literal = PyTuple.FromOwnedArray([marker, marker, marker]);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = executionOwner });
            var baseline = budget.ReservedBytes;
            try
            {
                var derived = Assert.IsType<PyTuple>(Derive(literal, operation, context));
                Assert.Same(context.MemoryGovernor, derived.OwnerMemoryGovernor);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.All(derived, item => Assert.Same(marker, item));
                Assert.Null(literal.OwnerMemoryGovernor);
                Assert.All(literal, item => Assert.Same(marker, item));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Derive(PyTuple literal, string operation, LythonRuntime.ExecutionContext context)
        => operation == "concat"
            ? LythonRuntime.EvaluateBinaryOperator(BinaryOperatorSyntax.Add, literal, literal, context, Span)
            : PyIndexing.ReadSlice(literal, BigInteger.Zero, null, new BigInteger(2), Span, context);
}
