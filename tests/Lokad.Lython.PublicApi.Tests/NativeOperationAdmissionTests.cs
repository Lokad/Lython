using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeOperationAdmissionTests
{
    [Theory]
    [InlineData("random-bytes", false)] [InlineData("random-bytes", true)]
    [InlineData("random-bits", false)] [InlineData("random-bits", true)]
    [InlineData("integer-to-bytes", false)] [InlineData("integer-to-bytes", true)]
    [InlineData("decimal-digits", false)] [InlineData("decimal-digits", true)]
    [InlineData("decimal-text", false)] [InlineData("decimal-text", true)]
    [InlineData("decimal-context-text", false)] [InlineData("decimal-context-text", true)]
    [InlineData("byte-sized", false)] [InlineData("byte-sized", true)]
    [InlineData("byte-iterable", false)] [InlineData("byte-iterable", true)]
    [InlineData("integer-byte-iterable", false)] [InlineData("integer-byte-iterable", true)]
    [InlineData("byte-fromhex", false)] [InlineData("byte-fromhex", true)]
    [InlineData("byte-rsplit", false)] [InlineData("byte-rsplit", true)]
    [InlineData("date-strftime", false)] [InlineData("date-strftime", true)]
    [InlineData("date-format", false)] [InlineData("date-format", true)]
    [InlineData("time-strftime", false)] [InlineData("time-strftime", true)]
    [InlineData("date-strptime", false)] [InlineData("date-strptime", true)]
    [InlineData("bytes", false)] [InlineData("bytes", true)]
    [InlineData("byte-slice", false)] [InlineData("byte-slice", true)]
    [InlineData("byte-stepped-slice", false)] [InlineData("byte-stepped-slice", true)]
    [InlineData("byte-repr", false)] [InlineData("byte-repr", true)]
    [InlineData("byte-join", false)] [InlineData("byte-join", true)]
    [InlineData("byte-join-input", false)] [InlineData("byte-join-input", true)]
    [InlineData("byte-hex", false)] [InlineData("byte-hex", true)]
    [InlineData("byte-translate", false)] [InlineData("byte-translate", true)]
    [InlineData("byte-expandtabs", false)] [InlineData("byte-expandtabs", true)]
    [InlineData("byte-decode", false)] [InlineData("byte-decode", true)]
    [InlineData("byte-replace", false)] [InlineData("byte-replace", true)]
    [InlineData("byte-pad", false)] [InlineData("byte-pad", true)]
    [InlineData("byte-zfill", false)] [InlineData("byte-zfill", true)]
    [InlineData("byte-lower", false)] [InlineData("byte-lower", true)]
    [InlineData("json-integer-indent", false)] [InlineData("json-integer-indent", true)]
    [InlineData("json-string-indent", false)] [InlineData("json-string-indent", true)]
    [InlineData("lower", false)] [InlineData("lower", true)]
    [InlineData("upper", false)] [InlineData("upper", true)]
    [InlineData("capitalize", false)] [InlineData("capitalize", true)]
    [InlineData("title", false)] [InlineData("title", true)]
    [InlineData("swapcase", false)] [InlineData("swapcase", true)]
    [InlineData("casefold", false)] [InlineData("casefold", true)]
    [InlineData("replace", false)] [InlineData("replace", true)]
    [InlineData("expandtabs", false)] [InlineData("expandtabs", true)]
    [InlineData("translate", false)] [InlineData("translate", true)]
    public async Task RefusedNativeConstructionReturnsAllowance(string operation, bool asynchronous)
    {
        var source = NativeOperationSources.RefusedConstruction(operation);
        var compilation = new LythonMemoryBudget();
        using var script = new LythonEngine().Compile(source, compilation);
        Assert.True(script.IsValid);
        var baseline = compilation.ReservedBytes;
        var budget = new LythonMemoryBudget(32768);
        var options = new LythonRunOptions { MemoryBudget = budget };
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(baseline, compilation.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, compilation.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeResultsRemainFundedUntilTheirLastConsumerEnds(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("return [b'ab'+b'cd', 'A'.lower(), 'a\\t'.expandtabs(4), 'a'.replace('a','z'), 'a'.translate({97:'😀'}), b'abc'[1:], repr(b'abc'), b'-'.join([b'a',b'b'])]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        using var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(first.ReturnValue);
        Assert.Equal(new byte[] { 97, 98, 99, 100 }, values[0]);
        Assert.Equal(new object?[] { "a", "a   ", "z", "😀" }, values.Skip(1).Take(4));
        Assert.Equal(new byte[] { 98, 99 }, values[5]);
        Assert.Equal("b'abc'", values[6]);
        Assert.Equal(new byte[] { 97, 45, 98 }, values[7]);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TranslationTablesUseRuneAndByteIndicesWithoutFullCopies(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("return ['a'.translate('😀'*98), len(('x'*64).translate(bytes(16384)))]");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(65536);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new object?[] { "😀", new BigInteger(64) }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "str")] [InlineData(true, "str")]
    [InlineData(false, "bytes")] [InlineData(true, "bytes")]
    [InlineData(false, "date")] [InlineData(true, "date")]
    [InlineData(false, "integer")] [InlineData(true, "integer")]
    [InlineData(false, "decimal")] [InlineData(true, "decimal")]
    [InlineData(false, "random-bytes")] [InlineData(true, "random-bytes")]
    [InlineData(false, "random-bits")] [InlineData(true, "random-bits")]
    public async Task NativeTextGrowthObservesCancellationAndRefundsStorage(bool asynchronous, string operation)
    {
        using var script = new LythonEngine().Compile(operation switch
        {
            "random-bytes" => "import random\nreturn len(random.randbytes(200000))",
            "random-bits" => "import random\nreturn random.getrandbits(1600000).bit_length()",
            "integer" => "return len((-1).to_bytes(100000,signed=True))",
            "decimal" => "import decimal,itertools\nreturn len(decimal.DecimalTuple(0,itertools.repeat(1,100000),0).digits)",
            "bytes" => "return len(bytes([9,46]).expandtabs(50000))",
            "date" => "import datetime\nreturn len(datetime.date(2024,1,2).strftime('" + new string('x', 100000) + "'))",
            _ => "return len(('x\\t'*32).expandtabs(50000))"
        });
        using var cancellation = new CancellationTokenSource();
        var budget = new CancelOnGrowth(cancellation);
        var options = new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.True(budget.BytesAtCancellation >= 32768);
            Assert.True(budget.LargestAdmissionAfterCancellation <= budget.BytesAtCancellation,
                $"Expansion continued growing after cancellation: {budget.LargestAdmissionAfterCancellation} bytes.");
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    private sealed class CancelOnGrowth(CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        internal readonly LythonMemoryBudget Root = new(16 * 1024 * 1024);
        internal long BytesAtCancellation;
        internal long LargestAdmissionAfterCancellation;
        private void OnResize(long bytes)
        {
            if (cancellation.IsCancellationRequested)
            {
                LargestAdmissionAfterCancellation = Math.Max(LargestAdmissionAfterCancellation, bytes);
                return;
            }
            if (bytes < 32768) return;
            BytesAtCancellation = bytes;
            cancellation.Cancel();
        }
        public ILythonMemoryScope CreateScope() => new Owner(this, Root.CreateScope());
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => Wrap(Root, bytes, out reservation);
        private bool Wrap(ILythonMemoryBudget inner, long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var value)) { reservation = null; return false; }
            reservation = new Admission(this, value);
            return true;
        }
        private sealed class Owner(CancelOnGrowth outer, ILythonMemoryScope inner) : ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope() => new Owner(outer, inner.CreateScope());
            public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => outer.Wrap(inner, bytes, out reservation);
            public void Dispose() => inner.Dispose();
        }
        private sealed class Admission(CancelOnGrowth outer, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                if (!inner.TryResize(bytes)) return false;
                outer.OnResize(bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
