using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// MG11: the closure-retention helper owns every retained non-module context
// along the defining chain (base plus per-variable slots), first-wins so
// shared frames pay once, with later definitions paying only for regrowth.
public sealed class ClosureRetentionAccountingTests
{
    private static LythonRuntime.ExecutionContext NewRoot()
        => new(new MockLythonHost(), new LythonRunOptions());

    [Fact]
    public void ModuleRootStaysExempt()
    {
        var root = NewRoot();
        root.Variables["g"] = 1;
        LythonRuntime.ChargeClosureRetention(root, root.MemoryGovernor, null);
        Assert.Equal(0, root.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void NullGovernorPaysNothing()
    {
        var root = NewRoot();
        var child = new LythonRuntime.ExecutionContext(root);
        child.Variables["a"] = 1;
        LythonRuntime.ChargeClosureRetention(child, null, null);
        Assert.Equal(0, root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void SingleContextChargesBasePlusSlotsOnce()
    {
        var root = NewRoot();
        var child = new LythonRuntime.ExecutionContext(root);
        child.Variables["a"] = 1;
        child.Variables["b"] = 2;
        child.Variables["c"] = 3;
        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal(512 + 3 * 32, root.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);

        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal(512 + 3 * 32, root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void RegrowthPaysDeltaOnly()
    {
        var root = NewRoot();
        var child = new LythonRuntime.ExecutionContext(root);
        child.Variables["a"] = 1;
        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal(512 + 32, root.MemoryGovernor.CurrentCommittedBytes);

        child.Variables["b"] = 2;
        child.Variables["c"] = 3;
        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal(512 + 3 * 32, root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ChainWalkSumsUnpaidLevels()
    {
        var root = NewRoot();
        var mid = new LythonRuntime.ExecutionContext(root);
        mid.Variables["a"] = 1;
        mid.Variables["b"] = 2;
        var leaf = new LythonRuntime.ExecutionContext(mid);
        leaf.Variables["c"] = 3;
        LythonRuntime.ChargeClosureRetention(leaf, root.MemoryGovernor, null);
        Assert.Equal((512 + 2 * 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);

        LythonRuntime.ChargeClosureRetention(leaf, root.MemoryGovernor, null);
        Assert.Equal((512 + 2 * 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void SiblingWalkPaysOnlyRegrowth()
    {
        var root = NewRoot();
        var mid = new LythonRuntime.ExecutionContext(root);
        mid.Variables["a"] = 1;
        var leaf = new LythonRuntime.ExecutionContext(mid);
        LythonRuntime.ChargeClosureRetention(leaf, root.MemoryGovernor, null);
        var first = root.MemoryGovernor.CurrentCommittedBytes;

        mid.Variables["b"] = 2;
        var sibling = new LythonRuntime.ExecutionContext(mid);
        LythonRuntime.ChargeClosureRetention(sibling, root.MemoryGovernor, null);
        Assert.Equal(first + 512 + 32, root.MemoryGovernor.CurrentCommittedBytes);
    }
}
