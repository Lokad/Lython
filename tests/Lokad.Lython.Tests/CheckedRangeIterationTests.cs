using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CheckedRangeIterationTests
{
    private static readonly LythonSourceSpan Span = new(10, 4, 2, 3);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public void ExhaustedPullKeepsExactCheckpointCadence(int length)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var context = Context();
            var start = BigInteger.One << 100;
            var range = new PyRange(start, start + direction * length, new BigInteger(direction));
            using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();

            for (var i = 0; i < length; i++)
            {
                Assert.True(iterator.MoveNext());
                Assert.Equal(start + direction * i, Assert.IsType<BigInteger>(iterator.Current));
            }

            Assert.False(iterator.MoveNext());
            Assert.False(iterator.MoveNext());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FirstPullChecksBeforeBoundsOrOwnership(int length)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(128),
            CancellationToken = cancellation.Token
        },regexProvider:Utf8RegexProvider.Instance);
        cancellation.Cancel();
        var start = BigInteger.One << 10000;
        var allocationSpan = new LythonSourceSpan(20, 5, 3, 4);
        var range = new PyRange(start,start + length,BigInteger.One,            context.MemoryGovernor,allocationSpan);
        using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal("execution canceled", error.Message);
        Assert.Equal(0, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.False(iterator.MoveNext());
    }

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(130)]
    public void CancellationPrecedesPull65IncludingExhaustion(int length)
    {
        using var cancellation = new CancellationTokenSource();
        var context = Context(cancellation.Token);
        using var iterator = PyIteration.ToSequence(new PyRange(0, length, 1), Span, context).GetEnumerator();
        for (var i = 0; i < 64; i++)
        {
            Assert.True(iterator.MoveNext());
            Assert.Equal(new BigInteger(i), iterator.Current);
        }
        cancellation.Cancel();

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
        Assert.False(iterator.MoveNext());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void DisposingActiveIterationIsTerminal(int pulls)
    {
        var context = Context();
        var iterator = PyIteration.ToSequence(new PyRange(0, 130, 1), Span, context).GetEnumerator();
        for (var i = 0; i < pulls; i++) Assert.True(iterator.MoveNext());

        iterator.Dispose();
        iterator.Dispose();
        Assert.False(iterator.MoveNext());
    }

    [Fact]
    public void HeapYieldDenialUsesRangeAllocationSpan()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(128)
        },regexProvider:Utf8RegexProvider.Instance);
        var start = BigInteger.One << 10000;
        var allocationSpan = new LythonSourceSpan(20, 5, 3, 4);
        var range = new PyRange(start,start + 1,BigInteger.One,            context.MemoryGovernor,allocationSpan);
        using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(allocationSpan, error.Span);
        Assert.Equal(1283, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.False(iterator.MoveNext());
    }

    private static LythonRuntime.ExecutionContext Context(CancellationToken cancellation = default)
        => new(new MockLythonHost(), new LythonRunOptions
        {
            CancellationToken = cancellation
        },regexProvider:Utf8RegexProvider.Instance);
}
