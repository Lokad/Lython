using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IterationCancellationTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ContextAwareIterationStopsAndDisposesAfterMidStreamCancellation(bool asynchronous, bool directCursor)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
        });
        var source = new CancellingIterable(cancellation);

        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (directCursor)
            {
                await using var cursor = PyIteration.Cursor.Create(source, Span, context);
                if (asynchronous)
                {
                    while ((await cursor.TryMoveNextAsync()).HasValue) { }
                }
                else
                {
                    while (cursor.TryMoveNext(out _)) { }
                }
            }
            else if (asynchronous)
            {
                await foreach (var _ in PyIteration.ToSequenceAsync(source, Span, context)) { }
            }
            else
            {
                foreach (var _ in PyIteration.ToSequence(source, Span, context)) { }
            }
        });

        Assert.Equal("execution canceled", failure.Message);
        // Cancellation starts on pull 90. A shared checkpoint must stop the
        // next bounded chunk, rather than draining the remaining 1,000 items.
        Assert.InRange(source.Produced, 90, 154);
        Assert.True(source.Disposed);
    }

    private sealed class CancellingIterable(CancellationTokenSource cancellation) : IPyAsyncIterableValue
    {
        public int Produced { get; private set; }
        public bool Disposed { get; private set; }

        public IEnumerable<object> Iterate()
        {
            try
            {
                for (var i = 0; i < 1000; i++)
                {
                    if (++Produced == 90) cancellation.Cancel();
                    yield return new BigInteger(i);
                }
            }
            finally
            {
                Disposed = true;
            }
        }

        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.CompletedTask;
            foreach (var value in Iterate()) yield return value;
        }
    }
}
