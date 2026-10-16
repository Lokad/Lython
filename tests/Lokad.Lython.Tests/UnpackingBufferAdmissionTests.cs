using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class UnpackingBufferAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task BoundedLeaseStopsWithoutChangingUnboundedConsumers(bool asynchronous, int? maximumItems)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new CountingSequence(maximumItems is null ? 5 : 100000);
        var baseline = context.MemoryGovernor.CurrentReservedBytes;
        using (var lease = asynchronous
            ? await PyIteration.DrainLeasedAsync(source, Span, context, maximumItems)
            : PyIteration.DrainLeased(source, Span, context, maximumItems))
        {
            var expected = maximumItems ?? 5;
            Assert.Equal(expected, source.Yielded);
            Assert.Equal(expected, lease.Items.Count);
            Assert.All(lease.Items, value => Assert.Same(source.Value, value));
            Assert.Equal(1, source.Disposed);
            Assert.InRange(context.MemoryGovernor.CurrentReservedBytes - baseline, 1, 256);
        }
        Assert.Equal(baseline, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedBufferGrowthReleasesReservationAndDisposesEnumeration(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = limit });
        var source = new CountingSequence(5);
        using (context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes, Span))
        {
            var reserved = context.MemoryGovernor.CurrentReservedBytes;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                using var lease = asynchronous
                    ? await PyIteration.DrainLeasedAsync(source, Span, context, 3)
                    : PyIteration.DrainLeased(source, Span, context, 3);
            });
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1, source.Yielded);
            Assert.Equal(1, source.Disposed);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class CountingSequence(int length) : IEnumerable<object>, IAsyncEnumerable<object>
    {
        public object Value { get; } = new();
        public int Yielded { get; private set; }
        public int Disposed { get; private set; }
        public IEnumerator<object> GetEnumerator()
        {
            try
            {
                for (var i = 0; i < length; i++)
                {
                    Yielded++;
                    yield return Value;
                }
            }
            finally { Disposed++; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public async IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            try
            {
                for (var i = 0; i < length; i++)
                {
                    await Task.Yield();
                    Yielded++;
                    yield return Value;
                }
            }
            finally { Disposed++; }
        }
    }
}
