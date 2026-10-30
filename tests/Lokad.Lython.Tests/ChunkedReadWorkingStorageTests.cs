using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using State = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.ChunkedTextFileReadState;

namespace Lokad.Lython.Tests;

public sealed class ChunkedReadWorkingStorageTests
{
    [Fact]
    public async Task PendingReadKeepsWorkingCapacityUntilCancellationFinishes()
    {
        const int window = 32;
        var host = new DelayedLythonHost();
        host.SeedFile("/file", new string('x', 4 * window));
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host,
                new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
            var state = await CreateAsync(context, window);
            var prefix = await state.ReadAsync(1);
            context.MemoryGovernor.RefundUnpublishedValue(prefix);
            var started = host.PauseReadUntilCancellation("/file");
            var pending = state.ReadAsync(-1).AsTask();
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= state.OwnedBytes + window - 1);
                cancellation.Cancel();
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Equal(state.OwnedBytes, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                cancellation.Cancel();
                try { await pending; }
                catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                state.Release();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompletedReadReleasesWorkingStorageAndKeepsItsResultFunded()
    {
        const int window = 32;
        var host = new DelayedLythonHost();
        var content = new string('x', 4 * window);
        host.SeedFile("/file", content);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var state = await CreateAsync(context, window);
            var observed = false;
            var completedReads = 0;
            host.OnTextReadCompleted = () =>
            {
                if (!observed && ++completedReads > 1)
                {
                    Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= state.OwnedBytes + window);
                    observed = true;
                }
            };
            try
            {
                var value = await state.ReadAsync(-1);
                Assert.Equal(content, value.AsString());
                Assert.True(observed);
                state.Release();
                Assert.Equal(state.OwnedBytes + value.CommittedOwnedBytes, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(content, value.AsString());
            }
            finally { state.Release(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "abcdefghijklmnop\nZ", 4, 4, "abcd", "efghijklmnop\n")]
    [InlineData(true, "abcdefghijklmnop\nZ", 4, 4, "abcd", "efghijklmnop\n")]
    [InlineData(false, "abcdefghijklmnop\nZ", 4, 5, "abcde", "fghijklmnop\n")]
    [InlineData(true, "abcdefghijklmnop\nZ", 4, 5, "abcde", "fghijklmnop\n")]
    [InlineData(false, "é😀abcé😀def\nZ", 4, 2, "é😀", "abcé😀def\n")]
    [InlineData(true, "é😀abcé😀def\nZ", 4, 2, "é😀", "abcé😀def\n")]
    public async Task SizedReadlineCountsRunesAcrossWindows(bool asynchronous, string content, int window, int size, string expected, string tail)
    {
        var host = new MockLythonHost();host.SeedFile("/file", content);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var state = Open(context, window);
            try
            {
                var value = asynchronous ? await state.ReadLineAsync(size) : state.ReadLine(size);
                Assert.Equal(expected, value.AsString());
                Assert.Equal(new BigInteger(System.Text.Encoding.UTF8.GetByteCount(expected)), state.Position);
                Assert.Equal(tail, (asynchronous ? await state.ReadLineAsync(-1) : state.ReadLine(-1)).AsString());
                Assert.Equal("Z", (asynchronous ? await state.ReadAsync(-1) : state.Read(-1)).AsString());
            }
            finally { state.Release(); }
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeniedWorkingGrowthDoesNotConsumeItsWindow(bool asynchronous, bool line)
    {
        const int window = 32;
        const long cap = 1024 * 1024;
        var host = new MockLythonHost();host.SeedFile("/file",new string('x',4*window));
        var budget = new LythonMemoryBudget(cap);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host,new LythonRunOptions { MemoryBudget=owner },regexProvider:Utf8RegexProvider.Instance);
            var state = await CreateAsync(context,window);
            var prefix = state.Read(1);
            context.MemoryGovernor.RefundUnpublishedValue(prefix);
            // First consumption leaves window-1 bytes for the builder; its next
            // append needs 2*window-1 bytes while that old capacity is still live.
            Assert.True(budget.TryReserve(cap-budget.ReservedBytes-((window-1)+(2*window-1)-1),out var pressure));
            try
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) { if(line) await state.ReadLineAsync(-1);else await state.ReadAsync(-1); }
                    else { if(line) state.ReadLine(-1);else state.Read(-1); }
                });
                Assert.Equal("MemoryError",error.ExceptionType);
                Assert.Equal(new BigInteger(window),state.Position);
                Assert.Equal(2,host.RangedReadCount);
                Assert.Equal(2*window-1,context.MemoryGovernor.LastDeniedReservationBytes);
                Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { state.Release();pressure.Dispose(); }
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    private static State Open(LythonRuntime.ExecutionContext context,int window)
        => CreateAsync(context, window).GetAwaiter().GetResult();

    private static async ValueTask<State> CreateAsync(LythonRuntime.ExecutionContext context, int window)
    {
        var file = new OpenedByteFile(context.Services);
        await file.OpenAsync("/file", new LythonFileOpenOptions(LythonFileAccess.Read), null, context.Host is not ILythonSynchronousHostCapability { CompletesSynchronously: true });
        return new(file, context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal, window);
    }
}
