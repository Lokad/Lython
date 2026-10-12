using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

public sealed class TextFileOwnerStorageTests
{
    private const long Cap = 262144;

    [Theory]
    [InlineData("r")]
    [InlineData("w")]
    [InlineData("a")]
    public void DroppedClosedShellReclaimsButRetainedClosedShellStaysFunded(string mode)
    {
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = Cap });
        var kept = Open(mode, context); kept.Exit();
        var dropped = Drop(mode, context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));
        var expected = 64 + ChargeReclamationPool.EntryChargeBytes + (mode == "r"
            ? LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes + ChargeReclamationPool.EntryChargeBytes : 0);
        Assert.Equal(expected, context.State.CallTemporaries.Sweep(full: true));
        Assert.Equal(before - expected, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(context.State.CallTemporaries.IsTracked(kept));
        Assert.True(kept.IsClosed);
        Assert.True(((IPyOwnershipSnapshot)kept).TrySnapshotOwnership(out var shell));
        Assert.Equal(64, shell);
        GC.KeepAlive(kept);
    }

    [Theory]
    [InlineData("w")]
    [InlineData("a")]
    public async Task DeniedRegistrationRefundsUnpublishedShellWithoutRegisteringAWriter(string mode)
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = Cap });
        var retained = Handle.ForWrite("/retained", context); retained.Exit();
        var governor = context.MemoryGovernor;
        var committed = governor.CurrentCommittedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - 64 - (ChargeReclamationPool.EntryChargeBytes - 1);
        using var reservation = governor.ReserveTemporary(pressure, null);
        var error = Assert.Throws<LythonRuntimeException>(() => mode == "w" ? Handle.ForWrite("/denied", context)
            : Handle.ForAppend("/denied", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        context.State.CloseOpenFileWriters();
        Assert.False(await host.ExistsAsync("/denied", CancellationToken.None));
        Assert.True(governor.PeakAccountedBytes <= Cap);
        GC.KeepAlive(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseReturnsReadWindowOnceAndKeepsEscapedLineFunded(bool asynchronous)
    {
        var expected = new string('x', 200) + " café 😀\n";
        var mock = asynchronous ? null : new MockLythonHost();
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        if (delayed is not null) delayed.SeedFile("/file", expected + new string('y', 32768));
        else mock!.SeedFile("/file", expected + new string('y', 32768));
        var context = new LythonRuntime.ExecutionContext((ILythonHost?)delayed ?? mock!, new LythonRunOptions { MaxExecutionMemoryBytes = Cap });
        var handle = asynchronous ? await Handle.ForReadAsync("/file", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal) : Open("r", context);
        var line = asynchronous ? await handle.ReadLineAsync(-1) : handle.ReadLine();
        Assert.Equal(expected, line.AsString());
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        if (asynchronous) await handle.ExitAsync(); else handle.Exit();
        var closed = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(closed < before);
        Assert.True(handle.IsClosed);
        Assert.Contains(context.State.LiveReclamationPools(), pool => pool.IsTracked(line));
        Assert.True(line.CommittedOwnedBytes > 0);
        if (asynchronous) { await handle.ExitAsync(); Assert.True(delayed!.CompletedAsynchronously > 0); }
        else handle.Exit();
        Assert.Equal(closed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal("ValueError", Assert.Throws<LythonRuntimeException>(() => handle.ReadLine()).ExceptionType);
        Assert.Equal(expected, line.AsString());
        GC.KeepAlive(line);
        GC.KeepAlive(handle);
    }

    [Fact]
    public async Task AsyncAppendRegistersItsShellBeforePublicationAndCloseIsIdempotent()
    {
        var host = new DelayedLythonHost(); host.SeedFile("/file", "before");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = Cap });
        var handle = await Handle.ForAppendAsync("/file", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
        Assert.True(context.State.CallTemporaries.IsTracked(handle));
        handle.Write(PyString.FromString("after"));
        await handle.ExitAsync();
        var closed = context.MemoryGovernor.CurrentCommittedBytes;
        await handle.ExitAsync();
        Assert.Equal(closed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal("beforeafter", host.ReadText("/file"));
        Assert.True(host.CompletedAsynchronously > 0);
        GC.KeepAlive(handle);
    }

    private static Handle Open(string mode, LythonRuntime.ExecutionContext context) => mode switch
    {
        "r" => Handle.ForRead("/file", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal),
        "a" => Handle.ForAppend("/file", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal),
        _ => Handle.ForWrite("/file", context),
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Handle> Drop(string mode, LythonRuntime.ExecutionContext context)
    {
        var handle = Open(mode, context); handle.Exit(); return new(handle);
    }
}
