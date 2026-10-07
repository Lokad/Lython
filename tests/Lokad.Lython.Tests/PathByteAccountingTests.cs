using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PathByteAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(310)]
    public void DeniedReadsRefundAcquisitionAndFreshResult(long cap)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[123]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var method = Method("read_bytes");
        for (var i = 0; i < 50; i++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => method.Invoke([], Span, context));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void AbandonedReadResultsRefundTheirOwnedCopies()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", new byte[1024]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 });
        for (var i = 0; i < 50; i++)
        {
            Consume(context);
            Sweep(context);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void FailedWritesRefundStagingAndLeaveInputOwnedByCaller()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });
        var input = new PyBytes(new byte[1024], context.MemoryGovernor, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var method = Method("write_bytes");
        for (var i = 0; i < 50; i++)
        {
            host.FailNextWriteBytes("/blob", "denied");
            var failure = Assert.Throws<HostOperationException>(() => method.Invoke([CallArgumentValue.Positional(input)], Span, context));
            Assert.Contains("write_bytes", failure.Message);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(1024, input.Length);
        }
        GC.KeepAlive(input);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(LythonRuntime.ExecutionContext context)
    {
        var value = Assert.IsType<PyBytes>(Method("read_bytes").Invoke([], Span, context));
        Assert.Equal(1024, value.Length);
        GC.KeepAlive(value);
    }

    private static LythonRuntime.ICallable Method(string name)
    {
        Assert.True(LythonRuntime.PathMembers.TryGetMember(new PyPath(PyString.FromString("/blob")), name, out var method));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(method);
    }

    private static void Sweep(LythonRuntime.ExecutionContext context)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
    }
}
