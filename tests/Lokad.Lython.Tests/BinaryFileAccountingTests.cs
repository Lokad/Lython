using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BinaryFileAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public async Task EmptyFilesUseSmallGovernedWindows()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", []);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false);
        Assert.True(handle.TryGetMember("read", out var method));
        var result = Assert.IsType<PyBytes>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([], Span, context));
        Assert.Equal(0, result.Length);
        Assert.True(context.MemoryGovernor.PeakCommittedBytes <= 1024);
        handle.Exit();
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, handle.OwnedBytes);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(1600)]
    public void DeniedBinaryConstructionRefundsShellAndWindow(long cap)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        for (var index = 0; index < 50; index++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
                    context, Span, false).GetAwaiter().GetResult());
            Assert.Equal("MemoryError", failure.ExceptionType);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void AbandonedReadHandlesRefundRetainedWindows()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 });
        for (var index = 0; index < 50; index++)
        {
            AbandonReader(context);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public async Task CloseReleasesReaderWindowExactlyOnce()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 });
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        handle.Exit();
        Assert.Equal(before - 1024, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(LythonRuntime.ExecutionContext.BinaryFileHandle.ShellBytes, handle.OwnedBytes);
        handle.Exit();
        Assert.Equal(before - 1024, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(handle);
    }

    [Fact]
    public async Task FailedPublicationRefundsStagingAndPreservesRetryableWriter()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 });
        var handle = await LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Write,
            context, Span, false);
        var input = new PyBytes(new byte[1024], context.MemoryGovernor, Span);
        Assert.True(handle.TryGetMember("write", out var method));
        Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([CallArgumentValue.Positional(input)], Span, context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var index = 0; index < 50; index++)
        {
            host.FailNextWriteBytes("/blob", "denied");
            Assert.Throws<HostOperationException>(() => handle.Exit());
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonReader(LythonRuntime.ExecutionContext context)
    {
        var handle = LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", LythonRuntime.TextFileOperation.Read,
            context, Span, false).GetAwaiter().GetResult();
        Assert.True(handle.TryGetMember("read", out var method));
        var result = Assert.IsType<PyBytes>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).Invoke([], Span, context));
        Assert.Equal(1024, result.Length);
        GC.KeepAlive(handle); GC.KeepAlive(result);
    }

    private static void Sweep(LythonRuntime.ExecutionContext context)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
    }
}
