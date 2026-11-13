using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TemporaryReservationOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(17, 3, 2, 1);

    [Theory]
    [InlineData(false), InlineData(true)]
    public void RefundReentryCannotReleaseNeighboringStorage(bool throwAfterDispose)
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            using var working = governor.ReserveTemporary(64, Span);
            using var neighbor = governor.ReserveTemporary(96, Span);
            var observed = false;
            var callbackFailure = new InvalidOperationException("controlled refund callback failure");
            hook.AfterShrink = _ =>
            {
                hook.AfterShrink = null;
                observed = true;
                working.Dispose();
                if (throwAfterDispose) throw callbackFailure;
            };
            if (throwAfterDispose) Assert.Same(callbackFailure,
                Assert.Throws<InvalidOperationException>(() => working.Shrink(16)));
            else working.Shrink(16);
            Assert.True(observed);
            Assert.Equal(96, governor.CurrentReservedBytes);
            Assert.Equal(96, budget.ReservedBytes);
            working.Dispose();
            Assert.Equal(96, budget.ReservedBytes);
            neighbor.Commit(96);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(96, governor.CurrentCommittedBytes);
            Assert.Equal(96, budget.ReservedBytes);
            governor.Release(96);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0), InlineData(1), InlineData(2)]
    public void AdmissionDisposalPreventsAllocationAndPreservesFirstRefusal(int failureKind)
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            using var working = governor.ReserveTemporary(64, Span);
            using var neighbor = governor.ReserveTemporary(96, Span);
            LythonRuntimeException? first = null;
            var observed = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                observed = true;
                working.Dispose();
                if (failureKind == 1) first = governor.RefuseAdmission("controlled nested refusal", 777, Span);
                if (failureKind == 2) hook.AfterShrink = _ =>
                {
                    hook.AfterShrink = null;
                    first = governor.RefuseAdmission("controlled growth refund refusal", 888, Span);
                };
            };
            var allocated = false;
            void Construct()
            {
                working.Grow(32, Span);
                allocated = true;
            }
            if (failureKind != 0)
            {
                var error = Assert.Throws<LythonRuntimeException>(Construct);
                Assert.Same(first, error);
            }
            else Assert.Throws<ObjectDisposedException>(Construct);
            Assert.True(observed);
            Assert.False(allocated);
            Assert.Equal(96, governor.CurrentReservedBytes);
            Assert.Equal(96, budget.ReservedBytes);
            working.Dispose();
            Assert.Equal(96, budget.ReservedBytes);
            neighbor.Dispose();
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, budget.ReservedBytes);
            if (failureKind != 0) Assert.Same(first, governor.AdmissionFailure);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
