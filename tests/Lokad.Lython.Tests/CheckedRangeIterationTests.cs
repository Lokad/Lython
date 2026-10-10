using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CheckedRangeIterationTests
{
    private static readonly LythonSourceSpan Span = new(10, 4, 2, 3);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(63, 1)]
    [InlineData(64, 2)]
    [InlineData(65, 2)]
    [InlineData(127, 2)]
    [InlineData(128, 3)]
    [InlineData(129, 3)]
    public void ExhaustedPullKeepsExactCheckpointCadence(int length, long expectedChecks)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var context = Context();
            var start = BigInteger.One << 100;
            var range = new PyRange(start, start + direction * length, new BigInteger(direction));
            using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();
            Assert.Equal(0, context.Limits.ExecutionStepCount);

            for (var i = 0; i < length; i++)
            {
                Assert.True(iterator.MoveNext());
                Assert.Equal(start + direction * i, Assert.IsType<BigInteger>(iterator.Current));
                Assert.Equal(1 + i / 64, context.Limits.ExecutionStepCount);
            }

            Assert.False(iterator.MoveNext());
            Assert.Equal(expectedChecks, context.Limits.ExecutionStepCount);
            Assert.False(iterator.MoveNext());
            Assert.Equal(expectedChecks, context.Limits.ExecutionStepCount);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void FirstPullChecksBeforeBoundsOrOwnership(int length, bool fuelDenied)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = fuelDenied ? 1 : 100,
            MaxExecutionMemoryBytes = 128,
            CancellationToken = cancellation.Token
        });
        context.Limits.ExecutionStepCount = 1;
        cancellation.Cancel();
        var start = BigInteger.One << 10000;
        var allocationSpan = new LythonSourceSpan(20, 5, 3, 4);
        var range = new PyRange(start, start + length, BigInteger.One,
            context.MemoryGovernor, context.State.CallTemporaries, allocationSpan);
        using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();
        Assert.Equal(1, context.Limits.ExecutionStepCount);

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal(fuelDenied ? "maximum execution step count exceeded (1)" : "execution canceled", error.Message);
        Assert.Equal(2, context.Limits.ExecutionStepCount);
        Assert.Equal(0, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.False(iterator.MoveNext());
        Assert.Equal(2, context.Limits.ExecutionStepCount);
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
        Assert.Equal(1, context.Limits.ExecutionStepCount);
        cancellation.Cancel();

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(2, context.Limits.ExecutionStepCount);
        Assert.False(iterator.MoveNext());
        Assert.Equal(2, context.Limits.ExecutionStepCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void DisposingActiveIterationIsTerminal(int pulls)
    {
        var context = Context();
        var iterator = PyIteration.ToSequence(new PyRange(0, 130, 1), Span, context).GetEnumerator();
        for (var i = 0; i < pulls; i++) Assert.True(iterator.MoveNext());
        var checks = context.Limits.ExecutionStepCount;

        iterator.Dispose();
        iterator.Dispose();
        Assert.False(iterator.MoveNext());
        Assert.Equal(checks, context.Limits.ExecutionStepCount);
    }

    [Fact]
    public void HeapYieldDenialUsesRangeAllocationSpan()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 100,
            MaxExecutionMemoryBytes = 128
        });
        var start = BigInteger.One << 10000;
        var allocationSpan = new LythonSourceSpan(20, 5, 3, 4);
        var range = new PyRange(start, start + 1, BigInteger.One,
            context.MemoryGovernor, context.State.CallTemporaries, allocationSpan);
        using var iterator = PyIteration.ToSequence(range, Span, context).GetEnumerator();

        var error = Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(allocationSpan, error.Span);
        Assert.Equal(1283, context.MemoryGovernor.LastDeniedReservationBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal(1, context.Limits.ExecutionStepCount);
        Assert.False(iterator.MoveNext());
    }

    private static LythonRuntime.ExecutionContext Context(CancellationToken cancellation = default)
        => new(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 100,
            CancellationToken = cancellation
        });
}
