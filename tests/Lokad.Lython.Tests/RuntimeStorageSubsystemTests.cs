using System.Numerics;
using Lokad.Lython.Tests.Harness;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class RuntimeStorageSubsystemTests
{
    [Fact]
    public void ArrayPyListStorage_ClonesIndependently()
    {
        IPyListStorage storage = new ArrayPyListStorage([new BigInteger(1), PyString.FromString("x")]);
        var clone = storage.Clone();

        clone[0] = new BigInteger(9);
        clone.Add(true);

        Assert.Equal(new BigInteger(1), storage[0]);
        Assert.Equal(2, storage.Count);
        Assert.Equal(3, clone.Count);
        Assert.Equal(new BigInteger(9), clone[0]);
    }

    [Fact]
    public void PyList_UpgradesFromSmallStorageWhenCapacityIsExceeded()
    {
        var list = new PyList(Enumerable.Range(0, PyListStorage.SmallCapacity).Select(i => (object)new BigInteger(i)));

        list.Add(new BigInteger(99));

        Assert.Equal(PyListStorage.SmallCapacity + 1, list.Count);
        Assert.Equal(new BigInteger(99), list[list.Count - 1]);
    }

    [Fact]
    public void PyList_GovernedGrowthFailsBeforePromotionAllocationCanRunAway()
    {
        var list = new PyList(Enumerable.Range(0, PyListStorage.SmallCapacity).Select(i => (object)new BigInteger(i)));
        list.AttachMemoryGovernor(new MemoryGovernor(32), null);

        var ex = Assert.Throws<LythonRuntimeException>(() => list.Add(new BigInteger(99)));

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.Contains("execution memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PyTuple_GovernedCreationFailsBeforeBackingArrayCanRunAway()
    {
        var values = Enumerable.Range(0, 8).Select(i => (object)new BigInteger(i)).ToArray();

        var ex = Assert.Throws<LythonRuntimeException>(() => new PyTuple(values, new MemoryGovernor(64), null));

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.Contains("execution memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PyDict_GovernedGrowthFailsBeforePromotionAllocationCanRunAway()
    {
        var dict = new PyDict();
        dict.AttachMemoryGovernor(new MemoryGovernor(64), null);
        for (var i = 0; i < PyDictStorage.SmallCapacity; i++)
        {
            dict.SetItem(new BigInteger(i), new BigInteger(i));
        }

        var ex = Assert.Throws<LythonRuntimeException>(() => dict.SetItem(new BigInteger(99), new BigInteger(99)));

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.Contains("execution memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PySet_GovernedGrowthFailsBeforeHashStorageCanRunAway()
    {
        var set = new PySet();
        set.AttachMemoryGovernor(new MemoryGovernor(48), null);

        var ex = Assert.Throws<LythonRuntimeException>(() =>
        {
            for (var i = 0; i < 8; i++)
            {
                set.Add(new BigInteger(i));
            }
        });

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.Contains("execution memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PyDict_GovernedCopyConstructionFailsBeforeLargeCloneCanRunAway()
    {
        var source = new PyDict();
        for (var i = 0; i < 12; i++)
        {
            source.SetItem(new BigInteger(i), new BigInteger(i));
        }

        var ex = Assert.Throws<LythonRuntimeException>(() => new PyDict(source, new MemoryGovernor(64), null));

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.Contains("execution memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PyDict_UsesSmallStorageUntilCapacityBoundary()
    {
        var dict = new PyDict();
        for (var i = 0; i < PyDictStorage.SmallCapacity; i++)
        {
            dict.SetItem(PyString.FromString("k" + i), new BigInteger(i));
        }

        dict.SetItem(PyString.FromString("overflow"), new BigInteger(99));

        Assert.Equal(PyDictStorage.SmallCapacity + 1, dict.Count);
        Assert.Equal(new BigInteger(99), dict.GetItem(PyString.FromString("overflow")));
    }

    [Fact]
    public void SmallPyDictStorage_SetItemReportsAddVersusUpdate()
    {
        IPyDictStorage storage = new SmallPyDictStorage();
        var key = PyString.FromString("name");

        Assert.True(storage.SetItem(key, PyString.FromString("lokad")));
        Assert.False(storage.SetItem(key, PyString.FromString("updated")));
        Assert.Equal(PyString.FromString("updated"), storage.GetRequired(key));
        Assert.Equal(1, storage.Count);
    }

    [Fact]
    public void MemoryGovernor_TracksExplicitReserveCommitReleaseCounters()
    {
        var governor = new MemoryGovernor(256);
        var diagnostics = new LegacyApproximateMemoryDiagnostics(256);

        governor.Reserve(80, null);
        governor.Commit(48);
        governor.Commit(16);
        governor.Release(24);
        diagnostics.TrackBytes(32, null);

        Assert.Equal(16, governor.CurrentReservedBytes);
        Assert.Equal(80, governor.PeakReservedBytes);
        Assert.Equal(40, governor.CurrentCommittedBytes);
        Assert.Equal(64, governor.PeakCommittedBytes);
        Assert.Equal(56, governor.CurrentLiveBytes);
        Assert.Equal(80, governor.PeakLiveBytes);
        Assert.Equal(32, diagnostics.CurrentBytes);
        Assert.Equal(32, diagnostics.PeakBytes);
    }

    [Fact]
    public void GovernedValueAndStorageAllocations_ContributeToCommittedBytes()
    {
        var governor = new MemoryGovernor(4096);

        _ = new PyBytes(new byte[16], governor, null);
        _ = new PyTuple(Enumerable.Range(0, 4).Select(i => (object)new BigInteger(i)), governor, null);
        var list = new PyList([], governor, null);
        for (var i = 0; i < 12; i++)
        {
            list.Add(new BigInteger(i));
        }

        Assert.True(governor.CurrentCommittedBytes > 0);
        Assert.True(governor.PeakCommittedBytes >= governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.True(governor.CurrentLiveBytes >= governor.CurrentCommittedBytes);
    }

    [Fact]
    public void GovernedMutableContainers_ReleaseCommittedBytesWhenCleared()
    {
        var governor = new MemoryGovernor(4096);

        var list = new PyList([], governor, null);
        for (var i = 0; i < 12; i++)
        {
            list.Add(new BigInteger(i));
        }

        var dict = new PyDict(governor, null);
        for (var i = 0; i < 8; i++)
        {
            dict.SetItem(new BigInteger(i), new BigInteger(i));
        }

        var set = new PySet(governor, null);
        for (var i = 0; i < 8; i++)
        {
            set.Add(new BigInteger(i));
        }

        Assert.True(governor.CurrentCommittedBytes > 0);

        list.Clear();
        dict.Clear();
        set.Clear();

        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void Utf8ValueBuilder_ToPyStringAddsGovernedStringChargeWithoutConsumingBuilder()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new Utf8ValueBuilder(governor, null, capacity: 3);

        builder.AppendAscii("ab");
        builder.AppendString("c");

        Assert.Equal(3, governor.CurrentCommittedBytes);

        var value = builder.ToPyString();

        Assert.Equal("abc", value.AsString());
        Assert.Equal(governor, value.OwnerMemoryGovernor);
        Assert.Equal(3, builder.Length);
        Assert.Equal(3 + PyString.EstimateApproximateBytes(3), governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void Utf8ValueBuilder_ToPyStringChargesCopyForTrimmedGovernedBuffer()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new Utf8ValueBuilder(governor);

        builder.AppendAscii("ab");
        builder.AppendString("c");

        Assert.Equal(8, governor.CurrentCommittedBytes);

        var value = builder.ToPyString();

        Assert.Equal("abc", value.AsString());
        Assert.Equal(governor, value.OwnerMemoryGovernor);
        Assert.Equal(3, builder.Length);
        Assert.Equal(8 + PyString.EstimateApproximateBytes(3), governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void Utf8ValueBuilder_ToArrayReturnsUtf8AndChargesGovernedCopy()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new Utf8ValueBuilder(governor);

        builder.AppendAscii("ab");
        builder.Append(PyString.FromString("c"));

        Assert.Equal(8, governor.CurrentCommittedBytes);

        var bytes = builder.ToArray();

        Assert.Equal("abc"u8.ToArray(), bytes);
        Assert.Equal(3, builder.Length);
        Assert.Equal(11, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void Utf8ValueBuilder_GrowthAccountsForTheTransientReplacementPeak()
    {
        var governor = new MemoryGovernor(20);
        var builder = new Utf8ValueBuilder(governor, capacity: 8);
        builder.AppendAscii("12345678");

        var exception = Assert.Throws<LythonRuntimeException>(() => builder.Append((byte)'9'));

        Assert.Equal("MemoryError", exception.ExceptionType);
        Assert.Equal(8, governor.CurrentCommittedBytes);
        Assert.Equal(8, builder.Length);
    }

    [Fact]
    public void Utf8ValueBuilder_ToArrayAndReleaseDropsTheBuilderCapacity()
    {
        var governor = new MemoryGovernor(32);
        var builder = new Utf8ValueBuilder(governor, capacity: 8);
        builder.AppendAscii("abc");

        var result = builder.ToArrayAndRelease();

        Assert.Equal("abc"u8.ToArray(), result);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void ExecutionServices_GovernedValuesDoNotInflateLegacyApproximateDiagnostics()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 4096
        });
        var services = new ExecutionServices(state);

        services.ObserveString(PyString.FromString("governed", state.MemoryGovernor, null), null);
        services.ObserveValue(new PyList([new BigInteger(1), new BigInteger(2)], state.MemoryGovernor, null), null);
        services.ObserveCollectionCount(64, null);

        Assert.Equal(0, state.LegacyApproximateMemoryDiagnostics.CurrentBytes);
    }

    [Fact]
    public void ExecutionServices_UngovernedValuesStillUseLegacyApproximateDiagnostics()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 4096
        });
        var services = new ExecutionServices(state);

        services.ObserveString(PyString.FromString("plain"), null);
        services.ObserveValue(new PyList([new BigInteger(1), new BigInteger(2)]), null);

        Assert.True(state.LegacyApproximateMemoryDiagnostics.CurrentBytes > 0);
        Assert.Equal(state.LegacyApproximateMemoryDiagnostics.CurrentBytes, state.LegacyApproximateMemoryDiagnostics.PeakBytes);
    }
}
