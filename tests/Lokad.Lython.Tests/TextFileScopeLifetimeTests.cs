using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextFileScopeLifetimeTests
{
    [Theory]
    [InlineData("r", false)]
    [InlineData("r", true)]
    [InlineData("w", false)]
    [InlineData("w", true)]
    [InlineData("a", false)]
    [InlineData("a", true)]
    public void EscapingHandleRetainsStorageWithoutCreationLocals(string mode, bool close)
    {
        var host = new MockLythonHost();
        host.SeedFile("/text", "g");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 28000 });
        var (handle, frame, payload) = CreateHandle(context, mode, close);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.False(payload.TryGetTarget(out _));
        context.State.CallTemporaries.Sweep(full: true);
        var expected = mode == "r" ? 2 : 1;
        Assert.Equal(expected, context.State.CallTemporaries.Count);
        var funded = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(((IPyOwnershipSnapshot)handle).TrySnapshotOwnership(out var shell));
        Assert.Equal(64, shell);
        Assert.True(funded >= shell + expected * ChargeReclamationPool.EntryChargeBytes);
        context.MemoryGovernor.Reserve(16000, null);
        context.MemoryGovernor.ReleaseReserved(16000);
        if (!close) handle.Exit();
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(handle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Handle Handle, WeakReference<LythonRuntime.ExecutionContext> Frame, WeakReference<PyString> Payload)
        CreateHandle(LythonRuntime.ExecutionContext parent, string mode, bool close)
    {
        var frame = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('x', 16000), parent.MemoryGovernor);
        parent.State.CallTemporaries.TrackFreshString(payload);
        frame.Variables["unrelated"] = payload;
        var handle = mode switch
        {
            "r" => Handle.ForRead("/text", frame, LythonRuntime.TextEncodingMode.Utf8,
                LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal),
            "w" => Handle.ForWrite("/text", frame),
            _ => Handle.ForAppend("/text", frame, LythonRuntime.TextEncodingMode.Utf8,
                LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal),
        };
        if (close) handle.Exit();
        return (handle, new(frame), new(payload));
    }
}
