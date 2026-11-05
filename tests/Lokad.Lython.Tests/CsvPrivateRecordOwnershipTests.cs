using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvPrivateRecordOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, PyString.FromString("\""),
        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);

    [Fact]
    public void PublicRowAdoptsItsFundedArrayWithoutCopyOrNewAdmission()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var source = new LythonRuntime.CsvRecordSource(new PyList([PyString.FromString("kept,tail")]), Options(), Span, context);
        Assert.True(source.TryMoveNext(out var raw));
        var funded = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(budget.TryReserve(65536 - budget.ReservedBytes, out var pressure));
        using (pressure)
        {
            var row = source.AdoptRecord(raw);
            Assert.Equal(funded, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Same(raw[0], row[0]);
            raw[1] = raw[0]; // White-box witness: adoption retains the exact backing.
            Assert.Same(row[0], row[1]);
            var kept = (PyString)row[0];
            row.Clear();
            Assert.Equal(funded - PyList.EstimateCapacityBytes(2) + PyList.EstimateCapacityBytes(0),
                context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal("kept", kept.AsString());
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("refusal")]
    [InlineData("cleanup-error")]
    public async Task PrivateRowCapacityIsReturnedOnlyAfterActualHeaderCleanup(string outcome)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var headers = new GatedHeaders(context.MemoryGovernor, outcome);
        var source = new LythonRuntime.CsvRecordSource(new PyList([PyString.FromString("kept")]), Options(), Span, context);
        var reader = new LythonRuntime.CsvDictReaderObject(source, headers, PyNone.Instance, PyNone.Instance, context, Span);
        var pending = reader.TryMoveNextAsync().AsTask();
        try
        {
            await headers.PullEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            headers.PullRelease.SetResult();
            await headers.CloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var heldThroughCleanup = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.True(heldThroughCleanup >= source.OwnedBytes + PyList.EstimateCapacityBytes(1));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes >= heldThroughCleanup);
            headers.CloseRelease.SetResult();
            if (outcome == "success")
            {
                var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(result.HasValue);
                var record = Assert.IsType<PyDict>(result.Value);
                Assert.True(record.TryGetValue(PyString.FromString("a"), out var kept));
                Assert.Equal("kept", Assert.IsType<PyString>(kept).AsString());
            }
            else
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
                if (outcome == "refusal") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                else Assert.Equal("header cleanup", error.Message);
            }
            Assert.Equal(heldThroughCleanup - PyList.EstimateCapacityBytes(1), context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            headers.PullRelease.TrySetResult();
            headers.CloseRelease.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (LythonRuntimeException) { }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class GatedHeaders(MemoryGovernor governor, string outcome) : IPyAsyncIterableValue, IPySizedValue
    {
        private MemoryGovernor Governor { get; } = governor;
        private string Outcome { get; } = outcome;
        internal TaskCompletionSource PullEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PullRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CloseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CloseRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Length => 1;
        public IEnumerable<object> Iterate() => [PyString.FromString("a")];
        public IAsyncEnumerable<object> IterateAsync() => new Keys(this);

        private sealed class Keys(GatedHeaders headers) : IAsyncEnumerable<object>, IAsyncEnumerator<object>
        {
            private bool _yielded;
            public object Current => PyString.FromString("a");
            public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
            public async ValueTask<bool> MoveNextAsync()
            {
                if (_yielded) return false;
                _yielded = true;
                headers.PullEntered.SetResult();
                await headers.PullRelease.Task;
                if (headers.Outcome == "refusal") headers.Governor.Reserve(65536, Span);
                return true;
            }
            public async ValueTask DisposeAsync()
            {
                headers.CloseEntered.SetResult();
                await headers.CloseRelease.Task;
                if (headers.Outcome == "cleanup-error") throw new LythonRuntimeException("RuntimeError", "header cleanup", Span);
            }
        }
    }
}
