using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CursorDispatchTests
{
    private static readonly LythonSourceSpan Span = new(10, 4, 2, 3);

    [Theory]
    [InlineData(0, false)]
    [InlineData(63, false)]
    [InlineData(64, false)]
    [InlineData(65, false)]
    [InlineData(0, true)]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, true)]
    public async Task ExactBuiltinPullsPreserveCheckpointCadenceIncludingExhaustion(int length, bool asynchronous)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { });
            await using var cursor = PyIteration.Cursor.Create(new PyRange(10, 10 + direction * length, direction), Span, context);
            var checksum = BigInteger.Zero;
            for (var i = 0; i <= length; i++)
            {
                var next = await Pull(cursor, asynchronous);
                Assert.Equal(i < length, next.HasValue);
                if (next.HasValue)
                {
                    var value = Assert.IsType<BigInteger>(next.Value);
                    Assert.Equal(new BigInteger(10 + direction * i), value);
                    checksum += value;
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsActivePullsBeforeAdvancing(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { CancellationToken = cancellation.Token});
        var source = new SyncIteratorAndAsyncIterable();
        await using var cursor = PyIteration.Cursor.Create(source, Span, context);
        for (var i = 0; i < 64; i++) Assert.True((await Pull(cursor, asynchronous)).HasValue);
        Assert.Equal(64, source.SyncMoves);
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await Pull(cursor, asynchronous); });
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(64, source.SyncMoves);
        Assert.Equal(0, source.AsyncEnumerations);
    }

    [Fact]
    public async Task SynchronousIteratorPrecedesAsynchronousIterable()
    {
        var source = new SyncIteratorAndAsyncIterable();
        await using var cursor = PyIteration.Cursor.Create(source, Span);
        Assert.Equal(BigInteger.One, Assert.IsType<BigInteger>((await cursor.TryMoveNextAsync()).Value));
        Assert.Equal(1, source.SyncMoves);
        Assert.Equal(0, source.AsyncEnumerations);
    }

    [Fact]
    public async Task AsynchronousIteratorPrecedesSynchronousIteratorAndCanSuspend()
    {
        var source = new AsyncIterator();
        await using var cursor = PyIteration.Cursor.Create(source, Span);
        var pending = cursor.TryMoveNextAsync();
        var suspended = !pending.IsCompleted;
        source.Release.TrySetResult(PyIterationResult.Yield(new BigInteger(2)));
        var next = await pending;
        Assert.True(suspended);
        Assert.Equal(new BigInteger(2), Assert.IsType<BigInteger>(next.Value));
        Assert.Equal(1, source.AsyncMoves);
        Assert.Equal(0, source.SyncMoves);
        Assert.Equal(0, source.AsyncEnumerations);
    }

    [Fact]
    public async Task AcquiredAsyncEnumeratorPrecedesIteratorAndIsDisposedOnce()
    {
        var source = new AsyncIterator();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var outer = new PyIteration.GeneratorOuter(source, source.Iterate());
        await using var cursor = PyIteration.Cursor.FromGeneratorOuter(outer, source, true, Span, context);
        var next = await cursor.TryMoveNextAsync();
        Assert.Equal(new BigInteger(3), Assert.IsType<BigInteger>(next.Value));
        Assert.Equal(1, source.AsyncEnumerations);
        Assert.Equal(0, source.AsyncMoves);
        Assert.Equal(0, source.SyncMoves);
        await cursor.DisposeAsync();
        await cursor.DisposeAsync();
        Assert.Equal(1, source.AsyncDisposals);
    }

    private static ValueTask<PyIterationResult> Pull(PyIteration.Cursor cursor, bool asynchronous)
        => asynchronous ? cursor.TryMoveNextAsync()
            : ValueTask.FromResult(cursor.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End);

    private class SyncIteratorAndAsyncIterable : IPyIteratorValue, IPyAsyncIterableValue
    {
        internal int SyncMoves;
        internal int AsyncEnumerations;
        internal int AsyncDisposals;

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            SyncMoves++;
            value = BigInteger.One;
            return true;
        }

        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value)) yield return value;
        }

        public async IAsyncEnumerable<object> IterateAsync()
        {
            AsyncEnumerations++;
            try { await Task.CompletedTask; yield return new BigInteger(3); }
            finally { AsyncDisposals++; }
        }
    }

    private sealed class AsyncIterator : SyncIteratorAndAsyncIterable, IPyAsyncIteratorValue
    {
        internal readonly TaskCompletionSource<PyIterationResult> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int AsyncMoves;
        public ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            AsyncMoves++;
            return new(Release.Task);
        }
    }
}
