using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BinaryFileScopeLifetimeTests
{
    [Theory]
    [InlineData("Read", false)]
    [InlineData("Read", true)]
    [InlineData("Write", false)]
    [InlineData("Write", true)]
    [InlineData("Append", false)]
    [InlineData("Append", true)]
    public void EscapingHandleRetainsItsStorageWithoutRetainingCreationLocals(string mode, bool close)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", [(byte)'g']);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 28000 });
        var (handle, frame, payload) = CreateHandle(context, Enum.Parse<LythonRuntime.TextFileOperation>(mode), close);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.False(payload.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.Equal(handle.OwnedBytes + ChargeReclamationPool.EntryChargeBytes +
            context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        context.MemoryGovernor.Reserve(16000, null);
        context.MemoryGovernor.ReleaseReserved(16000);
        if (!close) handle.Exit();
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(handle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.ExecutionContext.BinaryFileHandle Handle, WeakReference<LythonRuntime.ExecutionContext> Frame,
        WeakReference<PyString> Payload) CreateHandle(LythonRuntime.ExecutionContext parent, LythonRuntime.TextFileOperation mode, bool close)
    {
        var frame = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('x', 16000), parent.MemoryGovernor);
        parent.State.CallTemporaries.TrackFreshString(payload);
        frame.Variables["unrelated"] = payload;
        var handle = LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", mode, frame, new(0, 0, 0, 0), false)
            .GetAwaiter().GetResult();
        if (close) handle.Exit();
        return (handle, new(frame), new(payload));
    }
}
