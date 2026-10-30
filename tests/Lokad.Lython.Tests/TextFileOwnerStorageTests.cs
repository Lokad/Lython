using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextFileOwnerStorageTests
{
    private const long Cap = 262144;

    [Theory]
    [InlineData("r")]
    [InlineData("w")]
    [InlineData("a")]
    public void ClosedShellsRemainBatchOwnedWhileRetainedHandlesStayUsable(string mode)
    {
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var kept = Open(mode, context); kept.Exit();
        var dropped = Drop(mode, context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));
        var expected = 64 + (mode == "r"
            ? LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes : 0);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(before >= expected);
        Assert.True(kept.IsClosed);
        GC.KeepAlive(kept);
    }

    [Theory]
    [InlineData("w")]
    [InlineData("a")]
    public async Task FailedWriterOpenReturnsItsUnpublishedShell(string mode)
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var utf16 in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.FailFileWrite("/denied", "denied");
            var context = new LythonRuntime.ExecutionContext(host, null, regexProvider: Utf8RegexProvider.Instance);
            var codec = utf16 ? LythonRuntime.TextEncodingMode.Utf16 : LythonRuntime.TextEncodingMode.Utf8;
            await Assert.ThrowsAsync<HostOperationException>(async () =>
            {
                if (asynchronous)
                {
                    if (mode == "w") await Handle.ForWriteAsync("/denied", context, codec,
                        LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
                    else await Handle.ForAppendAsync("/denied", context, codec,
                        LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
                }
                else if (mode == "w") Handle.ForWrite("/denied", context, codec,
                    LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
                else Handle.ForAppend("/denied", context, codec,
                    LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
            });
            Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            host.ClearFileWriteFailures();
            context.State.CloseOpenFiles(context);
            Assert.False(host.Exists("/denied"));
        }
    }

    [Theory]
    [InlineData("w")]
    [InlineData("a")]
    public async Task DeniedShellAdmissionPublishesNoWriterOrFile(string mode)
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var retained = Handle.ForWrite("/retained", context); retained.Exit();
        var governor = context.MemoryGovernor;
        var committed = governor.CurrentCommittedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - 1;
        using var reservation = governor.ReserveTemporary(pressure, null);
        var error = Assert.Throws<LythonRuntimeException>(() => mode == "w" ? Handle.ForWrite("/denied", context)
            : Handle.ForAppend("/denied", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        context.State.CloseOpenFiles(context);
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
        var context = new LythonRuntime.ExecutionContext((ILythonHost?)delayed ?? mock!, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var handle = asynchronous ? await Handle.ForReadAsync("/file", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal) : Open("r", context);
        var line = asynchronous ? await handle.ReadLineAsync(-1) : handle.ReadLine();
        Assert.Equal(expected, line.AsString());
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        if (asynchronous) await handle.ExitAsync(); else handle.Exit();
        var closed = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(closed < before);
        Assert.True(handle.IsClosed);
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
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
        var handle = await Handle.ForAppendAsync("/file", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
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
