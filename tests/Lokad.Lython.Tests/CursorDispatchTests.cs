using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CursorDispatchTests
{
    private static readonly LythonSourceSpan Span = new(10, 4, 2, 3);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactSequencesKeepAliasesAcrossPullModesAndExhaustion(bool asynchronous)
    {
        foreach (var tuple in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
                var first = new PyList([BigInteger.One], context.MemoryGovernor, Span);
                var last = new PyList([new BigInteger(2)], context.MemoryGovernor, Span);
                object source = tuple
                    ? new PyTuple([first, last], context.MemoryGovernor, Span, context)
                    : new PyList([first, last], context.MemoryGovernor, Span);
                var baseline = budget.ReservedBytes;
                try
                {
                    await using (var cursor = PyIteration.Cursor.Create(source, Span, context))
                    {
                        Assert.Same(first, (await Pull(cursor, asynchronous)).Value);
                        Assert.Same(last, (await Pull(cursor, asynchronous)).Value);
                        Assert.False((await Pull(cursor, asynchronous)).HasValue);
                        Assert.False((await Pull(cursor, asynchronous)).HasValue);
                        await cursor.DisposeAsync();
                        Assert.False((await Pull(cursor, asynchronous)).HasValue);
                    }
                    Assert.Equal(baseline, budget.ReservedBytes);
                    Assert.Equal(BigInteger.One, Assert.Single(first));
                    Assert.Equal(new BigInteger(2), Assert.Single(last));
                }
                finally { context.State.CloseResources(); }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexedCursorSeesLiveMutationButCannotReviveAfterEof(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var source = new PyList([BigInteger.One, new BigInteger(2), new BigInteger(3)], context.MemoryGovernor, Span);
            try
            {
                await using var cursor = PyIteration.Cursor.Create(source, Span, context);
                Assert.Equal(BigInteger.One, (await Pull(cursor, asynchronous)).Value);
                source.SetItem(1, new BigInteger(9));
                for (var i = 4; i < 20; i++) source.Add(new BigInteger(i));
                Assert.Equal(new BigInteger(9), (await Pull(cursor, asynchronous)).Value);
                source.RemoveAt(0);
                Assert.Equal(new BigInteger(4), (await Pull(cursor, asynchronous)).Value);
                source.Clear();
                Assert.False((await Pull(cursor, asynchronous)).HasValue);
                source.Add(new BigInteger(99));
                Assert.False((await Pull(cursor, asynchronous)).HasValue);
                await cursor.DisposeAsync();
                Assert.Equal(new BigInteger(99), Assert.Single(source));
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CheckedSequenceEnumeratorPreservesCurrentAndDisposeBehavior()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var source = new PyList([BigInteger.One, new BigInteger(2)], context.MemoryGovernor, Span);
            try
            {
                using var cursor = PyIteration.ToSequence(source, Span, context).GetEnumerator();
                Assert.True(cursor.MoveNext());
                Assert.Equal(BigInteger.One, cursor.Current);
                source.SetItem(1, new BigInteger(9));
                Assert.True(cursor.MoveNext());
                Assert.Equal(new BigInteger(9), cursor.Current);
                Assert.False(cursor.MoveNext());
                Assert.Same(PyNone.Instance, cursor.Current);
                source.Add(new BigInteger(10));
                Assert.False(cursor.MoveNext());
                cursor.Dispose();
                Assert.False(cursor.MoveNext());
                Assert.Equal(3, source.Count);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexedSequencePullsKeepCooperativeCancellation(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var source = new PyList(Enumerable.Range(0, 256).Select(i => (object)new BigInteger(i)), context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            try
            {
                await using (var cursor = PyIteration.Cursor.Create(source, Span, context))
                {
                    Assert.True((await Pull(cursor, asynchronous)).HasValue);
                    cancellation.Cancel();
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    {
                        while ((await Pull(cursor, asynchronous)).HasValue) { }
                    });
                    Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    Assert.Same(Span, failure.Span);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(256, source.Count);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(63, false)]
    [InlineData(64, false)]
    [InlineData(65, false)]
    [InlineData(0, true)]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, true)]
    public async Task BuiltinPullsPreserveValuesAndExhaustion(int length, bool asynchronous)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { },regexProvider:Utf8RegexProvider.Instance);
            await using var cursor = PyIteration.Cursor.Create(new PyRange(10, 10 + direction * length, direction), Span, context);
            for (var i = 0; i <= length; i++)
            {
                var next = await Pull(cursor, asynchronous);
                Assert.Equal(i < length, next.HasValue);
                if (next.HasValue)
                {
                    var value = Assert.IsType<BigInteger>(next.Value);
                    Assert.Equal(new BigInteger(10 + direction * i), value);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsActivePullsWithoutSelectingAsyncIterable(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { CancellationToken = cancellation.Token},regexProvider:Utf8RegexProvider.Instance);
        var source = new SyncIteratorAndAsyncIterable();
        await using var cursor = PyIteration.Cursor.Create(source, Span, context);
        for (var i = 0; i < 3; i++) Assert.True((await Pull(cursor, asynchronous)).HasValue);
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            for (var i = 0; i < 2000; i++) await Pull(cursor, asynchronous);
        });
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
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
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
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
