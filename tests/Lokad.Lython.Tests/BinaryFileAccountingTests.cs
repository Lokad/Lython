using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class BinaryFileAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public async Task EmptyFilesUseSmallGovernedWindows()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", []);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024) },regexProvider:Utf8RegexProvider.Instance);
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false);
        Assert.True(handle.TryGetMember("read", out var method));
        var result = Assert.IsType<PyBytes>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([], Span, context));
        Assert.Equal(0, result.Length);
        handle.Exit();
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, handle.OwnedBytes);
    }

    [Theory]
    [InlineData(200)]
    public void DeniedBinaryConstructionRefundsShellAndWindow(long cap)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) },regexProvider:Utf8RegexProvider.Instance);
        for (var index = 0; index < 50; index++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
                    context, Span, false).GetAwaiter().GetResult());
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void CompletedReaderBatchesReturnTheirSharedAllowance()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var budget = new LythonMemoryBudget(16 * 1024);
        for (var index = 0; index < 8; index++)
        {
            using (var batch = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(host,
                    new LythonRunOptions { MemoryBudget = batch },regexProvider:Utf8RegexProvider.Instance);
                try
                {
                    ReadWithoutGuestClose(context);
                    Assert.True(budget.ReservedBytes >= 1024);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                }
                finally { context.State.TryCloseOpenFileWriters(context); }
                Assert.Equal(index + 1, host.ClosedFileCount);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1152)]
    public async Task DeniedFirstReadReleasesPrivateStorageAfterClose(long readHeadroom)
    {
        var host = new MockLythonHost(); host.SeedBytes("/blob", new byte[1024]);
        var budget = new LythonMemoryBudget(16 * 1024);
        var context = new LythonRuntime.ExecutionContext(host,
            new LythonRunOptions { MemoryBudget = budget }, regexProvider: Utf8RegexProvider.Instance);
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob",
            LythonRuntime.TextFileOperation.Read, context, Span, false);
        // Deny the read window, or its result-copy overlap, independently of
        // acquisition metadata and changing tiny-budget construction thresholds.
        Assert.True(budget.TryReserve(16 * 1024 - budget.ReservedBytes - readHeadroom, out var withheld));
        using var funding = withheld;
        Assert.Equal(0, host.RangedReadCount);
        Assert.True(handle.TryGetMember("read", out var method));
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([], Span, context));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(readHeadroom < 1024 ? 0 : 1, host.RangedReadCount);
        handle.Exit();
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public async Task CloseReleasesReaderWindowExactlyOnce()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false);
        Assert.True(handle.TryGetMember("read", out var method));
        var value = Assert.IsType<PyBytes>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(method)
            .Invoke([CallArgumentValue.Positional(new System.Numerics.BigInteger(1))], Span, context));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        handle.Exit();
        Assert.Equal(before - 1024, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(1, value.Length);
        Assert.Equal((byte)0, value.Bytes[0]);
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, handle.OwnedBytes);
        handle.Exit();
        Assert.Equal(before - 1024, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(handle);
    }

    [Fact]
    public async Task FailedFlushRefundsStagingAndPreservesRetryableWriter()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Write,
            context, Span, false);
        var input = new PyBytes(new byte[1024], context.MemoryGovernor, Span);
        Assert.True(handle.TryGetMember("write", out var method));
        Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([CallArgumentValue.Positional(input)], Span, context);
        Assert.True(handle.TryGetMember("flush", out var flush));
        var flushCall = Assert.IsAssignableFrom<LythonRuntime.ICallable>(flush);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var index = 0; index < 50; index++)
        {
            host.FailNextWriteBytes("/blob", "denied");
            Assert.Throws<HostOperationException>(() => flushCall.Invoke([], Span, context));
            Assert.False(handle.IsClosed);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        handle.Exit();
        Assert.True(handle.IsClosed);
        Assert.Equal(input.ToArray(), host.ReadBytes("/blob"));
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, handle.OwnedBytes);
        GC.KeepAlive(input);
    }

    private static void ReadWithoutGuestClose(LythonRuntime.ExecutionContext context)
    {
        var handle = LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false).GetAwaiter().GetResult();
        Assert.True(handle.TryGetMember("read", out var method));
        var result = Assert.IsType<PyBytes>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([], Span, context));
        Assert.Equal(1024, result.Length);
        GC.KeepAlive(handle); GC.KeepAlive(result);
    }

}
