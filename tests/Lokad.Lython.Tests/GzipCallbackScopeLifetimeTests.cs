using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class GzipCallbackScopeLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly byte[] Member = Convert.FromHexString("1F8B08007B00000002FF4BCC29C848E44A4A2D49E402006E50306E0B000000");

    [Theory]
    [InlineData("rb", false, false)]
    [InlineData("rb", true, false)]
    [InlineData("wb", false, false)]
    [InlineData("wb", true, false)]
    [InlineData("rb", false, true)]
    [InlineData("rb", true, true)]
    [InlineData("wb", false, true)]
    [InlineData("wb", true, true)]
    public void EscapingCallbackOwnerDropsCreationScopeAndPreservesRetainedPayload(string mode, bool close, bool retain)
    {
        var parent = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 2_000_000 });
        var (owner, raw, frame, payload) = CreateOwner(parent, mode, close, retain);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.Equal(retain, payload.TryGetTarget(out _));
        parent.State.CallTemporaries.Sweep(full: true);
        Assert.True(((IPyOwnershipSnapshot)owner).TrySnapshotOwnership(out var owned));
        Assert.True(owned >= 512);
        Assert.True(parent.MemoryGovernor.CurrentCommittedBytes >= owned + (retain ? 16000 : 0));
        if (!close)
        {
            if (mode == "rb")
                Assert.Equal("alpha\nbeta\n"u8.ToArray(), Assert.IsType<PyBytes>(Call(owner, "read", parent)).Bytes.ToArray());
            else Call(owner, "write", parent, new PyBytes("gamma\n"u8.ToArray()));
            Call(owner, "close", parent);
        }
        Assert.Equal(false, Get(raw, "closed"));
        Assert.Equal(0, parent.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(owner); GC.KeepAlive(raw); GC.KeepAlive(parent);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Owner, object Raw, WeakReference<LythonRuntime.ExecutionContext> Frame, WeakReference<PyString> Payload)
        CreateOwner(LythonRuntime.ExecutionContext parent, string mode, bool close, bool retain)
    {
        var frame = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('x', 16000), parent.MemoryGovernor);
        parent.State.CallTemporaries.TrackFreshString(payload);
        frame.Variables["unrelated"] = payload;
        if (retain) parent.Variables["kept"] = payload;
        var raw = new LythonRuntime.BytesIOObject(parent.Services, Span);
        parent.State.CallTemporaries.TrackFreshMutable(raw, raw.OwnedBytes, Span);
        if (mode == "rb") { raw.Write(new PyBytes(Member), Span); raw.Seek(0, 0, Span); }
        Assert.True(LythonRuntime.GzipModule.Instance.TryGetMember("GzipFile", out var constructor));
        var owner = ((LythonRuntime.ICallable)constructor).Invoke(
            [CallArgumentValue.Keyword("fileobj", raw), CallArgumentValue.Keyword("mode", PyString.FromString(mode)),
             CallArgumentValue.Keyword("mtime", BigInteger.Zero)], Span, frame);
        if (close) Call(owner, "close", frame);
        return (owner, raw, new(frame), new(payload));
    }

    private static object Get(object target, string name)
    {
        Assert.True(((IPyDynamicAttributes)target).TryGetMember(name, out var value));
        return value;
    }

    private static object Call(object owner, string name, LythonRuntime.ExecutionContext context, params object[] args)
        => ((LythonRuntime.ICallable)Get(owner, name)).Invoke(args.Select(CallArgumentValue.Positional).ToArray(), Span, context);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedOrFailedOperationDropsItsCallerScope(bool fail)
    {
        var parent = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (owner, raw, _, _) = CreateOwner(parent, "rb", false, false);
        var (frame, payload) = Operate(owner, parent, fail);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.False(payload.TryGetTarget(out _));
        Call(owner, "close", parent);
        GC.KeepAlive(owner); GC.KeepAlive(raw);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<LythonRuntime.ExecutionContext> Frame, WeakReference<PyString> Payload)
        Operate(object owner, LythonRuntime.ExecutionContext parent, bool fail)
    {
        var caller = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('y', 16000), parent.MemoryGovernor);
        parent.State.CallTemporaries.TrackFreshString(payload);
        caller.Variables["unrelated"] = payload;
        if (fail) Assert.Equal("TypeError", Assert.Throws<LythonRuntimeException>(() =>
            Call(owner, "read", caller, PyString.FromString("invalid"))).ExceptionType);
        else Assert.Equal("a"u8.ToArray(), Assert.IsType<PyBytes>(Call(owner, "read", caller, BigInteger.One)).Bytes.ToArray());
        return (new(caller), new(payload));
    }
}
