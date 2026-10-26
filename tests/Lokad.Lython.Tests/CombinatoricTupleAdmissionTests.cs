using System.Collections;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CombinatoricTupleAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in new[] { "product", "combinations", "replacement", "permutations", "zip" })
            foreach (var asynchronous in new[] { false, true }) yield return [name, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ResultBackingIsAdmittedBeforeAllocation(string name, bool asynchronous)
    {
        const int width = 8192;
        const int limit = 2 * 1024 * 1024;
        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(limit);
        using (var inputOwner = inputs.CreateScope())
        using (var owner = operation.CreateScope())
        {
            var inputGovernor = new MemoryGovernor(inputOwner);
            var single = new PyTuple([BigInteger.One], inputGovernor, Span);
            var values = new PyTuple(Enumerable.Repeat<object>(BigInteger.One, width), inputGovernor, Span);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            // The caller owns the preexisting layer table, independently of output.
            Assert.True(inputOwner.TryReserve(64L + 16L * width, out var table));
            using (table)
            {
                var pools = Enumerable.Repeat<IReadOnlyList<object>>(single, width).ToArray();
                var sources = Enumerable.Repeat<object>(single, width).ToArray();
                var governor = context.MemoryGovernor;
                PyIteratorBase iterator = name switch
                {
                    "product" => new PyProductIterator(pools, governor, Span, context),
                    "combinations" => new PyCombinationsIterator(values, width, governor, Span, context),
                    "replacement" => new PyCombinationsWithReplacementIterator(values, width, governor, Span, context),
                    "permutations" => new PyPermutationsIterator(values, width, governor, Span, context),
                    _ => await PyZipLongestIterator.CreateAsync(sources, PyNone.Instance, Span, governor, context, false)
                };
                Assert.True(owner.TryReserve(limit - operation.ReservedBytes - 64, out var pin));
                using (pin)
                {
                    var baseline = operation.ReservedBytes;
                    var inputBaseline = inputs.ReservedBytes;
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var error = Assert.Throws<LythonRuntimeException>(() =>
                    {
                        if (!asynchronous) iterator.TryMoveNext(out _);
                        else
                        {
                            var pending = iterator.TryMoveNextAsync();
                            Assert.True(pending.IsCompleted);
                            pending.GetAwaiter().GetResult();
                        }
                    });
#if !DEBUG
                    Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 32768);
#endif
                    Assert.Equal("MemoryError", error.ExceptionType);
                    Assert.Equal(baseline, operation.ReservedBytes);
                    Assert.Equal(inputBaseline, inputs.ReservedBytes);
                    Assert.Equal(0, governor.CurrentReservedBytes);
                }
            }
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }

    [Fact]
    public void OwnedTupleFillObservesCancellationBetweenSourceReads()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var source = new CancelOnRead(cancellation);
            var iterator = new PyCombinationsIterator(source, source.Count, context.MemoryGovernor, Span, context);
            var error = Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext(out _));
            Assert.Equal("execution canceled", error.Message);
            Assert.InRange(source.Reads, 1, source.Count - 1);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZipOutputReservationSurvivesInputSuspensionAndUnwinds(bool cancel)
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var source = new SuspendedSource(cancellation.Token);
            var iterator = await PyZipLongestIterator.CreateAsync([source], PyNone.Instance, Span, context.MemoryGovernor, context, false);
            var pending = iterator.TryMoveNextAsync().AsTask();
            try
            {
                await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(context.MemoryGovernor.CurrentReservedBytes >= PyTuple.EstimateApproximateBytes(1));
                if (cancel) cancellation.Cancel();
                else source.Resume.TrySetResult();
                if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                else
                {
                    var row = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.True(row.HasValue);
                    Assert.Equal(BigInteger.One, Assert.Single(Assert.IsType<PyTuple>(row.Value)));
                }
            }
            finally
            {
                cancellation.Cancel();
                source.Resume.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class CancelOnRead(CancellationTokenSource cancellation) : IReadOnlyList<object>
    {
        public int Count => 1024;
        public int Reads { get; private set; }
        public object this[int index] { get { Reads++; cancellation.Cancel(); return BigInteger.One; } }
        public IEnumerator<object> GetEnumerator() => throw new InvalidOperationException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SuspendedSource(CancellationToken cancellation) : IPyIterableValue, IPyAsyncIterableValue
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<object> Iterate() => [BigInteger.One];
        public async IAsyncEnumerable<object> IterateAsync()
        {
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(cancellation);
            yield return BigInteger.One;
        }
    }
}
