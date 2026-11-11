using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Instance table capacity remains owned after deletion and can be reused;
/// overwrites do not grow storage and owner cleanup returns the allowance.
/// </summary>
public sealed class InstanceAttributeAccountingTests
{
    private static PyType NewType() => new("A", [], new Dictionary<string, object>());

    [Fact]
    public void AttributeDeletionReusesFundedCapacityUntilOwnerCleanup()
    {
        var host = new MockLythonHost();
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyInstance(NewType(), context.MemoryGovernor, span);
        instance.SetAttribute("x", 1);
        instance.SetAttribute("y", 2);
        var charged = context.MemoryGovernor.CurrentCommittedBytes;
        instance.SetAttribute("x", 3);
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(instance.RemoveAttribute("x"));
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.False(instance.RemoveAttribute("missing"));
        instance.SetAttribute("z", 4);
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(instance.TryGetOwnAttribute("z", out var value));
        Assert.Equal(4, value);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReentrantAttributeGrowthFundsSharedCapacityOnce()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var governor = new MemoryGovernor(hook);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyInstance(NewType(), governor, span);
        hook.AfterGrowth = _ =>
        {
            hook.AfterGrowth = null;
            instance.SetAttribute("inner", 1);
        };
        instance.SetAttribute("outer", 2);
        Assert.True(instance.TryGetOwnAttribute("inner", out var inner));
        Assert.Equal(1, inner);
        Assert.True(instance.TryGetOwnAttribute("outer", out var outer));
        Assert.Equal(2, outer);

        // The same final table needs the same allowance regardless of whether
        // its first capacity was established inside the host's admission hook.
        var controlBudget = new LythonMemoryBudget(4096);
        using var controlOwner = controlBudget.CreateScope();
        var control = new PyInstance(NewType(), new MemoryGovernor(controlOwner), span);
        control.SetAttribute("inner", 1);
        control.SetAttribute("outer", 2);
        Assert.Equal(controlBudget.ReservedBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        controlOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, controlBudget.ReservedBytes);
    }

    [Fact]
    public void ReentrantGrowthCannotPublishBeyondFundedCapacity()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var governor = new MemoryGovernor(hook);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyInstance(NewType(), governor, span);
        hook.AfterGrowth = _ =>
        {
            hook.AfterGrowth = null;
            instance.SetAttribute("one", 1);
            instance.SetAttribute("two", 2);
            instance.SetAttribute("three", 3);
        };
        var failure = Assert.Throws<LythonRuntimeException>(() => instance.SetAttribute("outer", 4));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.False(instance.TryGetOwnAttribute("outer", out _));
        Assert.Equal(instance.CommittedStorageBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusalDuringAttributeRefundPreventsPublication()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var governor = new MemoryGovernor(hook);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var origin = new LythonSourceSpan(12, 4, 3, 2);
        var instance = new PyInstance(NewType(), governor, span);
        LythonRuntimeException? first = null;
        hook.AfterShrink = _ =>
        {
            hook.AfterShrink = null;
            first = governor.RefuseAdmission("controlled refund callback refusal", 17, origin);
        };
        var error = Assert.Throws<LythonRuntimeException>(() => instance.SetAttribute("outer", 1));
        Assert.Same(first, error);
        Assert.Same(origin, error.Span);
        Assert.False(instance.TryGetOwnAttribute("outer", out _));
        Assert.Equal(instance.CommittedStorageBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UngovernedAttributesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var instance = new PyInstance(NewType());
        instance.SetAttribute("x", 1);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
