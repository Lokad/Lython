using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DeferredSplitOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static PyList Fresh(MemoryGovernor governor, ChargeReclamationPool pool, int count = 5)
        => LythonRuntime.OwnFreshStringSplitListResult(
            PyStringOps.Split(PyString.FromString(string.Join('|', Enumerable.Repeat("abcd", count))),
                PyString.FromString("|"), governor, Span), Span, pool);

    private static long RegisteredCharges(ChargeReclamationPool pool)
    {
        var entries = new[] { "_young", "_old" }.SelectMany(name =>
            (List<ChargeReclamationPool.ReclamationEntry>)typeof(ChargeReclamationPool)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pool)!);
        return entries.Sum(entry => entry.ValueCharge + ChargeReclamationPool.EntryChargeBytes) + pool.CommittedBackingBytes;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void CountAndNativeBorrowKeepExactAggregateWithoutStringRegistrations()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        Assert.Equal(5, list.Count);
        Assert.Equal(1, pool.Count);
        Assert.True(list.TryBorrowSplitForJoin(out var borrowed));
        var parts = borrowed!.Cast<PyString>().ToArray();
        Assert.All(parts, part => Assert.Null(part.ReclamationEntry));
        Assert.Equal(5, parts.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(parts, part => Assert.Equal("abcd", part.AsString()));
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(list);
    }

    [Theory]
    [InlineData("indexer")]
    [InlineData("sequence")]
    [InlineData("index")]
    [InlineData("array")]
    [InlineData("copy")]
    [InlineData("slice")]
    [InlineData("iteration")]
    public void EveryOrdinaryEscapeAcquiresIndependentIdentities(string path)
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        Assert.True(list.TryBorrowSplitForJoin(out var borrowed));
        var original = borrowed!.ToArray();
        object result = path switch
        {
            "indexer" => list[0],
            "sequence" => list.GetItem(0),
            "index" => list.GetIndex(0),
            "array" => list.ToArray(),
            "copy" => new PyList(list),
            "slice" => list.GetSlice(new[] { 0, 2, 4 }),
            "iteration" => list.First(),
            _ => throw new InvalidOperationException()
        };
        var exposed = path switch { "array" or "copy" => 5, "slice" => 3, _ => 1 };
        Assert.Equal(exposed < 5, list.TryBorrowSplitForJoin(out _));
        Assert.Equal(exposed + 1, pool.Count);
        Assert.Equal(exposed, original.Count(pool.IsTracked));
        var extraBacking = result is PyList copy ? copy.CommittedStorageBytes : 0;
        Assert.Equal(governor.CurrentCommittedBytes - extraBacking, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        for (var i = 0; i < original.Length; i++) Assert.Same(original[i], list[i]);
        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(6, pool.Count);
        Assert.All(original, part => Assert.True(pool.IsTracked(part)));
        GC.KeepAlive(result);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("insert")]
    [InlineData("reverse")]
    [InlineData("repeat")]
    [InlineData("remove")]
    [InlineData("step-delete")]
    [InlineData("replace")]
    public void MutationsPreserveFundingAndIdentityBeforeChangingStorage(string operation)
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        Assert.True(list.TryBorrowSplitForJoin(out var borrowed));
        var original = borrowed!.ToArray();
        switch (operation)
        {
            case "append": list.Add(original[0]); break;
            case "insert": list.Insert(1, original[0]); break;
            case "reverse": list.Reverse(); break;
            case "repeat": list.RepeatInPlace(2, Span); break;
            case "remove": list.RemoveAt(0); break;
            case "step-delete": list.DeleteSlice(new PyIndexing.SliceBounds(0, 5, 2)); break;
            case "replace": list[1] = original[0]; break;
        }

        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(6, pool.Count);
        Assert.All(original, part => Assert.True(pool.IsTracked(part)));
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        if (operation == "repeat")
            for (var i = 0; i < 5; i++) Assert.Same(list[i], list[i + 5]);
        GC.KeepAlive(original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreationDenialRefundsAllUnpublishedPayloadsAndFees(int stage)
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var parts = PyStringOps.Split(PyString.FromString("a|b|c|d|e"), PyString.FromString("|"), governor, Span);
        var fees = 5 * ChargeReclamationPool.EntryChargeBytes + PyList.DeferredSplitStorage.MetadataBytes;
        var required = fees + stage * ChargeReclamationPool.EntryChargeBytes;
        var pressure = 65536 - governor.CurrentCommittedBytes - required + 1;
        governor.Reserve(pressure, Span);
        var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.OwnFreshStringSplitListResult(parts, Span, pool));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, pool.Count);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        governor.ReleaseReserved(pressure);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(parts);
    }

    [Fact]
    public void PartialAcquisitionDenialPreservesExactSnapshotsAndFundedRetry()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        var before = governor.CurrentCommittedBytes;
        var backingBefore = pool.CommittedBackingBytes;
        var pressure = 65536 - before - 31;
        governor.Reserve(pressure, Span);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => { foreach (var _ in list) { } }).ExceptionType);
            Assert.Equal(4, pool.Count);
            Assert.Equal(before, governor.CurrentCommittedBytes);
            Assert.Equal(before, RegisteredCharges(pool));
            Assert.Equal(pressure, governor.CurrentReservedBytes);
            Assert.True(list.TryBorrowSplitForJoin(out _));
        }

        governor.ReleaseReserved(pressure);
        foreach (var _ in list) { }
        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(6, pool.Count);
        Assert.Equal(before + pool.CommittedBackingBytes - backingBefore - PyList.DeferredSplitStorage.MetadataBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(list);
    }

    [Fact]
    public void PendingClearDiscardsAggregateWithoutRegisteringStrings()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool, 32);
        list.Clear();
        Assert.Empty(list);
        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(1, pool.Count);
        Assert.Equal(list.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void ClearingPartiallyEscapedListRefundsOnlyUnpublishedMembers()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        var retained = (PyString)list[0];
        Assert.True(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(2, pool.Count);
        list.Clear();
        Assert.Empty(list);
        Assert.Equal(2, pool.Count);
        Assert.Equal(list.CommittedStorageBytes + retained.CommittedOwnedBytes +
            2 * ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal("abcd", retained.AsString());
        GC.KeepAlive(retained);
    }

    [Fact]
    public void DroppedPendingContainerReclaimsPayloadsAndTicketsExactly()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var weak = DropPending(governor, pool);
        Collect();
        Assert.False(weak.TryGetTarget(out _));
        pool.Sweep(full: true);
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyList> DropPending(MemoryGovernor governor, ChargeReclamationPool pool)
        => new(Fresh(governor, pool));

    [Fact]
    public void RetainingOneEscapedItemDoesNotPinItsParentOrSiblings()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var (retained, parent, siblings) = EscapeOne(governor, pool);
        Collect();
        Assert.False(parent.TryGetTarget(out _));
        Assert.All(siblings, weak => Assert.False(weak.TryGetTarget(out _)));
        pool.Sweep(full: true);
        Assert.Equal(1, pool.Count);
        Assert.Equal(retained.CommittedOwnedBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal("abcd", retained.AsString());
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PyString, WeakReference<PyList>, WeakReference<object>[]) EscapeOne(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var list = Fresh(governor, pool);
        Assert.True(list.TryBorrowSplitForJoin(out var borrowed));
        var siblings = borrowed!.Skip(1).Select(part => new WeakReference<object>(part)).ToArray();
        var retained = (PyString)list[0];
        Assert.Equal(2, pool.Count);
        return (retained, new WeakReference<PyList>(list), siblings);
    }

    [Fact]
    public void EscapedUnicodeCacheUpdatesIndependentEntrySnapshot()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = LythonRuntime.OwnFreshStringSplitListResult(
            PyStringOps.Split(PyString.FromString("éλ🙂|tail"), PyString.FromString("|"), governor, Span), Span, pool);
        var text = (PyString)list[0];
        var before = text.CommittedOwnedBytes;
        pool.TrackFreshString(text.Index(1), Span);
        Assert.True(text.CommittedOwnedBytes > before);
        Assert.Equal(text.CommittedOwnedBytes, text.ReclamationEntry!.ValueCharge);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(list);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void NativeJoinBorrowPreservesFirstAndEvery64PullCancellation(int produced)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        var pool = new ChargeReclamationPool(context.MemoryGovernor);
        var list = Fresh(context.MemoryGovernor, pool, 130);
        Assert.True(list.TryBorrowSplitForJoin(out var borrowed));
        using var iterator = new PyIteration.CheckedSequence(borrowed!, Span, context).GetEnumerator();
        for (var i = 0; i < produced; i++) Assert.True(iterator.MoveNext());
        cancellation.Cancel();
        Assert.Equal("execution canceled", Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext()).Message);
        Assert.Equal(1, pool.Count);
        Assert.True(list.TryBorrowSplitForJoin(out _));
        GC.KeepAlive(list);
    }

    [Fact]
    public void SharedEmptyPartsStayUnownedAndDistinctEqualPartsAcquireSeparately()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = LythonRuntime.OwnFreshStringSplitListResult(
            PyStringOps.Split(PyString.FromString("||a||a|"), PyString.FromString("|"), governor, Span), Span, pool);
        Assert.Equal(6, list.Count);
        Assert.True(list.TryBorrowSplitForJoin(out _));
        Assert.NotSame(list[2], list[4]);
        Assert.Same(PyString.Empty, list[0]);
        Assert.Same(PyString.Empty, list[5]);
        Assert.False(pool.IsTracked(PyString.Empty));
        Assert.Equal(3, pool.Count);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
    }

    [Fact]
    public void UnicodeCacheDenialPreservesSnapshotAndFundedRetry()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = LythonRuntime.OwnFreshStringSplitListResult(
            PyStringOps.Split(PyString.FromString("éλ🙂|tail"), PyString.FromString("|"), governor, Span), Span, pool);
        var text = (PyString)list[0];
        var before = governor.CurrentCommittedBytes;
        var stringBefore = text.CommittedOwnedBytes;
        var pressure = 65536 - before - 1;
        governor.Reserve(pressure, Span);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => text.Index(1)).ExceptionType);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(stringBefore, text.ReclamationEntry!.ValueCharge);
        Assert.Equal(before, RegisteredCharges(pool));
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        governor.ReleaseReserved(pressure);
        pool.TrackFreshString(text.Index(1), Span);
        Assert.Equal(text.CommittedOwnedBytes, text.ReclamationEntry.ValueCharge);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(list);
    }

    [Fact]
    public void AlreadyOwnedItemsRetainExistingRegistration()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString("held", governor);
        pool.TrackFreshString(text, Span);
        var originalEntry = text.ReclamationEntry;
        var list = LythonRuntime.OwnFreshStringSplitListResult(new PyList(new object[] { text }, governor), Span, pool);
        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Same(originalEntry, text.ReclamationEntry);
        Assert.Equal(2, pool.Count);
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
    }

    [Fact]
    public void ZeroRepetitionDiscardsPendingAggregateWithoutStringRegistrations()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var list = Fresh(governor, pool);
        list.RepeatInPlace(0, Span);
        Assert.Empty(list);
        Assert.Equal(1, pool.Count);
        Assert.False(list.TryBorrowSplitForJoin(out _));
        Assert.Equal(governor.CurrentCommittedBytes, RegisteredCharges(pool));
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void PrefundedPublicationRestoresAllocationProgressAfterCaughtDenial()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        var (retained, pressure) = TransferAfterPinnedDenial(governor, pool);
        // Publishing entries used prefunded fees and no tier growth. The parent
        // and sibling are now garbage. This reservation must collect them,
        // despite an earlier caught denial having exhausted the relief gate.
        governor.Reserve(200, Span);
        governor.ReleaseReserved(pressure + 200);
        Assert.Equal(1, pool.Count);
        Assert.Equal(retained.CommittedOwnedBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PyString, long) TransferAfterPinnedDenial(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var list = Fresh(governor, pool, 2);
        governor.LivePoolProvider = () => new[] { pool };
        var pressure = 65536 - governor.CurrentCommittedBytes - 1;
        governor.Reserve(pressure, Span);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => governor.Reserve(2, Span)).ExceptionType);
        return ((PyString)list[0], pressure);
    }
}
