using System.Runtime.InteropServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class TerminalStringStorageTests
{
    [Theory]
    [InlineData("abc", "b")]
    [InlineData("café", "é")]
    [InlineData("α😀β", "😀")]
    [InlineData("café😀", "é")]
    [InlineData("😀β😀", "😀")]
    public void ContentOperationsExcludeUnusedInvalidTail(string content, string needle)
    {
        using var builder = new GovernedByteBuilder(128);
        builder.AppendString(content);
        var length = builder.Length;
        builder.Append([0xff, 0xff, 0xff, 0xff]);
        builder.Truncate(length);
        var text = builder.ToPyStringAndRelease();
        var exact = PyString.FromString(content);
        var part = PyString.FromString(needle);

        builder.AppendAscii("replacement");
        builder.Release();
        Assert.Equal(content, text.AsString());
        Assert.Equal(exact.Utf8Bytes.ToArray(), text.Utf8Bytes.ToArray());
        Assert.Equal(exact.Length, text.Length);
        Assert.Equal(content.Length, text.GetUtf16CodeUnitCount());
        var decoded = new char[content.Length];
        Assert.Equal(decoded.Length, text.CopyUtf16To(decoded));
        Assert.Equal(content, new string(decoded));
        Assert.Equal(exact, text);
        Assert.Equal(exact.GetHashCode(), text.GetHashCode());
        Assert.Equal(0, PyString.CompareOrdinal(exact, text));
        Assert.True(PyString.CompareOrdinal(text, exact.Concat(PyString.FromString("x"))) < 0);
        Assert.True(text.StartsWith(text.Index(0)));
        Assert.True(text.EndsWith(text.Index(text.Length - 1)));
        Assert.Equal(exact.Contains(part), text.Contains(part));
        Assert.Equal(exact.Find(part), text.Find(part));
        Assert.Equal(exact.Count(part), text.Count(part));
        Assert.Equal(exact.Concat(part), text.Concat(part));
        Assert.Equal(exact.Repeat(3), text.Repeat(3));
        Assert.Equal(PyStringOps.Lower(exact), PyStringOps.Lower(text));
        Assert.Equal(PyStringOps.Upper(exact), PyStringOps.Upper(text));
        Assert.Equal(PyStringOps.Title(exact), PyStringOps.Title(text));
        Assert.Equal(PyStringOps.Capitalize(exact), PyStringOps.Capitalize(text));
        Assert.Equal(PyStringOps.SwapCase(exact), PyStringOps.SwapCase(text));
        Assert.Equal(exact.Replace(part, PyString.FromString("!")), text.Replace(part, PyString.FromString("!")));
        Assert.Equal(exact.Replace(PyString.Empty, part), text.Replace(PyString.Empty, part));
        Assert.Equal(exact.Slice([0, text.Length - 1]), text.Slice([0, text.Length - 1]));
        Assert.Equal(exact.Slice(new PyIndexing.SliceBounds(0, text.Length, 1)), text.Slice(new PyIndexing.SliceBounds(0, text.Length, 1)));
        Assert.Equal(exact.Slice(new PyIndexing.SliceBounds(text.Length - 1, -1, -1)), text.Slice(new PyIndexing.SliceBounds(text.Length - 1, -1, -1)));
        Assert.Equal(exact.EnumerateRunes(), text.EnumerateRunes());
        Assert.Throws<ArgumentOutOfRangeException>(() => text.SliceByByteRange(0, length + 1));
        for (var rune = 0; rune <= text.Length; rune++)
        {
            var boundary = text.GetByteIndexForRuneBoundary(rune);
            Assert.Equal(exact.GetByteIndexForRuneBoundary(rune), boundary);
            Assert.Equal(rune, text.ByteIndexToRuneIndex(boundary));
            Assert.Equal(exact.GetByteIndexAfterRunes(boundary, 2), text.GetByteIndexAfterRunes(boundary, 2));
            if (rune < text.Length) Assert.Equal(exact.AsString(rune), text.AsString(rune));
        }
    }

    [Fact]
    public void TransferKeepsFullCapacityFundedAfterBuilderReuseAndCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var scope = budget.CreateScope())
        {
            var governor = new MemoryGovernor(scope);
            using var builder = new GovernedByteBuilder(governor, null, 256);
            builder.AppendAscii("value");
            Assert.True(MemoryMarshal.TryGetArray(builder.WrittenMemory, out var original));
            var capacityCharge = builder.CommittedCapacity;
            var text = builder.ToPyStringAndRelease();
            Assert.True(MemoryMarshal.TryGetArray(text.Utf8Bytes, out var transferred));
            Assert.Same(original.Array, transferred.Array);
            Assert.Equal(capacityCharge + PyString.EstimateApproximateBytes(0), governor.CurrentCommittedBytes);
            Assert.Equal(governor.CurrentCommittedBytes, text.CommittedOwnedBytes);
            Assert.True(((IPyOwnershipSnapshot)text).TrySnapshotOwnership(out var retained));
            Assert.Equal(governor.CurrentCommittedBytes, retained);
            Assert.Equal(0, builder.CommittedCapacity);
            Assert.Equal(0, builder.Length);
            builder.AppendAscii("new backing");
            builder.Release();
            Assert.Equal(retained, budget.ReservedBytes);
            Assert.Equal("value", text.AsString());
            governor.RefundUnpublishedValue(text);
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedMetadataAdmissionLeavesSourceFundedAndUnchanged()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var scope = budget.CreateScope())
        {
            var governor = new MemoryGovernor(scope);
            using var builder = new GovernedByteBuilder(governor, null, 256);
            builder.AppendString("café😀");
            var before = builder.WrittenMemory.ToArray();
            var capacity = builder.CommittedCapacity;
            Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out var blocker));
            using (blocker)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() => builder.ToPyStringAndRelease());
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(failure, governor.AdmissionFailure);
                Assert.Equal(before, builder.WrittenMemory.ToArray());
                Assert.Equal(capacity, builder.CommittedCapacity);
                Assert.Equal(capacity, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                builder.Release();
                Assert.Equal(0, governor.CurrentAccountedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void NonconsumingAndBorrowedPrefixConversionsRemainIndependentCopies()
    {
        using var builder = new GovernedByteBuilder(128);
        builder.AppendString("café😀");
        var copy = builder.ToPyString();
        var prefix = PyString.FromUtf8(builder.WrittenMemory);
        builder.Clear();
        builder.AppendAscii("overwrite the old content");
        Assert.Equal("café😀", copy.AsString());
        Assert.Equal(copy, prefix);
        var final = builder.ToPyStringAndRelease();
        builder.AppendAscii("separate");
        Assert.Equal("overwrite the old content", final.AsString());
    }

    [Fact]
    public void EmptyConversionRefundsUnusedPrivateCapacity()
    {
        var budget = new LythonMemoryBudget(4096);
        using var scope = budget.CreateScope();
        var governor = new MemoryGovernor(scope);
        using var builder = new GovernedByteBuilder(governor, null, 256);
        Assert.Same(PyString.Empty, builder.ToPyStringAndRelease());
        Assert.Equal(0, builder.Length);
        Assert.Equal(0, builder.CommittedCapacity);
        Assert.Equal(0, budget.ReservedBytes);
    }
}
