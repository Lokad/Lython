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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReentrantRawPullCannotDiscardTheActiveNativeRecord(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/csv", "10,20\n30,40\n50,60\n");
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = hook });
        var file = await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync("/csv", context,
            LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
        var source = new LythonRuntime.CsvRecordSource(file, Options(), Span, context);
        var reader = new LythonRuntime.CsvReaderObject(source);
        async ValueTask<PyList> Next()
            => Assert.IsType<PyList>((asynchronous ? await reader.TryMoveNextAsync() : reader.TryMoveNext()).Value);
        string[] Values(PyList row) => row.Cast<PyString>().Select(value => value.AsString()).ToArray();
        try
        {
            var first = await Next();
            var nestedRefused = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                var failure = Assert.Throws<LythonRuntimeException>(() => reader.TryMoveNext());
                Assert.Equal("reentrant text read", failure.Message);
                nestedRefused = true;
            };
            Assert.Equal(["30", "40"], Values(await Next()));
            Assert.True(nestedRefused);
            Assert.Equal(["10", "20"], Values(first));
            Assert.Equal(["50", "60"], Values(await Next()));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            hook.AfterGrowth = null;
            context.State.CloseResources();
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IterableRecordAdmissionProtectsTheRowAndRevalidatesTheHeader(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        var lines = new PyList([PyString.FromString("a,b"), PyString.FromString("10,20"),
            PyString.FromString("30,40"), PyString.FromString("50,60")]);
        var source = new LythonRuntime.CsvRecordSource(lines, Options(), Span, context);
        var reader = new LythonRuntime.CsvDictReaderObject(source, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        var raw = new LythonRuntime.CsvReaderObject(source);
        var replacement = new PyList([PyString.FromString("x"), PyString.FromString("y")], context.MemoryGovernor, Span);

        async ValueTask<PyDict> Next()
            => Assert.IsType<PyDict>((asynchronous ? await reader.TryMoveNextAsync() : reader.TryMoveNext()).Value);
        static string Read(PyDict row, string key) => Assert.IsType<PyString>(row.GetItem(PyString.FromString(key))).AsString();

        try
        {
            var first = await Next();
            Exception? nestedFailure = null;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                nestedFailure = Record.Exception(() => { raw.TryMoveNext(); });
            };
            var second = await Next();
            var error = Assert.IsType<LythonRuntimeException>(nestedFailure);
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal("30", Read(second, "a"));
            Assert.Equal(3, raw.LineNum);

            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                reader.TrySetMember("fieldnames", replacement);
            };
            var third = await Next();
            Assert.Equal("50", Read(third, "x"));
            Assert.Equal("60", Read(third, "y"));
            Assert.False(third.TryGetValue(PyString.FromString("a"), out _));
            Assert.Equal("10", Read(first, "a"));
            Assert.Equal("30", Read(second, "a"));
            Assert.Equal(4, raw.LineNum);
            Assert.False((asynchronous ? await reader.TryMoveNextAsync() : reader.TryMoveNext()).HasValue);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            hook.AfterGrowth = null;
            context.State.CloseResources();
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task InferredHeaderSurvivesPendingMultilineIteratorInput()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var lines = new DeferredRecordLines("name,value\n", "first,10\n");
        var source = new LythonRuntime.CsvRecordSource(lines, Options(), Span, context);
        var reader = new LythonRuntime.CsvDictReaderObject(source, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        var first = Assert.IsType<PyDict>((await reader.TryMoveNextAsync()).Value);
        var pending = reader.TryMoveNextAsync().AsTask();

        try
        {
            Assert.False(pending.IsCompleted);
            lines.First.SetResult(PyIterationResult.Yield(PyString.FromString("\"next\n")));
            await lines.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.Equal(3, source.PhysicalLineCount);
            lines.Second.SetResult(PyIterationResult.Yield(PyString.FromString("row\",20\n")));

            var second = Assert.IsType<PyDict>((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Value);
            var key = first.Keys.First();
            Assert.Same(key, second.Keys.First());
            Assert.Equal("first", Assert.IsType<PyString>(first.GetItem(key)).AsString());
            Assert.Equal("next\nrow", Assert.IsType<PyString>(second.GetItem(key)).AsString());
            Assert.Equal(4, source.PhysicalLineCount);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            lines.First.TrySetResult(PyIterationResult.End);
            lines.Second.TrySetResult(PyIterationResult.End);
            try { await pending; }
            finally { context.State.CloseResources(); }
        }
        owner.Dispose();
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

    private sealed class DeferredRecordLines(params string[] initial) : IPyAsyncIteratorValue
    {
        private int _position;
        internal TaskCompletionSource<PyIterationResult> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<PyIterationResult> Second { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public PyIterationResult TryMoveNext() => throw new InvalidOperationException("Expected an asynchronous pull.");
        public ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            var position = _position++;
            if (position < initial.Length)
                return new(PyIterationResult.Yield(PyString.FromString(initial[position])));
            switch (position - initial.Length)
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
