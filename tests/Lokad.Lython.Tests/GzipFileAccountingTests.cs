using System.Numerics;
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
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 512 + raw.OwnedBytes);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var once = context.MemoryGovernor.CurrentCommittedBytes;
        await Call(writer, "close", context, asynchronous);
        Assert.Equal(once, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.IsType<PyNone>(Get(writer, "fileobj"));
        GC.KeepAlive(raw); GC.KeepAlive(writer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecutionCleanupRetiresReaderBeforeEndingItsOwner(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2000000);
        using var owner = budget.CreateScope();
        var context = Context(null, owner);
        var raw = Raw(context, Member);
        var reader = await Construct(context, raw, "rb", asynchronous);
        var output = Assert.IsType<PyBytes>(await Call(reader, "read", context, asynchronous, BigInteger.One));
        var funded = budget.ReservedBytes;
        var owned = Snapshot(reader);
        Assert.True(owned >= LythonRuntime.ZlibModule.InflationScratchBytes);
        if (asynchronous) await context.State.CloseOpenFileWritersAsync(context);
        else context.State.CloseOpenFileWriters(context);
        Assert.Equal(true, Get(reader, "closed"));
        Assert.Equal(false, Get(raw, "closed"));
        Assert.Equal(512, Snapshot(reader));
        Assert.True(budget.ReservedBytes < funded);
        Assert.Equal(new byte[] { (byte)'a' }, output.Bytes.ToArray());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(raw); GC.KeepAlive(reader); GC.KeepAlive(output);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(80000)]
    [InlineData(131500)]
    [InlineData(140000)]
    public async Task RepeatedInflationDenialsLeaveNoReservationsAndCloseReleasesOwnership(long cap)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var context = Context(cap);
            var raw = Raw(context, Member);
            raw.Seek(0, 0, Span);
            var reader = await Construct(context, raw, "rb", false);
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Call(reader, "read", context, false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            await ((IExecutionFileWriter)reader).ExitAsync(context);
            Assert.Equal(512, Snapshot(reader));
            GC.KeepAlive(reader);
            GC.KeepAlive(raw);
        }
    }

    [Theory]
    [InlineData(524000)]
    [InlineData(525000)]
    public async Task ConstructorDenialRollsBackTheUnpublishedWrapper(long cap)
    {
        var context = Context(cap);
        var raw = Raw(context, []);
        long? admittedSetup = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Construct(context, raw, "wb", false));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            // Setup may retain the borrowed stream's name/dispatch metadata.
            // A refused wrapper/compressor must not strand another shell on retry.
            admittedSetup ??= context.MemoryGovernor.CurrentCommittedBytes;
            Assert.Equal(admittedSetup.Value, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.True(admittedSetup.Value >= raw.OwnedBytes);
        }
        GC.KeepAlive(raw);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyDeflateBlocksProduceNoOutputAndReleaseWorkingStorage(bool asynchronous)
    {
        // A valid member containing 10,000 empty stored blocks. Output-size
        // checks alone cannot govern this input; the compressed cursor must.
        var member = new byte[10 + 5 * 10000 + 2 + 8];
        Member.AsSpan(0, 10).CopyTo(member);
        for (var offset = 10; offset < 50010; offset += 5)
        { member[offset + 3] = 255; member[offset + 4] = 255; }
        member[50010] = 3;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { },regexProvider:Utf8RegexProvider.Instance);
        var raw = Raw(context, member);
        var reader = await Construct(context, raw, "rb", asynchronous);
        var value = Assert.IsType<PyBytes>(await Call(reader, "read", context, asynchronous));
        Assert.Equal(0, value.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // Runtime cleanup releases the decoder while preserving the returned payload.
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
            new LythonRunOptions { CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
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

    private static LythonRuntime.ExecutionContext Context(long? cap = null, ILythonMemoryBudget? budget = null)
        => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget ?? new LythonMemoryBudget(cap) },regexProvider:Utf8RegexProvider.Instance);

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
