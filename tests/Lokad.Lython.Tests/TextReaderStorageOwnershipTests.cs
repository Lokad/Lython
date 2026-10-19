using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using State = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.ChunkedTextFileReadState;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextReaderStorageOwnershipTests
{
    private const long Cap = 262144;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectedReaderRemainsBatchOwnedWhileEscapedLineKeepsItsFunding(bool retainLine)
    {
        var host = new MockLythonHost(); host.SeedFile("/file", "first café 😀\n" + new string('x', 128));
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var retained = Open(context); retained.Prime();
        var dropped = Drop(context, retainLine);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(dropped.Reference.TryGetTarget(out _));
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(committed >= dropped.Charge);
        Assert.Equal("first café 😀\n", retained.ReadLine(-1).AsString());
        if (dropped.Line is { } line)
        {
            Assert.Equal("first café 😀\n", line.AsString());
            Assert.True(line.CommittedOwnedBytes > 0);
        }
        GC.KeepAlive(retained);
        GC.KeepAlive(dropped.Line);
    }

    [Fact]
    public void SuccessfulCloseReleasesBackingAndKeepsTheShellBatchOwned()
    {
        var host = new MockLythonHost(); host.SeedFile("/file", new string('x', 128));
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var reference = CloseAndDrop(context);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(reference.TryGetTarget(out _));
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(committed >= State.ShellBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidPrimedUtf8RefundsUnpublishedStateAndBuffersWithoutWaitingForGc(bool asynchronous)
    {
        var host = new MockLythonHost(); host.SeedRawTextUtf8("/file", [0xff]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var error = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Handle.ForReadAsync("/file", context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal))
            : Assert.Throws<LythonRuntimeException>(() => Handle.ForRead("/file", context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal));
        Assert.Equal("UnicodeDecodeError", error.ExceptionType);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static State Open(LythonRuntime.ExecutionContext context) => new("/file", context,
        LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
        LythonRuntime.TextNewlineMode.TranslateUniversal, 32);

    [Fact]
    public async Task CancellationDuringActualPrimingRefundsUnpublishedStorage()
    {
        var host = new DelayedLythonHost(); host.SeedFile("/file", "data");
        var started = host.PauseReadUntilCancellation("/file");
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        { MemoryBudget = new LythonMemoryBudget(Cap), CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var open = Handle.ForReadAsync("/file", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal).AsTask();
        try { await started.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(open.IsCompleted); }
        finally { cancellation.Cancel(); }
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => open);
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Contains("cancel", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<State> Reference, long Charge, PyString? Line) Drop(LythonRuntime.ExecutionContext context, bool retainLine)
    {
        var state = Open(context); state.Prime();
        var line = retainLine ? state.ReadLine(-1) : null;
        return (new(state), state.OwnedBytes, line);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<State> CloseAndDrop(LythonRuntime.ExecutionContext context)
    {
        var state = Open(context); state.Prime(); state.Release(); state.Release();
        Assert.Equal(State.ShellBytes, state.OwnedBytes);
        return new(state);
    }
}
