using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ZipStatePreflightTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    private static object Pull(PyZipIterator zip)
    {
        Assert.True(zip.TryMoveNext(out var value));
        return value;
    }

    [Fact]
    public void FailedPartialInitializationRefundsItsCursorStorage()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var held = new PyList([BigInteger.One]);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var failure = Assert.Throws<PyNotIterableException>(() => new PyZipIterator([held, PyNone.Instance], false, Span, context));
        Assert.Equal("'NoneType' object is not iterable", failure.Message);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(BigInteger.One, held[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedRowPreflightDoesNotConsumeAndStopsFurtherPulls(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new CountingSource();
        var zip = new PyZipIterator([source], false, Span, context);
        // Reserve all remaining allowance: even row allocation must fail first.
        var funding = context.MemoryGovernor.MaxAccountedBytes!.Value
            - context.MemoryGovernor.CurrentCommittedBytes - context.MemoryGovernor.CurrentReservedBytes;
        context.MemoryGovernor.Reserve(funding, Span);
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
                : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(0, source.Pulls);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        }
        finally { context.MemoryGovernor.ReleaseReserved(funding); }
        var denied = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
            : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, denied);
        Assert.Equal(0, source.Pulls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedTupleStorageReturnsFreshRowAdmissionAndPreservesHeldRows(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new CountingSource();
        var zip = new PyZipIterator([source], false, Span, context);
        var held = Assert.IsType<PyTuple>(asynchronous ? (await zip.TryMoveNextAsync()).Value : Pull(zip));
        var rowBytes = PyTuple.EstimateStorageBytes([BigInteger.One]);
        // Admit the working row buffer, leaving one byte too little for the
        // tuple's actual storage. The held row must remain funded and usable.
        var funding = context.MemoryGovernor.MaxAccountedBytes!.Value
            - context.MemoryGovernor.CurrentCommittedBytes - (32 + 80) - rowBytes + 1;
        context.MemoryGovernor.Reserve(funding, Span);
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
                : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(2, source.Pulls);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(BigInteger.One, held[0]);
        }
        finally { context.MemoryGovernor.ReleaseReserved(funding); }
        var denied = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
            : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, denied);
        Assert.Equal(2, source.Pulls);
        Assert.Equal(BigInteger.One, held[0]);
    }

    private sealed class CountingSource : IPyAsyncIteratorValue
    {
        internal int Pulls;
        public bool TryMoveNext(out object value) { Pulls++; value = BigInteger.One; return true; }
        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            await Task.Yield();
            Pulls++;
            return PyIterationResult.Yield(BigInteger.One);
        }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public void ConstructorFundingCoversMeasuredCursorStorage(int count)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new PyList([BigInteger.One]);
        var sources = Enumerable.Repeat<object>(source, count).ToArray();
        _ = new PyZipIterator(sources, false, Span, context);
        var initial = context.MemoryGovernor.CurrentCommittedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var iterator = new PyZipIterator(sources, false, Span, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var admitted = context.MemoryGovernor.CurrentCommittedBytes - initial;
        Assert.True(admitted >= allocated, $"admitted {admitted}, allocated {allocated}");
        GC.KeepAlive(iterator);
    }

}
