using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextReadBorrowLifetimeTests
{
    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task RequestedCleanupKeepsDecoderAndAppendBorrowsFunded(bool asynchronous, bool decoding)
    {
        var host = new MockLythonHost();
        var text = "a" + new string(decoding ? 'é' : 'x', 40) + "\nnext\n";
        host.SeedBytes("/input", System.Text.Encoding.Latin1.GetBytes(text));
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = hook });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                decoding ? LythonRuntime.TextEncodingMode.Latin1 : LythonRuntime.TextEncodingMode.Utf8,
                LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal,
                decoding ? 64 : 16);
            try
            {
                if (!decoding) Assert.Equal("a", reader.Read(1).AsString());
                var reached = false;
                hook.AfterGrowth = _ =>
                {
                    // Initial raw capacity is not published until admission
                    // returns. The decoder's next allocation borrows that array.
                    if (decoding && reader.OwnedBytes == LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes) return;
                    hook.AfterGrowth = null;
                    reached = true;
                    var held = budget.ReservedBytes;
                    reader.Release();
                    Assert.Equal(held, budget.ReservedBytes);
                };
                async Task<PyString> ReadLine() => asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
                if (decoding) Assert.Equal(text[..(text.IndexOf('\n') + 1)], (await ReadLine()).AsString());
                else
                {
                    // Buffered copying may finish, but the next refill cannot
                    // start host I/O after cleanup was requested.
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(ReadLine);
                    Assert.Equal("ValueError", error.ExceptionType);
                }
                Assert.True(reached);
                Assert.Equal(LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes, reader.OwnedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { hook.AfterGrowth = null; reader.Release(); context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task RequestedCleanupKeepsTheReadWindowFundedUntilItsCopyFinishes(bool asynchronous, int failureKind)
    {
        var host = new MockLythonHost();
        host.SeedFile("/input", "a" + new string('x', 40) + "\nnext\n");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(host, new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 64);
            try
            {
                Assert.Equal("a", reader.Read(1).AsString());
                var reached = false;
                LythonRuntimeException? first = null;
                var origin = new LythonSourceSpan(121, 3, 8, 5);
                hook.AfterGrowth = _ =>
                {
                    hook.AfterGrowth = null;
                    reached = true;
                    var funded = budget.ReservedBytes;
                    reader.Release();
                    // The admitted output is not populated yet; its source is
                    // still a live borrower even after cleanup was requested.
                    Assert.Equal(funded, budget.ReservedBytes);
                    if (failureKind == 1) cancellation.Cancel();
                    if (failureKind == 2) first = context.MemoryGovernor.RefuseAdmission("controlled read refusal", 777, origin);
                };
                async Task<PyString> ReadLine() => asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
                if (failureKind == 0)
                    Assert.Equal(new string('x', 40) + "\n", (await ReadLine()).AsString());
                else
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(ReadLine);
                    if (failureKind == 1) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    else
                    {
                        Assert.Same(first, failure);
                        Assert.Equal(origin, failure.Span);
                        Assert.Equal(777L, failure.Attribution?.Requested);
                    }
                }
                Assert.True(reached);
                Assert.Equal(LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes, reader.OwnedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                hook.AfterGrowth = null;
                reader.Release();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0, false), InlineData(0, true)]
    [InlineData(1, true), InlineData(2, true)]
    public async Task AsynchronousReadReturnsWhileRequestedCleanupIsStillPending(int failureKind, bool closeFails)
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/input", "a" + new string('x', 40) + "\nnext\n");
        var budget = new LythonMemoryBudget(1024 * 1024);
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(host, new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, true);
            var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 64);
            Assert.Equal("a", (await reader.ReadAsync(1)).AsString());
            host.BeforeFileClose = async () =>
            {
                closing.SetResult();
                await finishClose.Task;
                if (closeFails) throw new LythonHostException(LythonHostFailureKind.Transport, "controlled close failure");
            };
            LythonRuntimeException? first = null;
            long borrowedStorageAtCleanup = 0;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                borrowedStorageAtCleanup = reader.OwnedBytes;
                reader.Release();
                if (failureKind == 1) cancellation.Cancel();
                if (failureKind == 2) first = context.MemoryGovernor.RefuseAdmission("controlled read refusal", 777, null);
            };
            // A blocked native entry would otherwise prevent this test from
            // releasing the controlled close. Always join this owned worker.
            var pending = Task.Run(async () =>
            {
                var line = reader.ReadLineAsync(-1);
                returned.SetResult();
                return await line;
            });
            try
            {
                await closing.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(borrowedStorageAtCleanup > LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes);
                Assert.True(budget.ReservedBytes >= borrowedStorageAtCleanup);
                finishClose.SetResult();
                if (failureKind == 0 && !closeFails)
                    Assert.Equal(new string('x', 40) + "\n", (await pending).AsString());
                else if (failureKind == 0)
                {
                    var failure = await Assert.ThrowsAsync<HostOperationException>(() => pending);
                    Assert.Equal("OSError", failure.ExceptionType);
                }
                else
                {
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                    if (failureKind == 2) Assert.Same(first, failure);
                    else Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                }
                Assert.Equal(LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes, reader.OwnedBytes);
                Assert.Equal(1, host.ClosedFileCount);
            }
            finally
            {
                finishClose.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (LythonRuntimeException) when (failureKind != 0 || closeFails) { }
                finally { reader.Release(); context.State.CloseResources(); }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task PendingHostReadKeepsItsBuffersAndTransportUntilCancellationUnwinds()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/input", new string('x', 128));
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, true);
            var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 32);
            var prefix = await reader.ReadAsync(1);
            context.MemoryGovernor.RefundUnpublishedValue(prefix);
            var entered = host.PauseReadUntilCancellation("/input");
            var pending = reader.ReadAsync(-1).AsTask();
            try
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                var held = budget.ReservedBytes;
                reader.Release();
                Assert.False(pending.IsCompleted);
                Assert.Equal(held, budget.ReservedBytes);
                cancellation.Cancel();
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Equal(LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes, reader.OwnedBytes);
                Assert.Equal(reader.OwnedBytes, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                cancellation.Cancel();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.Cancelled) { }
                reader.Release();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RejectedReentryUnwindsWithoutConsumingOrClosingTheOuterRead()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input", "a" + new string('x', 40) + "\nnext\n");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = hook });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, false);
            var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 64);
            try
            {
                Assert.Equal("a", reader.Read(1).AsString());
                hook.AfterGrowth = _ => { hook.AfterGrowth = null; reader.ReadLine(-1); };
                var error = Assert.Throws<LythonRuntimeException>(() => reader.ReadLine(-1));
                Assert.Equal("RuntimeError", error.ExceptionType);
                Assert.Contains("reentrant", error.Message);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(new string('x', 40) + "\n", reader.ReadLine(-1).AsString());
                Assert.Equal("next\n", reader.ReadLine(-1).AsString());
            }
            finally
            {
                hook.AfterGrowth = null;
                reader.Release();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
