using System.Runtime.InteropServices;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringWindowOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(-1, "a😀bé")]
    [InlineData(0, "a😀bé")]
    [InlineData(1, "A😀BÉ")]
    public void SplitWordsKeepTheirOwnUtf8AndScalarBoundaries(int casing, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var source = PyString.FromString("HEAD a😀bé TAIL");
            var words = casing < 0 ? PyStringOps.SplitWhitespace(source, governor, Span, context)
                : PyStringOps.SplitCasedWhitespace(source, casing == 1, governor, Span, context);
            var word = Assert.IsType<PyString>(words[1]);
            var independent = PyString.FromString(expected);
            Assert.Equal(Encoding.UTF8.GetBytes(expected), word.Utf8Bytes.ToArray());
            Assert.Equal(expected, word.AsString());
            Assert.Equal(4, word.Length);
            Assert.Equal(5, word.GetUtf16CodeUnitCount());
            Assert.Equal(expected[..1], word.Index(0).AsString());
            Assert.Equal("😀", word.Index(1).AsString());
            Assert.Equal(expected[^1..], word.AsString(3));
            Assert.Equal(5, word.GetByteIndexForRuneBoundary(2));
            Assert.Equal(2, word.ByteIndexToRuneIndex(5));
            Assert.Equal(independent.GetHashCode(), word.GetHashCode());
            Assert.True(word.Equals(independent));
            Assert.Equal(0, PyString.CompareOrdinal(word, independent));
            Assert.Equal(independent.EnumerateRunes().Select(s => s.AsString()), word.EnumerateRunes().Select(s => s.AsString()));
            Assert.Equal("😀" + expected[^1..], word.Slice([1, 3]).AsString());
            Assert.Equal(expected[^1..] + expected[^2..^1] + "😀" + expected[..1],
                word.Slice(new PyIndexing.SliceBounds(3, -1, -1)).AsString());
            Assert.Equal(expected + expected, word.Concat(word).AsString());
            Assert.Equal(expected + expected, word.Repeat(2).AsString());
            Assert.Equal("|" + string.Join('|', independent.EnumerateRunes().Select(s => s.AsString())) + "|",
                word.Replace(PyString.Empty, PyString.FromString("|")).AsString());
            Assert.Equal(expected[..1] + "+" + expected[^2..], word.Replace(PyString.FromString("😀"), PyString.FromString("+")).AsString());
            var utf16 = new char[5];
            Assert.Equal(5, word.CopyUtf16To(utf16));
            Assert.Equal(expected, new string(utf16));
            Assert.Equal(1, word.Find(PyString.FromString("😀")));
            Assert.Equal(1, word.Count(PyString.FromString("😀")));
            Assert.False(word.Contains(PyString.FromString("TAIL")));
            Assert.True(governor.CurrentAccountedBytes > 0);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefundingPrivateWordPreservesOtherWordsBackingAndCacheFunding()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var source = PyString.FromString(new string('x', 65536) + " " + new string('α', 32768) + "😀");
            var words = PyStringOps.SplitWhitespace(source, governor, Span);
            var privateWord = (PyString)words[0];
            var retainedWord = (PyString)words[1];
            words.RemoveAt(0);
            governor.RefundUnpublishedValue(privateWord);

            Assert.True(MemoryMarshal.TryGetArray(retainedWord.Utf8Bytes, out var backing));
            Assert.True(budget.ReservedBytes >= backing.Array!.LongLength
                + PyString.EstimateApproximateBytes(0) + words.CommittedStorageBytes);
            var indexed = retainedWord.Index(17);
            Assert.Equal("α", indexed.AsString());
            Assert.Equal("😀", retainedWord.AsString(32768));
            Assert.Equal(32769, retainedWord.Length);
            Assert.Equal(65536, retainedWord.GetByteIndexForRuneBoundary(32768));
            var held = governor.CurrentCommittedBytes;
            governor.RefundUnpublishedValue(indexed);
            Assert.True(governor.CurrentCommittedBytes < held);
            var cacheBytes = 32L + 4L * ((long)retainedWord.Length + 1);
            Assert.True(budget.ReservedBytes >= backing.Array.LongLength
                + PyString.EstimateApproximateBytes(0) + cacheBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedTextFundsItsActualBackingAfterPrivateSourceRefund(bool nested)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var source = PyString.FromString("xx😀é\0β" + new string('z', 32768) + "yy", governor, Span);
            var middle = nested ? source.SliceByByteRange(2, source.Utf8Bytes.Length - 2) : null;
            var value = nested ? middle!.SliceByByteRange(0, 9) : source.SliceByByteRange(2, 11);
            governor.RefundUnpublishedValue(source);
            if (middle is not null) governor.RefundUnpublishedValue(middle);

            Assert.Equal("😀é\0β", value.AsString());
            Assert.True(MemoryMarshal.TryGetArray(value.Utf8Bytes, out var backing));
            // Works for copied payloads and shared windows: actual retained byte
            // storage must stay funded after its private producer has finished.
            Assert.True(budget.ReservedBytes >= backing.Array!.LongLength + PyString.EstimateApproximateBytes(0));
            Assert.Equal(4, value.Length);
            Assert.Equal(2, value.ByteIndexToRuneIndex(6));
            Assert.Equal(7, value.GetByteIndexForRuneBoundary(3));
            Assert.Equal("é\0", value.SliceByByteRange(4, 7).AsString());
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedSlicePreservesSourceFundingAndAllowsNativeRollback()
    {
        const long limit = 128 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var source = PyString.FromString(new string('x', 8192), governor, Span);
            var funded = governor.CurrentAccountedBytes;
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out var fill));
            using (fill)
            {
                LythonRuntimeException failure;
                using (PyStructuralGuard.PushAmbient(context, Span))
                    failure = Assert.Throws<LythonRuntimeException>(() => source.SliceByByteRange(1, 2));
                // Native observations and rollback follow the stopped operation.
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
                Assert.Equal(funded, governor.CurrentAccountedBytes);
                Assert.Equal(8192, source.Length);
                Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => source.SliceByByteRange(2, 3)));
                governor.RefundUnpublishedValue(source);
                Assert.Equal(0, governor.CurrentAccountedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UnknownPartialMemoryRemainsIndependentBeforeSlicing()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var raw = Encoding.UTF8.GetBytes("x😀é\0βy");
            var source = PyString.FromUtf8(raw.AsMemory(1, raw.Length - 2), governor, Span);
            var value = source.SliceByByteRange(0, 6);
            Array.Fill(raw, (byte)'!');
            governor.RefundUnpublishedValue(source);
            Assert.Equal("😀é", value.AsString());
            Assert.Equal(2, value.Length);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
