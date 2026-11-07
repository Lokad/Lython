using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ReentrantAdmissionOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(120, 5, 12, 3);

    [Theory]
    [InlineData(false), InlineData(true)]
    public void AdmittedCallbackCannotRefundTheOuterPendingAllocation(bool temporary)
    {
        const long limit = 4096;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            PyString? nested = null;
            var reached = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                reached = true;
                if (temporary)
                {
                    using var scratch = governor.ReserveTemporary(1024, Span);
                }
                else nested = PyString.FromString(new string('n', 1000), governor, Span);
            };
            var outer = PyString.FromString(new string('o', 2000), governor, Span);
            Assert.True(reached);
            var retainedPayload = outer.Utf8Bytes.Length + (nested?.Utf8Bytes.Length ?? 0);
            var overcommit = budget.TryReserve(limit - retainedPayload + 1, out var unrelated);
            unrelated?.Dispose();
            Assert.False(overcommit, "Shared allowance admitted more than its limit while native strings remain retained.");
            Assert.True(budget.ReservedBytes >= retainedPayload);
            Assert.Equal(new string('o', 2000), outer.AsString());
            if (nested is not null) Assert.Equal(new string('n', 1000), nested.AsString());
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void NestedRefusalUnwindsPendingChargesAndKeepsItsFirstOrigin()
    {
        var budget = new LythonMemoryBudget(2500);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            var nestedSpan = new LythonSourceSpan(410, 3, 41, 2);
            LythonRuntimeException? first = null;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                // Suppression by host instrumentation cannot approve the outer
                // allocation or replace the first refusal during its unwind.
                first = Assert.Throws<LythonRuntimeException>(() =>
                    PyString.FromString(new string('n', 1000), governor, nestedSpan));
            };
            var error = Assert.Throws<LythonRuntimeException>(() => PyString.FromString(new string('o', 2000), governor, Span));
            Assert.Same(first, error);
            Assert.Same(error, governor.AdmissionFailure);
            Assert.Equal(nestedSpan, error.Span);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span)));
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var later = budget.CreateScope())
        {
            var value = PyString.FromString(new string('r', 2000), new MemoryGovernor(later), Span);
            Assert.Equal(2000, value.Utf8Bytes.Length);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CallbackExceptionRefundsUnallocatedOuterStorage()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            var failure = new InvalidOperationException("controlled admission callback failure");
            hook.AfterGrowth = _ => { hook.AfterGrowth = null; throw failure; };
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                PyString.FromString(new string('o', 2000), governor, Span)));
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            var recovered = PyString.FromString(new string('r', 2000), governor, Span);
            Assert.Equal(2000, recovered.Utf8Bytes.Length);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UnfundedSameGovernorReentryFailsBeforeAllocationEvenWhenSuppressed()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            var nestedSpan = new LythonSourceSpan(410, 3, 41, 2);
            LythonRuntimeException? first = null;
            hook.RefuseGrowth = _ =>
            {
                hook.RefuseGrowth = null;
                first = Assert.Throws<LythonRuntimeException>(() =>
                    PyString.FromString(new string('n', 1000), governor, nestedSpan));
                return false;
            };
            var error = Assert.Throws<LythonRuntimeException>(() =>
                PyString.FromString(new string('o', 2000), governor, Span));
            Assert.Same(first, error);
            Assert.Equal(nestedSpan, error.Span);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void BeforeFundingCallbackCanUseAnIndependentGovernorSharingTheAllowance()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            var independent = new MemoryGovernor(owner);
            PyString? nested = null;
            hook.RefuseGrowth = _ =>
            {
                hook.RefuseGrowth = null;
                nested = PyString.FromString(new string('n', 1000), independent, Span);
                return false;
            };
            var outer = PyString.FromString(new string('o', 2000), governor, Span);
            Assert.Equal(2000, outer.Utf8Bytes.Length);
            Assert.Equal(1000, Assert.IsType<PyString>(nested).Utf8Bytes.Length);
            Assert.True(budget.ReservedBytes >= 3000);
            Assert.Null(governor.AdmissionFailure);
            Assert.Null(independent.AdmissionFailure);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void CallbackAfterRefundKeepsItsNewValueFunded(bool temporary)
    {
        const long limit = 4096;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var hook = new ResizeHook(owner);
            var governor = new MemoryGovernor(hook);
            var outer = PyString.FromString(new string('o', 2000), governor, Span);
            PyString? nested = null;
            hook.AfterResize = () =>
            {
                hook.AfterResize = null;
                if (temporary) { using var scratch = governor.ReserveTemporary(1024, Span); }
                nested = PyString.FromString(new string('n', 1000), governor, Span);
            };
            // Relinquish this fresh, unpublished value; it has no later users.
            governor.RefundUnpublishedValue(outer);
            Assert.Equal(1000, Assert.IsType<PyString>(nested).Utf8Bytes.Length);
            var overcommit = budget.TryReserve(limit - nested.Utf8Bytes.Length + 1, out var unrelated);
            unrelated?.Dispose();
            Assert.False(overcommit);
            Assert.True(budget.ReservedBytes >= nested.Utf8Bytes.Length);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Null(governor.AdmissionFailure);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CallbackBeforeRefundCannotAllocateUsingTheRetiringCharge()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new ResizeHook(owner);
            var governor = new MemoryGovernor(hook);
            var outer = PyString.FromString(new string('o', 2000), governor, Span);
            LythonRuntimeException? first = null;
            hook.BeforeResize = () =>
            {
                hook.BeforeResize = null;
                first = Assert.Throws<LythonRuntimeException>(() =>
                    PyString.FromString(new string('n', 1000), governor, Span));
            };
            governor.RefundUnpublishedValue(outer);
            Assert.NotNull(first);
            Assert.Same(first, governor.AdmissionFailure);
            Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span)));
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FailureBeforeFundingDoesNotInvokeAnotherCallbackForUnchangedOwnership()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var hook = new ResizeHook(owner);
            var governor = new MemoryGovernor(hook);
            var failure = new InvalidOperationException("controlled failure before funding");
            var failBeforeFunding = true;
            PyString? unexpected = null;
            hook.BeforeResize = () =>
            {
                if (failBeforeFunding) { failBeforeFunding = false; throw failure; }
                hook.BeforeResize = null;
                unexpected = PyString.FromString(new string('n', 1000), governor, Span);
            };
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                PyString.FromString(new string('o', 2000), governor, Span)));
            Assert.Null(unexpected);
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ResizeHook(ILythonMemoryBudget parent) : ILythonMemoryBudget
    {
        internal Action? BeforeResize, AfterResize;
        public ILythonMemoryScope CreateScope() => parent.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!parent.TryReserve(bytes, out var inner)) { reservation = null; return false; }
            reservation = new Reservation(this, inner);
            return true;
        }
        private sealed class Reservation(ResizeHook hook, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                hook.BeforeResize?.Invoke();
                if (!inner.TryResize(bytes)) return false;
                hook.AfterResize?.Invoke();
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
