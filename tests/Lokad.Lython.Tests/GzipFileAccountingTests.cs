using System.Numerics;
using System.Runtime.CompilerServices;
using System.IO.Compression;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
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
        Assert.Equal(false, Assert.IsAssignableFrom<IExecutionFileWriter>(reader).Exit(context));
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
        var cleanup = Assert.IsAssignableFrom<IExecutionFileWriter>(wrapper);
        if (mode == "rb") Assert.Equal(false, await cleanup.ExitAsync(context));
        else await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await cleanup.ExitAsync(context));
        Assert.Equal(512, Snapshot(wrapper));
        Assert.Equal(true, Get(wrapper, "closed"));
        Assert.True(raw.TryGetMember("closed", out var closed));
        Assert.Equal(false, closed);
        GC.KeepAlive(raw); GC.KeepAlive(wrapper);
    }

    private static LythonRuntime.ExecutionContext Context(long? cap = null)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbandonmentDetachesCursorAndPreservesEscapedInput(bool asynchronous)
    {
        var context = Context(2_000_000);
        var raw = Raw(context, Member);
        var owner = await Construct(context, raw, "rb", asynchronous);
        var escaped = await Call(owner, "read", context, asynchronous, BigInteger.One);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cursorField = owner.GetType().GetField("_cursor", flags)!;
        var cursor = cursorField.GetValue(owner)!;
        var input = Assert.IsType<PyBytes>(cursor.GetType().GetField("_input", flags)!.GetValue(cursor));
        Assert.IsAssignableFrom<IPyFinalizableOwnership>(owner).AbandonStorage();
        Assert.Null(cursorField.GetValue(owner));
        Assert.Equal(512, Snapshot(owner));
        GC.KeepAlive(escaped);
        GC.KeepAlive(input);
    }

    [Theory]
    [InlineData("rb", false)]
    [InlineData("rb", true)]
    [InlineData("wb", false)]
    [InlineData("wb", true)]
    public async Task AbandonmentAtFullCancelledBudgetReleasesOnlyLocalStorage(string mode, bool asynchronous)
    {
        const long cap = 2_000_000;
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MaxExecutionMemoryBytes = cap, CancellationToken = cancellation.Token });
        var raw = Raw(context, mode == "rb" ? Member : []);
        var owner = await Construct(context, raw, mode, asynchronous);
        PyBytes escaped;
        if (mode == "rb")
            escaped = Assert.IsType<PyBytes>(await Call(owner, "read", context, asynchronous, BigInteger.One));
        else
        {
            await Call(owner, "write", context, asynchronous, new PyBytes(new byte[32769]));
            escaped = raw.GetValue(Span);
        }
        var ownedBefore = Snapshot(owner);
        var rawBytes = raw.OwnedBytes;
        var rawLength = raw.Length;
        var rawPosition = raw.Position;
        var escapedBytes = escaped.CommittedStorageBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        using var pressure = context.MemoryGovernor.ReserveTemporary(cap - context.MemoryGovernor.CurrentAccountedBytes, Span);
        cancellation.Cancel();
        var finalizable = Assert.IsAssignableFrom<IPyFinalizableOwnership>(owner);
        finalizable.AbandonStorage();
        Assert.Equal(512, Snapshot(owner));
        Assert.Equal(committed - ownedBefore + 512, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(rawBytes, raw.OwnedBytes);
        Assert.Equal(rawLength, raw.Length);
        Assert.Equal(rawPosition, raw.Position);
        Assert.Equal(escapedBytes, escaped.CommittedStorageBytes);
        Assert.Equal(false, Get(raw, "closed"));
        Assert.Equal(true, Get(owner, "closed"));
        Assert.Same(PyNone.Instance, Get(owner, "fileobj"));
        var after = context.MemoryGovernor.CurrentCommittedBytes;
        finalizable.AbandonStorage();
        Assert.Equal(after, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(escaped);
    }

    [Fact]
    public async Task AbandonmentDisposalFaultStillReleasesBuffersAndCanBeRepeated()
    {
        var context = Context(2_000_000);
        var raw = Raw(context, []);
        var owner = await Construct(context, raw, "wb", false);
        await Call(owner, "write", context, false, new PyBytes(new byte[32769]));
        var native = ReplaceNativeWithThrowingDisposer(owner);
        var owned = Snapshot(owner);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var finalizable = Assert.IsAssignableFrom<IPyFinalizableOwnership>(owner);
        Assert.Same(native.Failure, Assert.Throws<IOException>(finalizable.AbandonStorage));
        Assert.Equal(512, Snapshot(owner));
        Assert.Equal(committed - owned + 512, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(true, Get(owner, "closed"));
        Assert.Equal(false, Get(raw, "closed"));
        finalizable.AbandonStorage();
        Assert.Equal(1, native.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalCloseDisposalFaultStillClosesTheOwnedBaseStream(bool asynchronous)
    {
        var context = Context(2_000_000);
        Assert.True(LythonRuntime.GzipModule.Instance.TryGetMember("GzipFile", out var member));
        var constructor = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var owner = constructor.Invoke([CallArgumentValue.Positional(PyString.FromString("/file")),
            CallArgumentValue.Positional(PyString.FromString("wb"))], Span, context);
        var raw = Get(owner, "fileobj");
        await Call(owner, "write", context, asynchronous, new PyBytes([1, 2, 3]));
        var native = ReplaceNativeWithThrowingDisposer(owner);
        var error = await Assert.ThrowsAsync<IOException>(async () => await Call(owner, "close", context, asynchronous));
        Assert.Same(native.Failure, error);
        Assert.Equal(512, Snapshot(owner));
        Assert.Equal(true, Get(raw, "closed"));
        context.State.CloseOpenFileWriters(context); // No terminal writer remains registered.
        Assert.Equal(2, native.DisposeCount);
    }

    private static ThrowingDeflateStream ReplaceNativeWithThrowingDisposer(object owner)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var ownerType = owner.GetType();
        var sink = ownerType.GetField("_sink", flags)!.GetValue(owner)!;
        sink.GetType().GetMethod("Discard", flags)!.Invoke(sink, null);
        var field = ownerType.GetField("_native", flags)!;
        ((DeflateStream)field.GetValue(owner)!).Dispose();
        var replacement = new ThrowingDeflateStream();
        field.SetValue(owner, replacement);
        return replacement;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveReadCallbackCannotAbandonItsOwner(bool asynchronous)
    {
        var context = Context(2_000_000);
        var source = new CallbackReadSource();
        var owner = await Construct(context, source, "rb", asynchronous);
        var guarded = 0;
        source.OnRead = () =>
        {
            var snapshot = Snapshot(owner);
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.Throws<InvalidOperationException>(Assert.IsAssignableFrom<IPyFinalizableOwnership>(owner).AbandonStorage);
            Assert.Equal(snapshot, Snapshot(owner));
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(false, Get(owner, "closed"));
            guarded++;
        };
        var result = Assert.IsType<PyBytes>(await Call(owner, "read", context, asynchronous, BigInteger.One));
        Assert.Equal(new byte[] { (byte)'a' }, result.Bytes.ToArray());
        Assert.True(guarded > 0);
        Assert.IsAssignableFrom<IPyFinalizableOwnership>(owner).AbandonStorage();
        Assert.Equal(512, Snapshot(owner));
        GC.KeepAlive(result);
    }

    private sealed class CallbackReadSource : IPyDynamicAttributes
    {
        internal Action OnRead = static () => { };
        private int _position;
        public bool TryGetMember(string name, out object value)
        {
            if (name == "read") { value = new ReadMethod(this); return true; }
            value = null!;
            return false;
        }

        private sealed class ReadMethod(CallbackReadSource source) : LythonRuntime.ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            {
                source.OnRead();
                var count = Math.Min((int)(BigInteger)arguments[0].Value, Member.Length - source._position);
                var bytes = Member.AsSpan(source._position, count).ToArray();
                source._position += count;
                return new PyBytes(bytes);
            }
            public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            {
                await Task.Yield();
                return Invoke(arguments, span, context);
            }
        }
    }

    private sealed class ThrowingDeflateStream() : DeflateStream(new MemoryStream(), CompressionLevel.Fastest)
    {
        internal IOException Failure { get; } = new("Controlled disposal failure after resource release.");
        internal int DisposeCount { get; private set; }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { DisposeCount++; throw Failure; }
        }
    }

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
