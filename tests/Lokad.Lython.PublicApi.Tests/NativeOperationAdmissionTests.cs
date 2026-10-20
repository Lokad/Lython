using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeOperationAdmissionTests
{
    [Theory]
    [InlineData("bytes", false)] [InlineData("bytes", true)]
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
        using var script = new LythonEngine().Compile("return [b'ab'+b'cd', 'A'.lower(), 'a\\t'.expandtabs(4), 'a'.replace('a','z'), 'a'.translate({97:'😀'})]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        using var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(first.ReturnValue);
        Assert.Equal(new byte[] { 97, 98, 99, 100 }, values[0]);
        Assert.Equal(new object?[] { "a", "a   ", "z", "😀" }, values.Skip(1));
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
    [InlineData(false)] [InlineData(true)]
    public async Task TabExpansionStopsDuringGrowthAndRefundsStorage(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("return len(('x\\t'*32).expandtabs(50000))");
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
