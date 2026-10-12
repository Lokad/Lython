using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class PrimitiveStorageTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var value in new object[] { true, false, (sbyte)-1, (byte)1, (short)-1,
            (ushort)1, -1, (uint)1, (long)-1, (ulong)1, new BigInteger(1), 1.5, new PyDecimal(1.5m) })
            for (var container = 0; container < 3; container++)
                yield return [container, value];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryRetainedNumericKindUsesTheSameOccupiedAllowance(int container, object value)
    {
        var governor = new MemoryGovernor(null);
        var bytes = Make(container, [value, value], governor);
        var baseline = Make(container, [PyNone.Instance, PyNone.Instance], new MemoryGovernor(null));
        Assert.Equal(baseline + (container == 2 ? 64 : 128), bytes);
        Assert.Equal(bytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    private static long Make(int kind, object[] items, MemoryGovernor governor) => kind switch
    {
        0 => new PyList(items, governor).CommittedStorageBytes,
        1 => new PyTuple(items, governor).CommittedStorageBytes,
        _ => new PySet(items, governor).CommittedStorageBytes
    };

    [Fact]
    public void RetainedBooleanBoxesFitWithinTheConservativeStorageEstimate()
    {
        const int count = 4096;
        var items = new object[count];
        Box(false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) items[i] = Box((i & 1) != 0);
        var boxBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        var governor = new MemoryGovernor(null);
        var list = new PyList(items, governor);
        Assert.Equal(count, list.Count);
        Assert.True(governor.CurrentCommittedBytes >= boxBytes,
            $"Retained Boolean boxes alone occupy {boxBytes}, but list storage accounts {governor.CurrentCommittedBytes}.");
        GC.KeepAlive(items);
        GC.KeepAlive(list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Box(bool value) => value;

    [Fact]
    public void SaturatedListTransfersNumericSlotsAndDeniesNewGrowth()
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = new MemoryGovernor(65536);
        var list = new PyList(new object[] { true }, governor);
        var before = governor.CurrentCommittedBytes;
        using (governor.ReserveTemporary(65536 - governor.CurrentAccountedBytes, span))
        {
            object incoming = (ushort)1;
            list.SetIndex(0, incoming);
            Assert.Same(incoming, list[0]);
            Assert.Equal(before, governor.CurrentCommittedBytes);
            var error = Assert.Throws<LythonRuntimeException>(() => list.Add(false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Single(list);
            for (var i = 0; i < 2048; i++)
            {
                list.RemoveAt(0);
                list.Add((byte)1);
            }
            Assert.Equal(before, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaturatedSetEqualityTwinsReleaseTheStoredNumericAllowance(bool booleanStored)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = new MemoryGovernor(65536);
        object stored = booleanStored ? (object)true : BigInteger.One;
        object twin = booleanStored ? BigInteger.One : (object)true;
        var set = new PySet(new[] { stored }, governor);
        var before = governor.CurrentCommittedBytes;
        using (governor.ReserveTemporary(65536 - governor.CurrentAccountedBytes, span))
        {
            Assert.False(set.Add(twin));
            Assert.Equal(before, governor.CurrentCommittedBytes);
            Assert.True(set.Remove(twin));
            Assert.Equal(before - 64, governor.CurrentCommittedBytes);
            Assert.True(set.Add(stored));
            Assert.Equal(before, governor.CurrentCommittedBytes);
            var error = Assert.Throws<LythonRuntimeException>(() => set.Add(false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Single(set);
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(set);
    }

    [Fact]
    public void NumericListAttachmentDeniesBeforePublishingAndCanRetry()
    {
        var list = new PyList(new object[] { true, (byte)1 });
        var denied = new MemoryGovernor(127);
        var error = Assert.Throws<LythonRuntimeException>(() => list.AttachMemoryGovernor(denied));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Null(list.OwnerMemoryGovernor);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        var admitted = new MemoryGovernor(128);
        list.AttachMemoryGovernor(admitted);
        Assert.Same(admitted, list.OwnerMemoryGovernor);
        Assert.Equal(128, admitted.CurrentCommittedBytes);
        list.RemoveAt(0);
        Assert.Equal(64, admitted.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DeniedNumericConstructionRefundsItsPrivateStorage(int kind)
    {
        var needed = Make(kind, new object[] { true, (byte)2 }, new MemoryGovernor(null));
        var governor = new MemoryGovernor(needed - 1);
        var error = Assert.Throws<LythonRuntimeException>(() => Make(kind, new object[] { true, (byte)2 }, governor));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
