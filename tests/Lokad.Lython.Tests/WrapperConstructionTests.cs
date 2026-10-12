using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class WrapperConstructionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InnerDenialPreservesUnrelatedCommittedAndReservedStorage(bool counter)
    {
        var governor = new MemoryGovernor(1024);
        governor.Reserve(128, null);
        governor.Commit(128);
        using var pinned = governor.ReserveTemporary(832, null);
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (counter) _ = new PyCounter(governor);
            else _ = new PyDefaultDict(PyNone.Instance, governor);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(128, governor.CurrentCommittedBytes);
        Assert.Equal(832, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshWrapperRegistrationDenialRefundsAllUnpublishedStorage(bool counter)
    {
        var governor = new MemoryGovernor(256);
        var pool = new ChargeReclamationPool(governor);
        object wrapper = counter ? new PyCounter(governor) : new PyDefaultDict(PyNone.Instance, governor);
        var owned = counter ? ((PyCounter)wrapper).CommittedStorageBytes : ((PyDefaultDict)wrapper).CommittedStorageBytes;
        Assert.Equal(256, owned);
        var error = Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshMutable(wrapper, owned));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyWrapperInnerDenialRefundsItsShell(bool counter)
    {
        var governor = new MemoryGovernor(64);
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (counter) _ = new PyCounter(governor);
            else _ = new PyDefaultDict(PyNone.Instance, governor);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void InvalidDefaultFactoryCommitsNoUnpublishedShell()
    {
        var governor = new MemoryGovernor(1024);
        var error = Assert.Throws<LythonRuntimeException>(() => new PyDefaultDict(BigInteger.Zero, governor));
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void ExplicitCounterCopyInnerDenialRefundsItsShell()
    {
        var original = new PyCounter();
        var governor = new MemoryGovernor(64);
        var error = Assert.Throws<LythonRuntimeException>(() => new PyCounter(original, governor));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(original);
    }

    [Fact]
    public void InheritedCounterCopySnapshotMatchesActualAdmission()
    {
        var governor = new MemoryGovernor(4096);
        var original = new PyCounter(governor);
        original.SetItem(BigInteger.One, PyNone.Instance);
        var before = governor.CurrentCommittedBytes;
        var copy = new PyCounter(original);
        Assert.Equal(copy.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        GC.KeepAlive(original);
        GC.KeepAlive(copy);
    }

    [Fact]
    public void InheritedDefaultDictionaryCopySnapshotMatchesActualAdmission()
    {
        var governor = new MemoryGovernor(4096);
        var original = new PyDict(governor);
        original.SetItem(BigInteger.One, PyNone.Instance);
        var before = governor.CurrentCommittedBytes;
        var copy = new PyDefaultDict(PyNone.Instance, original);
        Assert.Equal(copy.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        GC.KeepAlive(original);
        GC.KeepAlive(copy);
    }

    [Fact]
    public void RepeatedCaughtInvalidFactoriesKeepTheirBoundedBudget()
    {
        var script = new LythonEngine().Compile("""
            import collections
            def build(ctor):
                try:
                    ctor(0)
                except TypeError:
                    return True
                return False
            for i in range(2000):
                assert build(collections.defaultdict)
            return i + 1
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var result = script.Run(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        Assert.True(result.Success, result.Failure?.ExceptionType + ":" + result.Failure?.Message);
        Assert.Equal(new BigInteger(2000), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedFailedPopulationKeepsItsBoundedBudget(bool counter)
    {
        var call = counter ? "ctor(bad())" : "ctor(None, [(1, None), (2,)])";
        var source = "import collections\ndef bad():\n yield 1\n raise ValueError('stop')\ndef build(ctor):\n try:\n  " + call +
            "\n except (TypeError, ValueError):\n  return True\n return False\n" +
            "for i in range(2000):\n assert build(collections." + (counter ? "Counter" : "defaultdict") + ")\nreturn i + 1\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var result = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        Assert.True(result.Success, result.Failure?.ExceptionType + ":" + result.Failure?.Message);
        Assert.Equal(new BigInteger(2000), result.ReturnValue);
    }

    [Fact]
    public void RepeatedDroppedDefaultDictionaryCopiesKeepTheirBoundedBudget()
    {
        var script = new LythonEngine().Compile("""
            from collections import defaultdict
            original = defaultdict(None, [(1, None)])
            for i in range(2000):
                copied = original.copy()
                assert len(copied) == 1
            return len(original)
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var result = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        Assert.True(result.Success, result.Failure?.ExceptionType + ":" + result.Failure?.Message);
        Assert.Equal(BigInteger.One, result.ReturnValue);
    }
}
