using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GzipFileAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    // Frozen CPython 3.13.16 member: alpha\nbeta\n, mtime=123.
    private static readonly byte[] Member = Convert.FromHexString("1F8B08007B00000002FF4BCC29C848E44A4A2D49E402006E50306E0B000000");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingReaderReleasesNativeAndWindowButKeepsSuppliedStreamOpen(bool asynchronous)
    {
        var context = Context();
        var raw = Raw(context, Member);
        var reader = await Construct(context, raw, "rb", asynchronous);
        var shell = Snapshot(reader);
        Assert.Equal(512, shell);
        var output = Assert.IsType<PyBytes>(await Call(reader, "read", context, asynchronous, BigInteger.One));
        Assert.Equal(new byte[] { (byte)'a' }, output.Bytes.ToArray());
        Assert.True(Snapshot(reader) >= shell + LythonRuntime.ZlibModule.InflationScratchBytes + 8192);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var owned = Snapshot(reader);
        await Call(reader, "close", context, asynchronous);
        Assert.Equal(shell, Snapshot(reader));
        Assert.Equal(before - (owned - shell), context.MemoryGovernor.CurrentCommittedBytes);
        await Call(reader, "close", context, asynchronous);
        Assert.Equal(before - (owned - shell), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(raw.TryGetMember("closed", out var closed));
        Assert.Equal(false, closed);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // Aliases share one registration; close must not refund the live shell.
        var entries = context.State.CallTemporaries.Count;
        context.State.CallTemporaries.TrackCallResult(reader, Span);
        Assert.Equal(entries, context.State.CallTemporaries.Count);
        GC.KeepAlive(raw); GC.KeepAlive(reader); GC.KeepAlive(output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriterOwnsPersistentNativeStateAndCloseRefundsBuffers(bool asynchronous)
    {
        var context = Context();
        var raw = Raw(context, []);
        var writer = await Construct(context, raw, "wb", asynchronous);
        Assert.Equal(512 + LythonRuntime.ZlibModule.CompressionScratchBytes, Snapshot(writer));
        await Call(writer, "write", context, asynchronous, new PyBytes(new byte[1000]));
        Assert.True(Snapshot(writer) > 512 + LythonRuntime.ZlibModule.CompressionScratchBytes);
        await Call(writer, "close", context, asynchronous);
        Assert.Equal(512, Snapshot(writer));
        // Remove dead header/trailer/input values but retain both live aliases.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(512 + raw.OwnedBytes + 2 * ChargeReclamationPool.EntryChargeBytes
            + context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var once = context.MemoryGovernor.CurrentCommittedBytes;
        await Call(writer, "close", context, asynchronous);
        Assert.Equal(once, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.IsType<PyNone>(Get(writer, "fileobj"));
        GC.KeepAlive(raw); GC.KeepAlive(writer);
    }

    [Fact]
    public void DroppedReaderFinalizesAndReclaimsItsNativeCouponAndCursorGraph()
    {
        var context = Context();
        var weak = DropReader(context);
        for (var attempt = 0; attempt < 3; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Assert.False(weak.IsAlive);
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(0, context.State.CallTemporaries.Count);
        Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference DropReader(LythonRuntime.ExecutionContext context)
    {
        var reader = Construct(context, Raw(context, Member), "rb", false).GetAwaiter().GetResult();
        Call(reader, "read", context, false, BigInteger.One).GetAwaiter().GetResult();
        Assert.True(Snapshot(reader) >= LythonRuntime.ZlibModule.InflationScratchBytes);
        return new WeakReference(reader);
    }

    [Theory]
    [InlineData(80000)]
    [InlineData(131500)]
    [InlineData(140000)]
    public async Task RepeatedInflationDenialsLeaveNoReservationsAndCloseReleasesOwnership(long cap)
    {
        var context = Context(cap);
        var raw = Raw(context, Member);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            raw.Seek(0, 0, Span);
            var reader = await Construct(context, raw, "rb", false);
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Call(reader, "read", context, false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            await Call(reader, "close", context, false);
            Assert.Equal(512, Snapshot(reader));
            GC.KeepAlive(reader);
        }
        GC.KeepAlive(raw);
    }

    [Theory]
    [InlineData(524000)]
    [InlineData(525000)]
    public async Task ConstructorDenialRollsBackTheUnpublishedWrapper(long cap)
    {
        var context = Context(cap);
        var raw = Raw(context, []);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Construct(context, raw, "wb", false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(raw.OwnedBytes + ChargeReclamationPool.EntryChargeBytes
                + context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        }
        GC.KeepAlive(raw);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyDeflateBlocksConsumeWorkBeforeProducingAnyOutput(bool asynchronous)
    {
        // A valid member containing 10,000 empty stored blocks. Output-size
        // checks alone cannot govern this input; the compressed cursor must.
        var member = new byte[10 + 5 * 10000 + 2 + 8];
        Member.AsSpan(0, 10).CopyTo(member);
        for (var offset = 10; offset < 50010; offset += 5)
        { member[offset + 3] = 255; member[offset + 4] = 255; }
        member[50010] = 3;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 50 });
        var raw = Raw(context, member);
        var reader = await Construct(context, raw, "rb", asynchronous);
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await Call(reader, "read", context, asynchronous));
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Contains("step", error.Message);
        Assert.True(context.Limits.ExecutionStepCount > 50);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // Runtime cleanup bypasses the already exhausted guest call budget.
        Assert.False(Assert.IsAssignableFrom<IPyContextManager>(reader)
            .Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance));
        Assert.Equal(512, Snapshot(reader));
        GC.KeepAlive(raw); GC.KeepAlive(reader);
    }

    [Theory]
    [InlineData("rb")]
    [InlineData("wb")]
    public async Task CancellationKeepsExistingOwnershipAndCleanupReleasesNativeState(string mode)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        var raw = Raw(context, mode == "rb" ? Member : []);
        var wrapper = await Construct(context, raw, mode, true);
        if (mode == "rb") await Call(wrapper, "read", context, true, BigInteger.One);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await Call(wrapper, mode == "rb" ? "read" : "write", context, true,
                mode == "rb" ? BigInteger.One : new PyBytes([1, 2, 3])));
        Assert.Equal("execution canceled", error.Message);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var cleanup = Assert.IsAssignableFrom<IPyAsyncContextManager>(wrapper);
        if (mode == "rb") Assert.False(await cleanup.ExitAsync(PyNone.Instance, PyNone.Instance, PyNone.Instance));
        else await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await cleanup.ExitAsync(PyNone.Instance, PyNone.Instance, PyNone.Instance));
        Assert.Equal(512, Snapshot(wrapper));
        Assert.Equal(true, Get(wrapper, "closed"));
        Assert.True(raw.TryGetMember("closed", out var closed));
        Assert.Equal(false, closed);
        GC.KeepAlive(raw); GC.KeepAlive(wrapper);
    }

    private static LythonRuntime.ExecutionContext Context(long? cap = null)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });

    private static LythonRuntime.BytesIOObject Raw(LythonRuntime.ExecutionContext context, byte[] initial)
    {
        var raw = new LythonRuntime.BytesIOObject(context.Services, Span);
        context.State.CallTemporaries.TrackFreshMutable(raw, raw.OwnedBytes, Span);
        if (initial.Length > 0) { raw.Write(new PyBytes(initial), Span); raw.Seek(0, 0, Span); }
        return raw;
    }

    private static async ValueTask<object> Construct(LythonRuntime.ExecutionContext context, object raw,
        string mode, bool asynchronous)
    {
        Assert.True(LythonRuntime.GzipModule.Instance.TryGetMember("GzipFile", out var member));
        var constructor = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        CallArgumentValue[] args = [CallArgumentValue.Keyword("fileobj", raw),
            CallArgumentValue.Keyword("mode", PyString.FromString(mode)), CallArgumentValue.Keyword("mtime", BigInteger.Zero)];
        return asynchronous ? await constructor.InvokeAsync(args, Span, context) : constructor.Invoke(args, Span, context);
    }

    private static object Get(object target, string name)
    {
        Assert.True(Assert.IsAssignableFrom<IPyDynamicAttributes>(target).TryGetMember(name, out var value));
        return value;
    }

    private static async ValueTask<object> Call(object target, string name, LythonRuntime.ExecutionContext context,
        bool asynchronous, params object[] values)
    {
        var method = Assert.IsAssignableFrom<LythonRuntime.ICallable>(Get(target, name));
        var args = values.Select(CallArgumentValue.Positional).ToArray();
        return asynchronous ? await method.InvokeAsync(args, Span, context) : method.Invoke(args, Span, context);
    }

    private static long Snapshot(object value)
    {
        Assert.True(Assert.IsAssignableFrom<IPyOwnershipSnapshot>(value).TrySnapshotOwnership(out var bytes));
        return bytes;
    }
}
