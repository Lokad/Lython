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
    public void PyTuple_GovernedUnknownEnumerationStopsBeforeFullMaterialization()
    {
        var yielded = 0;
        var values = Enumerable.Range(0, 1_000).Select(index =>
        {
            yielded++;
            return (object)new BigInteger(index);
        });

        var ex = Assert.Throws<LythonRuntimeException>(() => new PyTuple(values, new MemoryGovernor(256), null));

        Assert.Equal("MemoryError", ex.ExceptionType);
        Assert.True(yielded < 1_000);
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
    public void PyDict_ReassignmentPreservesTheOriginalEqualKeyObject()
    {
        var originalKey = PyString.FromString("key");
        var dict = new PyDict();

        dict.SetItem(originalKey, PyString.FromString("first"));
        dict.SetItem(PyString.FromString("key"), PyString.FromString("second"));

        Assert.Same(originalKey, Assert.Single(dict.Keys));
        Assert.Equal("second", Assert.IsType<PyString>(dict.GetItem(originalKey)).AsString());
    }

    [Fact]
    public void PyDict_ValueIterationObservesReplacementWithoutSnapshotting()
    {
        var first = PyString.FromString("first");
        var second = PyString.FromString("second");
        var dict = new PyDict();
        dict.SetItem(first, BigInteger.One);
        dict.SetItem(second, new BigInteger(2));

        using var values = dict.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        dict.SetItem(second, new BigInteger(3));

        Assert.True(values.MoveNext());
        Assert.Equal(new BigInteger(3), values.Current);
    }

    [Fact]
    public void PyDict_IterationRejectsStructuralMutationBeforeAdvancingStorage()
    {
        var dict = new PyDict();
        dict.SetItem(PyString.FromString("first"), BigInteger.One);

        using var keys = dict.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        dict.SetItem(PyString.FromString("second"), new BigInteger(2));

        var exception = Assert.Throws<LythonRuntimeException>(() => keys.MoveNext());
        Assert.Equal("RuntimeError", exception.ExceptionType);
    }

    [Fact]
    public void MemoryGovernor_TracksExplicitReserveCommitReleaseCounters()
    {
        var governor = new MemoryGovernor(256);

        governor.Reserve(80, null);
        governor.Commit(48);
        governor.Commit(16);
        governor.Release(24);

        Assert.Equal(16, governor.CurrentReservedBytes);
        Assert.Equal(80, governor.PeakReservedBytes);
        Assert.Equal(40, governor.CurrentCommittedBytes);
        Assert.Equal(64, governor.PeakCommittedBytes);
        Assert.Equal(56, governor.CurrentAccountedBytes);
        Assert.Equal(80, governor.PeakAccountedBytes);
    }

    [Fact]
    public void TemporaryMemoryReservation_GrowsAndReleasesWithoutCommitting()
    {
        var governor = new MemoryGovernor(256);

        using (var reservation = governor.ReserveTemporary(64, null))
        {
            reservation.Grow(32, null);

            Assert.Equal(96, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            Assert.Equal(96, governor.CurrentAccountedBytes);
        }

        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        Assert.Equal(96, governor.PeakAccountedBytes);
    }

    [Fact]
    public void StableSortBuffer_RemainsReservedUntilGovernedResultOwnsItsStorage()
    {
        var governor = new MemoryGovernor(4096);
        PyList result;

        using (var buffer = new PyStableSort.Buffer(governor, null))
        {
            for (var i = 11; i >= 0; i--)
            {
                var value = new BigInteger(i);
                buffer.Add(new PyStableSort.Entry(value, value), null);
            }

            buffer.Sort(reverse: false, static (left, right) => (BigInteger)left < (BigInteger)right);

            Assert.True(governor.CurrentReservedBytes > 0);
            result = new PyList(buffer, governor, null);
            Assert.True(governor.CurrentReservedBytes > 0);
            Assert.True(governor.CurrentCommittedBytes > 0);
        }

        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(
            Enumerable.Range(0, 12).Select(static value => (object)new BigInteger(value)).ToArray(),
            result.ToArray());
    }

    [Fact]
    public void StableSortBuffer_ReleasesReservationAfterBudgetFailure()
    {
        var governor = new MemoryGovernor(220);

        using (var buffer = new PyStableSort.Buffer(governor, null))
        {
            buffer.Add(new PyStableSort.Entry(new BigInteger(1), new BigInteger(1)), null);
            var exception = Assert.Throws<LythonRuntimeException>(
                () => buffer.Add(new PyStableSort.Entry(new BigInteger(2), new BigInteger(2)), null));

            Assert.Equal("MemoryError", exception.ExceptionType);
        }

        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public void GovernedHostBytes_ChargesItsLifetimeAndReleasesOnDispose()
    {
        var governor = new MemoryGovernor(128);

        using (var payload = new GovernedHostBytes(new byte[40], governor, null))
        {
            Assert.Equal(72, governor.CurrentAccountedBytes);
            var exception = Assert.Throws<LythonRuntimeException>(
                () => new GovernedHostBytes(new byte[40], governor, null));
            Assert.Equal("MemoryError", exception.ExceptionType);
        }

        Assert.Equal(0, governor.CurrentAccountedBytes);
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
        Assert.True(governor.CurrentAccountedBytes >= governor.CurrentCommittedBytes);
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

        // The cleared list retains its small backing array under charge;
        // only the grown array charges are gone.
        Assert.Equal(64 + (16 * 8), governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void GovernedByteBuilder_ToPyStringAddsGovernedStringChargeWithoutConsumingBuilder()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new GovernedByteBuilder(governor, null, capacity: 3);

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
    public void GovernedByteBuilder_ToPyStringChargesCopyForTrimmedGovernedBuffer()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new GovernedByteBuilder(governor);

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
    public void GovernedByteBuilder_ToPyStringAndReleaseTransfersRetainedBudgetToResult()
    {
        var governor = new MemoryGovernor(4096);
        var builder = new GovernedByteBuilder(governor);

        builder.AppendAscii("ab");
        builder.Append(PyString.FromString("c"));

        Assert.Equal(8, governor.CurrentCommittedBytes);

        var value = builder.ToPyStringAndRelease();

        Assert.Equal("abc", value.AsString());
        Assert.Equal(0, builder.Length);
        Assert.Equal(PyString.EstimateApproximateBytes(3), governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void GovernedByteBuilder_GrowthAccountsForTheTransientReplacementPeak()
    {
        var governor = new MemoryGovernor(20);
        var builder = new GovernedByteBuilder(governor, allocationSpan: null, capacity: 8);
        builder.AppendAscii("12345678");

        var exception = Assert.Throws<LythonRuntimeException>(() => builder.Append((byte)'9'));

        Assert.Equal("MemoryError", exception.ExceptionType);
        Assert.Equal(8, governor.CurrentCommittedBytes);
        Assert.Equal(8, builder.Length);
    }

    [Fact]
    public void GovernedByteBuilder_ToArrayAndReleaseDropsTheBuilderCapacity()
    {
        var governor = new MemoryGovernor(32);
        var builder = new GovernedByteBuilder(governor, allocationSpan: null, capacity: 8);
        builder.AppendAscii("abc");

        var result = builder.ToArrayAndRelease();

        Assert.Equal("abc"u8.ToArray(), result);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void TextFileHandle_RepeatedWritesUseOneGovernedMutableBuffer()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 4_096 });
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/output.txt", context);

        for (var i = 0; i < 200; i++)
        {
            _ = handle.Write(PyString.FromString("x"));
        }

        Assert.True(context.MemoryGovernor.CurrentAccountedBytes > 0);
        _ = handle.Exit();
        Assert.Equal(new string('x', 200), host.ReadText("/output.txt"));
    }

    [Fact]
    public void TextFileHandle_RepeatedFlushesReleasePublishedBufferCapacity()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 96 });
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/output.txt", context);

        for (var i = 0; i < 200; i++)
        {
            _ = handle.Write(PyString.FromString("x"));
            _ = handle.Flush();
        }

        Assert.Equal(new BigInteger(200), handle.Tell());
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        _ = handle.Exit();
        Assert.Equal(new string('x', 200), host.ReadText("/output.txt"));
    }

    [Fact]
    public void TextFileHandle_TellTracksPendingCrLfExpansionIncrementally()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite(
            "/output.txt",
            context,
            LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.PreserveCarriageReturnLineFeed);

        _ = handle.Write(PyString.FromString("a\n"));
        Assert.Equal(new BigInteger(3), handle.Tell());
        _ = handle.Write(PyString.FromString("b\n"));
        Assert.Equal(new BigInteger(6), handle.Tell());

        _ = handle.Exit();
        Assert.Equal("a\r\nb\r\n", host.ReadText("/output.txt"));
    }

    [Fact]
    public async Task TextFileHandle_AsyncLatin1AppendDoesNotRetainOrRewritePrefix()
    {
        var host = new DelayedLythonHost();
        host.SeedBytes("/output.txt", Enumerable.Repeat((byte)'a', 1_000).ToArray());
        var context = new LythonRuntime.ExecutionContext(
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 96 });
        var handle = await LythonRuntime.ExecutionContext.TextFileHandle.ForAppendAsync(
            "/output.txt",
            context,
            LythonRuntime.TextEncodingMode.Latin1,
            LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.TranslateUniversal);

        _ = handle.Write(PyString.FromString("é"));
        await handle.FlushAsync();
        _ = handle.Write(PyString.FromString("!"));
        await handle.FlushAsync();
        await handle.ExitAsync();

        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal([.. Enumerable.Repeat((byte)'a', 1_000), 0xe9, 0x21], host.ReadBytes("/output.txt"));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void ExecutionServices_GovernedValuesDoNotDoubleChargeMemoryGovernor()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 4096
        });
        var services = new ExecutionServices(state);

        var text = PyString.FromString("governed", state.MemoryGovernor, null);
        var list = new PyList([new BigInteger(1), new BigInteger(2)], state.MemoryGovernor, null);
        var accounted = state.MemoryGovernor.CurrentAccountedBytes;

        services.ObserveString(text, null);
        services.ObserveValue(list, null);
        services.ObserveCollectionCount(64, null);

        Assert.Equal(accounted, state.MemoryGovernor.CurrentAccountedBytes);
    }

    [Fact]
    public void ExecutionServices_UngovernedValuesAreCheckedWithoutBeingRetained()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 4096
        });
        var services = new ExecutionServices(state);

        for (var i = 0; i < 100; i++)
        {
            services.ObserveString(PyString.FromString("plain"), null);
            services.ObserveValue(new BigInteger(1), null);
        }

        Assert.Equal(0, state.MemoryGovernor.CurrentAccountedBytes);

        var exception = Assert.Throws<LythonRuntimeException>(
            () => services.ObserveValue(new PyList(Enumerable.Repeat<object>(new BigInteger(1), 256)), null));
        Assert.Equal("MemoryError", exception.ExceptionType);
    }
}
