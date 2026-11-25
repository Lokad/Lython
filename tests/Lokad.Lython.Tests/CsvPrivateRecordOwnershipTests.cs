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
    [InlineData("pull-error")]
    public async Task PrivateRowCapacityIsReturnedOnlyAfterPendingHeaderPull(string outcome)
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
            var heldDuringPull = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.True(heldDuringPull >= source.OwnedBytes + PyList.EstimateCapacityBytes(1));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes >= heldDuringPull);
            headers.PullRelease.SetResult();
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
                else Assert.Equal("header pull", error.Message);
            }
            Assert.Equal(heldDuringPull - PyList.EstimateCapacityBytes(1), context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            headers.PullRelease.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (LythonRuntimeException) { }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class GatedHeaders(MemoryGovernor governor, string outcome) : IPyAsyncIteratorValue, IPySizedValue
    {
        private bool _yielded;
        internal TaskCompletionSource PullEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PullRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Length => 1;

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public PyIterationResult TryMoveNext() => throw new InvalidOperationException("Expected an asynchronous pull.");

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            if (!_yielded)
            {
                _yielded = true;
                return PyIterationResult.Yield(PyString.FromString("a"));
            }
            PullEntered.TrySetResult();
            await PullRelease.Task;
            if (outcome == "refusal") governor.Reserve(65536, Span);
            if (outcome == "pull-error") throw new LythonRuntimeException("RuntimeError", "header pull", Span);
            return PyIterationResult.End;
        }
    }
}
