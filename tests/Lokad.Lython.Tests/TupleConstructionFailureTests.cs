using System.Collections;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TupleConstructionFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NumericTupleAdmissionFailurePreservesOtherFunding(bool cancelled, bool floating)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = gate, CancellationToken = cancellation.Token });
            try
            {
                using var other = context.MemoryGovernor.ReserveTemporary(17, null);
                var baseline = budget.ReservedBytes;
                var committed = context.MemoryGovernor.CurrentCommittedBytes;
                var first = default(LythonRuntime.ExecutableSlot);
                var second = default(LythonRuntime.ExecutableSlot);
                if (floating)
                {
                    first.SetDouble(10001.5);
                    second.SetDouble(10002.5);
                }
                else
                {
                    first.SetInteger(10001);
                    second.SetInteger(10002);
                }
                if (cancelled) gate.AfterGrowth = _ => cancellation.Cancel();
                else gate.RefuseGrowth = _ => true;
                var error = Assert.Throws<LythonRuntimeException>(() =>
                    PyTuple.FromSlots([first, second], context, new(0, 1, 1, 1)));
                Assert.Equal(cancelled ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancelled) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(17, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryFailureRefundsScratchWithoutReleasingOtherStorage(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        var governor = context.MemoryGovernor;
        using (var other = governor.ReserveTemporary(17, null))
        {
            var baseline = budget.ReservedBytes;
            var committed = governor.CurrentCommittedBytes;
            var expected = new InvalidOperationException("factory stopped");
            object Item(int i) => i == 0 ? PyNone.Instance : throw expected;
            async ValueTask<object> ItemAsync(int i)
            {
                await Task.Yield();
                return Item(i);
            }
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (asynchronous) await LythonRuntime.CreateTupleAsync(2, ItemAsync, context, null);
                else LythonRuntime.CreateTuple(2, Item, context, null);
            });
            Assert.Same(expected, failure);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(17, governor.CurrentReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationFailureRefundsConstructionWithoutReleasingAnotherReservation(bool counted)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        using var other = governor.ReserveTemporary(17, null);
        var expected = new InvalidOperationException("source stopped");
        static IEnumerable<object> Values(Exception failure)
        {
            yield return new BigInteger(7);
            throw failure;
        }
        IEnumerable<object> values = Values(expected);
        if (counted) values = new CountedSource(values);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => new PyTuple(values, governor)));
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        var recovered = new PyTuple(new object[] { PyNone.Instance }, governor);
        Assert.Single(recovered);
        Assert.Equal(48, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        GC.KeepAlive(recovered);
    }

    private sealed class CountedSource(IEnumerable<object> source) : IReadOnlyCollection<object>
    {
        public int Count => 2;
        public IEnumerator<object> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
