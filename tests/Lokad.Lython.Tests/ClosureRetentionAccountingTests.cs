using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// MG11: the closure-retention helper owns every retained context along the
// defining chain (base plus per-variable slots), first-wins so shared frames
// pay once, with later definitions paying only for regrowth. Module frames
// count only genuinely module-owned entries: the inherited builtin aliases
// stay owned by the run, while shadowing assignments count normally.
public sealed class ClosureRetentionAccountingTests
{
    private static LythonRuntime.ExecutionContext NewRoot()
        => new(new MockLythonHost(), new LythonRunOptions());

    [Fact]
    public void ModuleRootOwnsOnlyUserEntries()
    {
        var root = NewRoot();
        LythonRuntime.ChargeClosureRetention(root, root.MemoryGovernor, null);
        Assert.Equal(512 + 32, root.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);

        root.Variables["g"] = 1;
        LythonRuntime.ChargeClosureRetention(root, root.MemoryGovernor, null);
        Assert.Equal(512 + 2 * 32, root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ShadowedBuiltinCountsAsOwned()
    {
        var root = NewRoot();
        root.Variables["list"] = new List<object>();
        LythonRuntime.ChargeClosureRetention(root, root.MemoryGovernor, null);
        Assert.Equal(512 + 2 * 32, root.MemoryGovernor.CurrentCommittedBytes);
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
        Assert.Equal((512 + 3 * 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);

        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal((512 + 3 * 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void RegrowthPaysDeltaOnly()
    {
        var root = NewRoot();
        var child = new LythonRuntime.ExecutionContext(root);
        child.Variables["a"] = 1;
        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal((512 + 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);

        child.Variables["b"] = 2;
        child.Variables["c"] = 3;
        LythonRuntime.ChargeClosureRetention(child, root.MemoryGovernor, null);
        Assert.Equal((512 + 3 * 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);
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
        Assert.Equal((512 + 2 * 32) + (512 + 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);

        LythonRuntime.ChargeClosureRetention(leaf, root.MemoryGovernor, null);
        Assert.Equal((512 + 2 * 32) + (512 + 32) + (512 + 32), root.MemoryGovernor.CurrentCommittedBytes);
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
        Assert.Equal(512 + (512 + 32) + (512 + 32), first);

        mid.Variables["b"] = 2;
        var sibling = new LythonRuntime.ExecutionContext(mid);
        LythonRuntime.ChargeClosureRetention(sibling, root.MemoryGovernor, null);
        Assert.Equal(first + 512 + 32, root.MemoryGovernor.CurrentCommittedBytes);
    }
}
