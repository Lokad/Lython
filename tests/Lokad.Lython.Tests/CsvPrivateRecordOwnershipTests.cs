using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvPrivateRecordOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, PyString.FromString("\""),
        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBlankRowRefundKeepsOtherStorageFunded(bool asynchronous)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var governor = context.MemoryGovernor;
            var headers = new PyList([PyString.FromString("name")], governor, Span);
            var source = new LythonRuntime.CsvRecordSource(new PyList([PyString.FromString("\n")]), Options(), Span, context);
            var reader = new LythonRuntime.CsvDictReaderObject(source, headers, PyNone.Instance, PyNone.Instance, context, Span);
            long fundedAtFailure = -1;
            hook.AfterShrink = refund =>
            {
                Assert.Equal(PyList.EstimateCapacityBytes(0), refund);
                hook.AfterShrink = null;
                var refusal = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(limit + 1, Span));
                fundedAtFailure = budget.ReservedBytes;
                throw refusal;
            };
            try
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await reader.TryMoveNextAsync())
                    : Assert.Throws<LythonRuntimeException>(() => reader.TryMoveNext());
                Assert.Same(governor.AdmissionFailure, error);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.True(fundedAtFailure > 0);
                Assert.Equal(fundedAtFailure, budget.ReservedBytes);
                Assert.Equal(fundedAtFailure, governor.CurrentAccountedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Equal("name", Assert.IsType<PyString>(Assert.Single(headers)).AsString());
            }
            finally { hook.AfterShrink = null; context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

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

    [Fact]
    public async Task DeferredSourceFailureDiscardsPartialRecordAndAllowsNextPull()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var lines = new DeferredRecordLines();
        var source = new LythonRuntime.CsvRecordSource(lines, Options(), Span, context);
        var reader = new LythonRuntime.CsvReaderObject(source);
        var pending = reader.TryMoveNextAsync().AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            lines.First.SetResult(PyIterationResult.Yield(PyString.FromString("\"unfinished\n")));
            await lines.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, reader.LineNum);
            Assert.False(pending.IsCompleted);
            lines.Second.SetException(new InvalidOperationException("source mutated"));
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("RuntimeError", error.ExceptionType);
            var row = Assert.IsType<PyList>((await reader.TryMoveNextAsync()).Value);
            Assert.Single(row);
            Assert.Equal("kept", Assert.IsType<PyString>(row[0]).AsString());
            Assert.Equal(2, reader.LineNum);
            Assert.False((await reader.TryMoveNextAsync()).HasValue);
            Assert.Equal("kept", Assert.IsType<PyString>(row[0]).AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            lines.First.TrySetResult(PyIterationResult.End);
            lines.Second.TrySetResult(PyIterationResult.End);
            try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (LythonRuntimeException) { }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class DeferredRecordLines : IPyAsyncIteratorValue
    {
        private int _position;
        internal TaskCompletionSource<PyIterationResult> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<PyIterationResult> Second { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public PyIterationResult TryMoveNext() => throw new InvalidOperationException("Expected an asynchronous pull.");
        public ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            switch (_position++)
            {
                case 0: return new(First.Task);
                case 1:
                    SecondEntered.SetResult();
                    return new(Second.Task);
                case 2: return new(PyIterationResult.Yield(PyString.FromString("kept\n")));
                default: return new(PyIterationResult.End);
            }
        }
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
