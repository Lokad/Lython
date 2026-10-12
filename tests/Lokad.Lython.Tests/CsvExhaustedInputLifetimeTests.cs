using System.Runtime.CompilerServices;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvExhaustedInputLifetimeTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var indexed in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var retention in new[] { "drop", "field", "input" })
            yield return [indexed, asynchronous, retention];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EofReleasesOnlyUnreachableInput(bool indexed, bool asynchronous, string retention)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var (source, input, keptInput, field) = ReadToEnd(context, indexed, asynchronous, retention);
        Collect();
        Assert.Equal(retention == "input", input.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        var expected = source.OwnedBytes + ChargeReclamationPool.EntryChargeBytes +
            context.State.CallTemporaries.CommittedBackingBytes;
        var count = 1;
        if (field is not null)
        {
            expected += Charge(field);
            count++;
            Assert.Equal(new string('x', 8000), field.AsString());
        }
        if (keptInput is not null)
        {
            expected += Charge(keptInput);
            IEnumerable<object> children = keptInput is PyList list ? list : (PyTuple)keptInput;
            foreach (var child in children) { expected += Charge(child); count++; }
            count++;
        }
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
        Assert.Equal(count, context.State.CallTemporaries.Count);
        Assert.Equal(expected, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        if (keptInput is not null)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(50000, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(expected, context.MemoryGovernor.CurrentCommittedBytes);
        }
        else
        {
            context.MemoryGovernor.Reserve(50000, null);
            context.MemoryGovernor.ReleaseReserved(50000);
        }
        Assert.False(source.TryMoveNext(out _));
        Assert.Null(await source.TryMoveNextAsync());
        GC.KeepAlive(source);
        GC.KeepAlive(field);
        GC.KeepAlive(keptInput);
    }

    private static long Charge(object value)
    {
        Assert.True(((IPyOwnershipSnapshot)value).TrySnapshotOwnership(out var owned));
        return owned + ChargeReclamationPool.EntryChargeBytes;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TerminalParseErrorReleasesInputAndScratch(bool indexed, bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (source, input) = MalformedSource(context, indexed);
        var error = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await source.TryMoveNextAsync(); })
            : Assert.Throws<LythonRuntimeException>(() => source.TryMoveNext(out _));
        Assert.Equal("Error", error.ExceptionType);
        Collect();
        Assert.False(input.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
        Assert.False(source.TryMoveNext(out _));
        Assert.Null(await source.TryMoveNextAsync());
        GC.KeepAlive(source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedReaderCanReplayAnIndependentlyFundedRecord(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new LythonRuntime.CsvRecordSource(new PyList([PyString.FromString("kept,😀\n")]), Options(), new(0, 0, 0, 0), context);
        var row = asynchronous ? await source.TryMoveNextAsync() : source.TryMoveNext(out var next) ? next : null;
        Assert.NotNull(row);
        if (asynchronous) Assert.Null(await source.TryMoveNextAsync());
        else Assert.False(source.TryMoveNext(out _));
        source.PushBack(row);
        var replay = asynchronous ? await source.TryMoveNextAsync() : source.TryMoveNext(out var pushed) ? pushed : null;
        Assert.Same(row, replay);
        Assert.Equal("kept", ((PyString)replay![0]).AsString());
        Assert.Equal("😀", ((PyString)replay[1]).AsString());
        Assert.True(context.State.CallTemporaries.IsTracked(replay));
        Assert.False(source.TryMoveNext(out _));
        Assert.Null(await source.TryMoveNextAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EofPreservesQueuedFinalRecordAndItsFunding(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (source, input, firstField) = CompleteWithQueuedRecord(context, asynchronous);
        Collect();
        Assert.False(input.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        Assert.True(source.OwnedBytes > LythonRuntime.CsvRecordSource.ShellBytes);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var slots = source.OwnedBytes - LythonRuntime.CsvRecordSource.ShellBytes;
        var row = asynchronous ? await source.TryMoveNextAsync() : source.TryMoveNext(out var next) ? next : null;
        Assert.NotNull(row);
        Assert.Equal("second", ((PyString)row[0]).AsString());
        Assert.Equal("😀", ((PyString)row[1]).AsString());
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
        Assert.Equal(before - slots, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(context.State.CallTemporaries.IsTracked(firstField));
        Assert.True(context.State.CallTemporaries.IsTracked(row));
        Assert.False(source.TryMoveNext(out _));
        Assert.Null(await source.TryMoveNextAsync());
        GC.KeepAlive(firstField);
        GC.KeepAlive(row);
        GC.KeepAlive(source);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "sync")]
    [InlineData(true, "sync")]
    [InlineData(false, "async")]
    [InlineData(true, "async")]
    public async Task MixedCursorCleanupReleasesScratchEvenWhenDisposalFails(bool asynchronous, string failure)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var sync = new CleanupCursor(failure == "sync");
        var asyncCursor = new AsyncCleanupCursor(failure == "async");
        var (source, input) = EmptySource(context);
        sync.Input = input;
        asyncCursor.Input = input;
        // Controlled white-box faults at EOF, after the source has selected
        // completion. Both mode cursors can exist after interleaved use.
        typeof(LythonRuntime.CsvRecordSource).GetField("_cursor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, sync);
        typeof(LythonRuntime.CsvRecordSource).GetField("_asyncCursor", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, asyncCursor);
        if (failure == "none")
        {
            if (asynchronous) Assert.Null(await source.TryMoveNextAsync());
            else Assert.False(source.TryMoveNext(out _));
        }
        else
        {
            var error = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await source.TryMoveNextAsync(); })
                : Assert.Throws<LythonRuntimeException>(() => source.TryMoveNext(out _));
            Assert.Equal(failure + " cleanup", error.Message);
        }
        Assert.Equal(1, sync.Disposals);
        Assert.Equal(1, asyncCursor.Disposals);
        Assert.True(sync.InputAliveDuringDisposal);
        Assert.True(asyncCursor.InputAliveDuringDisposal);
        Assert.Equal(LythonRuntime.CsvRecordSource.ShellBytes, source.OwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Collect();
        Assert.False(input.TryGetTarget(out _));
        Assert.False(source.TryMoveNext(out _));
        Assert.Null(await source.TryMoveNextAsync());
        Assert.Equal(1, sync.Disposals);
        Assert.Equal(1, asyncCursor.Disposals);
        GC.KeepAlive(source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.CsvRecordSource Source, WeakReference<object> Input) MalformedSource(LythonRuntime.ExecutionContext context, bool indexed)
    {
        var text = PyString.FromString("\"" + new string('x', 8000), context.MemoryGovernor);
        context.State.CallTemporaries.TrackFreshString(text);
        object input = indexed ? new PyList([text], context.MemoryGovernor, null) : new PyTuple([text], context.MemoryGovernor, null);
        Assert.True(((IPyOwnershipSnapshot)input).TrySnapshotOwnership(out var owned));
        context.State.CallTemporaries.TrackFreshMutable(input, owned);
        return (new(input, Options(), new(0, 0, 0, 0), context), new(input));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.CsvRecordSource Source, WeakReference<object> Input) EmptySource(LythonRuntime.ExecutionContext context)
    {
        var input = new PyList([]);
        return (new(input, Options(), new(0, 0, 0, 0), context), new(input));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.CsvRecordSource Source, WeakReference<object> Input, PyString Field)
        CompleteWithQueuedRecord(LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var (source, input) = EmptySource(context);
        var parser = (LythonRuntime.CsvRecordParser)typeof(LythonRuntime.CsvRecordSource)
            .GetField("_parser", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
        // A controlled parser/source boundary: completion with an already
        // parsed record still queued must not discard that record or its slots.
        parser.Feed("first,1\nsecond,😀\n");
        Assert.True(source.TryMoveNext(out var first));
        var finish = typeof(LythonRuntime.CsvRecordSource).GetMethod(asynchronous ? "FinishExhaustedAsync" : "FinishExhausted",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = finish.Invoke(source, null);
        if (asynchronous) ((ValueTask)result!).GetAwaiter().GetResult();
        return (source, input, (PyString)first[0]);
    }

    private sealed class CleanupCursor(bool fail) : IEnumerator<object>
    {
        public int Disposals { get; private set; }
        public WeakReference<object>? Input { get; set; }
        public bool InputAliveDuringDisposal { get; private set; }
        public object Current => PyNone.Instance;
        object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext() => false;
        public void Reset() => throw new NotSupportedException();
        public void Dispose()
        {
            GC.Collect();
            InputAliveDuringDisposal = InputIsAlive(Input!);
            Disposals++;
            if (fail) throw new LythonRuntimeException("RuntimeError", "sync cleanup", null);
        }
    }

    private sealed class AsyncCleanupCursor(bool fail) : IAsyncEnumerator<object>
    {
        public int Disposals { get; private set; }
        public WeakReference<object>? Input { get; set; }
        public bool InputAliveDuringDisposal { get; private set; }
        public object Current => PyNone.Instance;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        public async ValueTask DisposeAsync()
        {
            await Task.Delay(1).ConfigureAwait(false);
            GC.Collect();
            InputAliveDuringDisposal = InputIsAlive(Input!);
            Disposals++;
            if (fail) throw new LythonRuntimeException("RuntimeError", "async cleanup", null);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.CsvRecordSource Source, WeakReference<object> Input, object? KeptInput, PyString? Field)
        ReadToEnd(LythonRuntime.ExecutionContext context, bool indexed, bool asynchronous, string retention)
    {
        var values = new object[]
        {
            PyString.FromString(new string('x', 8000), context.MemoryGovernor),
            PyString.FromString(new string('y', 8000), context.MemoryGovernor),
        };
        foreach (var value in values) context.State.CallTemporaries.TrackFreshString((PyString)value);
        object input = indexed ? new PyList(values, context.MemoryGovernor, null) : new PyTuple(values, context.MemoryGovernor, null);
        Assert.True(((IPyOwnershipSnapshot)input).TrySnapshotOwnership(out var owned));
        context.State.CallTemporaries.TrackFreshMutable(input, owned);
        var source = new LythonRuntime.CsvRecordSource(input, Options(), new(0, 0, 0, 0), context);
        var weak = new WeakReference<object>(input);
        PyString? field = null;
        var count = 0;
        while (true)
        {
            var row = asynchronous ? source.TryMoveNextAsync().GetAwaiter().GetResult() :
                source.TryMoveNext(out var next) ? next : null;
            if (row is null) break;
            if (count++ == 0 && retention == "field") field = (PyString)row[0];
        }
        Assert.Equal(2, count);
        Assert.Equal(2, source.PhysicalLineCount);
        return (source, weak, retention == "input" ? input : null, field);
    }

    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, PyString.FromString("\""),
        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), true);

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Sampling a live weak target temporarily creates a strong local. Keep it
    // outside an async completion frame whose inline continuation collects it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool InputIsAlive(WeakReference<object> input) => input.TryGetTarget(out _);
}
