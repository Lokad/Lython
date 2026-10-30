using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OwnedByteFileTests
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task TextDirectionAndClosedContextEntryUsePythonErrors(bool asynchronous)
    {
        var host = new MockLythonHost(); host.SeedFile("/input", "x");
        using var script = new LythonEngine().Compile("""
            for mode in ('r','w'):
                f=open('input',mode)
                try:
                    if mode=='r':f.write('x')
                    else:f.read()
                except Exception as error:print(type(error).__name__,isinstance(error,OSError),isinstance(error,ValueError))
                f.close()
                try:
                    with f:print('body')
                except Exception as error:print(type(error).__name__)
            """);
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("UnsupportedOperation True True\nValueError\nUnsupportedOperation True True\nValueError\n", result.StandardOutput);
        Assert.Equal(2, host.OpenFileCount); Assert.Equal(host.OpenFileCount, host.ClosedFileCount);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task InterleavedAppendUsesActualPublicationPosition(bool asynchronous)
    {
        var host = new MockLythonHost(); host.SeedBytes("/target", "s"u8.ToArray());
        using var script = new LythonEngine().Compile("""
            with open('target','ab') as first, open('target','ab') as second:
                first.write(b'A'); second.write(b'B')
                second.flush(); first.flush()
                print(first.tell(),second.tell())
            """);
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3 2\n", result.StandardOutput);
        Assert.Equal("sBA"u8.ToArray(), host.ReadBytes("/target"));
        Assert.Equal(host.OpenFileCount, host.ClosedFileCount);
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task OpenedIdentitiesAndIndependentCursorsSurvivePathReplacement(bool asynchronous, bool text)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/source", "abcdef"u8.ToArray());
        host.SeedBytes("/target", "old"u8.ToArray());
        var changed = false;
        host.OnStandardOutputWrite = () =>
        {
            if (changed) return;
            changed = true;
            host.Move("/source", "/moved-source"); host.SeedBytes("/source", "replacement"u8.ToArray());
            host.Move("/target", "/moved-target"); host.SeedBytes("/target", "replacement"u8.ToArray());
        };
        var source = """
            with open('source','rb') as a, open('source','rb') as b, open('target','wb') as out:
                print('ready',end='')
                print(a.read(2).decode(),b.read().decode(),a.read().decode())
                out.write(b'kept')
            """;
        if (text) source = source.Replace("'rb'", "'r'").Replace("'wb'", "'w'").Replace(".decode()", "").Replace("b'kept'", "'kept'");
        using var script = new LythonEngine().Compile(source);
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("readyab abcdef cdef\n", result.StandardOutput);
        Assert.Equal("kept"u8.ToArray(), host.ReadBytes("/moved-target"));
        Assert.Equal("replacement"u8.ToArray(), host.ReadBytes("/target"));
        Assert.Equal(3, host.OpenFileCount); Assert.Equal(host.OpenFileCount, host.ClosedFileCount);
    }

    [Theory]
    [InlineData("source/../other", "NotADirectoryError")]
    [InlineData("source/", "NotADirectoryError")]
    [InlineData("missing/../source", "FileNotFoundError")]
    [InlineData("", "FileNotFoundError")]
    public async Task HostSeesUnresolvedPathIntent(string path, string error)
    {
        var host = new FileHost(); host.Inner.SeedBytes("/source", [1]);
        using var result = await new LythonEngine().RunAsync("open('" + path + "','rb')", host);
        Assert.False(result.Success); Assert.Equal(error, result.Failure?.ExceptionType);
        Assert.Equal(new LythonPathRequest(path, "/"), host.LastRequest);
        Assert.Equal(0, host.Inner.OpenFileCount);
    }

    [Theory]
    [InlineData(false, "zero"), InlineData(true, "zero")]
    [InlineData(false, "partial"), InlineData(true, "partial")]
    [InlineData(false, "partial-zero"), InlineData(true, "partial-zero")]
    [InlineData(false, "unknown"), InlineData(true, "unknown")]
    [InlineData(false, "short"), InlineData(true, "short")]
    [InlineData(false, "flush"), InlineData(true, "flush")]
    public async Task AcceptedPrefixesNeverReplayAndKnownZeroCanRetry(bool asynchronous, string failureMode)
    {
        var host = new FileHost();
        host.Write = async (file, buffer, token) =>
        {
            if (failureMode == "short") return await file.WriteAsync(buffer[..Math.Min(2, buffer.Length)], token);
            if (failureMode == "partial-zero")
                return host.WriteCalls == 1 ? await file.WriteAsync(buffer[..3], token)
                    : new LythonFileWriteResult(0, new LythonHostException(LythonHostFailureKind.Transport, "later write failed"));
            if (host.WriteCalls != 1 || failureMode == "flush") return await file.WriteAsync(buffer, token);
            var prefix = failureMode == "partial" ? 3 : failureMode == "unknown" ? 2 : 0;
            if (prefix != 0) await file.WriteAsync(buffer[..prefix], token);
            return new LythonFileWriteResult(failureMode == "unknown" ? null : prefix,
                new LythonHostException(LythonHostFailureKind.Transport, "publication failed"));
        };
        host.Flush = (file, token) => failureMode == "flush" && host.FlushCalls == 1
            ? ValueTask.FromException(new LythonHostException(LythonHostFailureKind.Transport, "flush failed")) : file.FlushAsync(token);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            f=open('target','wb')
            f.write(b'abcdef')
            try:f.flush()
            except OSError:print(f.closed)
            if not f.closed:f.flush()
            f.close()
            """, budget))
        {
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(failureMode is "partial" or "partial-zero" or "unknown" ? "True\n" : failureMode == "short" ? "" : "False\n", result.StandardOutput);
                Assert.Equal(failureMode is "partial" or "partial-zero" ? "abc"u8.ToArray() : failureMode == "unknown" ? "ab"u8.ToArray() : "abcdef"u8.ToArray(), host.Inner.ReadBytes("/target"));
                Assert.Equal(failureMode is "zero" or "partial-zero" ? 2 : failureMode == "short" ? 3 : 1, host.WriteCalls);
                Assert.Equal(1, host.Inner.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task RefusedAcquisitionPrecedesTruncationAndGuestContinuation(bool asynchronous)
    {
        var budget = new AdmissionGateBudget();
        var host = new FileHost { BeforeOpen = () => budget.Gate(0) };
        host.Inner.SeedBytes("/target", "before"u8.ToArray());
        using var script = new LythonEngine().Compile("try:open('target','wb')\nexcept Exception:print('continued')");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success); Assert.Empty(result.StandardOutput);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("before"u8.ToArray(), host.Inner.ReadBytes("/target"));
            Assert.Equal(0, host.Inner.OpenFileCount);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task RefusedWriteAdmissionIsTerminalAndCleanupCannotMaskIt(bool asynchronous)
    {
        var host = new FileHost();
        host.Write = (_, _, _) => ValueTask.FromResult(new LythonFileWriteResult(0,
            new LythonHostException(LythonHostFailureKind.MemoryAdmission, "first file refusal", requestedBytes: 12345)));
        host.Dispose = async file =>
        {
            await file.DisposeAsync();
            throw new LythonHostException(LythonHostFailureKind.Transport, "secondary close failure");
        };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            try:
                f=open('target','wb')
                f.write(b'never')
                try:f.flush()
                except Exception:print('continued')
            finally:return 7
            """, budget))
        {
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.False(result.Success); Assert.Empty(result.StandardOutput);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Contains("first file refusal", result.Failure?.Message);
                Assert.Empty(host.Inner.ReadBytes("/target"));
                Assert.Equal(1, host.WriteCalls); Assert.Equal(1, host.Inner.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task CancellationWaitsForActualReadAndUncancelledCloseBeforeRefund(bool text)
    {
        var entered = Signal(); var readRelease = Signal(); var closing = Signal(); var closeRelease = Signal();
        using var cancellation = new CancellationTokenSource();
        var host = new FileHost { CompletesSynchronously = false };
        host.Inner.SeedBytes("/source", [1]);
        host.Read = async (_, buffer, token) =>
        {
            Assert.Equal(cancellation.Token, token); entered.TrySetResult();
            await readRelease.Task;
            Assert.True(token.IsCancellationRequested);
            buffer.Span[0] = 42; // Deliberately finish the original borrow after cancellation.
            return 1;
        };
        host.Dispose = async file =>
        {
            closing.TrySetResult(); await closeRelease.Task; await file.DisposeAsync();
        };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("f=open('source','" + (text ? "r" : "rb") + "'); return f.read(1)", budget);
        var baseline = budget.ReservedBytes;
        var run = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > baseline + 256);
            readRelease.TrySetResult(); await closing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > baseline + 256);
            Assert.Equal(0, host.Inner.ClosedFileCount);
            closeRelease.TrySetResult();
            using (var result = await run) { Assert.False(result.Success); Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason); }
            Assert.Equal(1, host.Inner.ClosedFileCount); Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally { readRelease.TrySetResult(); closeRelease.TrySetResult(); using var drained = await run; }
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task UnpublishedOpenIsClosedAfterItsActualCompletion(bool falseSynchronousPromise, bool text)
    {
        var entered = Signal(); var release = Signal(); var closing = Signal(); var closeRelease = Signal();
        using var cancellation = new CancellationTokenSource();
        var host = new FileHost { CompletesSynchronously = falseSynchronousPromise };
        var observedPending = Signal();
        if (falseSynchronousPromise) host.OpenStatusObserved = observedPending;
        host.Inner.SeedBytes("/source", [1]);
        host.AfterOpen = async () => { entered.TrySetResult(); await release.Task; };
        host.Dispose = async file => { closing.TrySetResult(); await closeRelease.Task; await file.DisposeAsync(); };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("return open('source','" + (text ? "r" : "rb") + "').read()", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };
        var run = falseSynchronousPromise ? Task.Run(() => script.Run(host, options)) : script.RunAsync(host, options);
        try
        {
            await (falseSynchronousPromise ? observedPending.Task : entered.Task).WaitAsync(TimeSpan.FromSeconds(10));
            if (!falseSynchronousPromise) cancellation.Cancel();
            Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > baseline);
            release.TrySetResult(); await closing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > baseline);
            closeRelease.TrySetResult();
            using (var result = await run)
            {
                Assert.False(result.Success);
                Assert.Equal(falseSynchronousPromise ? LythonFailureReason.HostOperationFailed : LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            Assert.Equal(1, host.Inner.ClosedFileCount); Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally { release.TrySetResult(); closeRelease.TrySetResult(); using var drained = await run; }
    }

    [Theory]
    [InlineData(false, "utf-8", false), InlineData(true, "utf-8", false)]
    [InlineData(false, "utf-8-sig", false), InlineData(true, "utf-8-sig", false)]
    [InlineData(false, "utf-16", false), InlineData(true, "utf-16", false)]
    [InlineData(false, "utf-8", true), InlineData(true, "utf-8", true)]
    [InlineData(false, "utf-8-sig", true), InlineData(true, "utf-8-sig", true)]
    [InlineData(false, "utf-16", true), InlineData(true, "utf-16", true)]
    public async Task EncodedPrefixesDoNotReplayAndZeroAcceptancePreservesBomRetry(bool asynchronous, string encoding, bool zero)
    {
        var expected = Convert.FromHexString(encoding switch
        {
            "utf-8" => "f09f988078",
            "utf-8-sig" => "efbbbff09f988078",
            _ => "fffe3dd800de7800",
        });
        var host = new FileHost();
        host.Write = async (file, buffer, token) =>
        {
            if (host.WriteCalls != 1) return await file.WriteAsync(buffer, token);
            if (!zero) await file.WriteAsync(buffer[..2], token);
            return new LythonFileWriteResult(zero ? 0 : 2, new LythonHostException(LythonHostFailureKind.Transport, "encoded write failed"));
        };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("f=open('target','w',encoding='" + encoding + "')\nf.write('😀x')\ntry:f.flush()\nexcept OSError:print(f.closed)\nif not f.closed:f.flush()\nf.close()", budget))
        {
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(zero ? "False\n" : "True\n", result.StandardOutput);
                Assert.Equal(zero ? expected : expected[..2], host.Inner.ReadBytes("/target"));
                Assert.Equal(zero ? 2 : 1, host.WriteCalls); Assert.Equal(1, host.Inner.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData(false, "utf-8"), InlineData(true, "utf-8")]
    [InlineData(false, "utf-16"), InlineData(true, "utf-16")]
    public async Task BackendFlushFailureCannotReplayAcceptedTextOrBom(bool asynchronous, string encoding)
    {
        var host = new FileHost();
        host.Flush = (file, token) => host.FlushCalls == 1
            ? ValueTask.FromException(new LythonHostException(LythonHostFailureKind.Transport, "flush failed")) : file.FlushAsync(token);
        using var script = new LythonEngine().Compile("f=open('target','w',encoding='" + encoding + "')\nf.write('x')\ntry:f.flush()\nexcept OSError:pass\nf.write('y');f.close()");
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(Convert.FromHexString(encoding == "utf-8" ? "7879" : "fffe78007900"), host.Inner.ReadBytes("/target"));
        Assert.Equal(2, host.WriteCalls); Assert.Equal(1, host.Inner.ClosedFileCount);
    }

    private sealed class FileHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public MockLythonHost Inner { get; } = new();
        public bool CompletesSynchronously { get; init; } = true;
        public string Cwd => Inner.Cwd;
        public DateTimeOffset LocalNow => Inner.LocalNow;
        public DateTimeOffset UtcNow => Inner.UtcNow;
        public LythonPathRequest? LastRequest;
        public int WriteCalls, FlushCalls;
        public Func<ILythonByteFile, Memory<byte>, CancellationToken, ValueTask<int>>? Read;
        public Func<ILythonByteFile, ReadOnlyMemory<byte>, CancellationToken, ValueTask<LythonFileWriteResult>>? Write;
        public Func<ILythonByteFile, CancellationToken, ValueTask>? Flush;
        public Func<ILythonByteFile, ValueTask>? Dispose;
        public Func<Task>? AfterOpen;
        public TaskCompletionSource? OpenStatusObserved;
        public Action? BeforeOpen;
        public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options, ILythonMemoryBudget budget, CancellationToken token)
        {
            var open = OpenCoreAsync(path, options, budget, token);
            return OpenStatusObserved is { } observed
                ? new ObservedValueTask<ILythonByteFile>(open.AsTask(), observed).Value
                : open;
        }
        private async ValueTask<ILythonByteFile> OpenCoreAsync(LythonPathRequest path, LythonFileOpenOptions options, ILythonMemoryBudget budget, CancellationToken token)
        {
            LastRequest = path;
            BeforeOpen?.Invoke();
            var file = new File(this, await Inner.OpenFileAsync(path, options, budget, token));
            if (AfterOpen is not null) await AfterOpen();
            return file;
        }
        private sealed class File(FileHost host, ILythonByteFile file) : ILythonByteFile, ILythonSynchronousHostCapability
        {
            public bool CompletesSynchronously => host.CompletesSynchronously;
            public long Position => file.Position;
            public long? LengthHint => file.LengthHint;
            public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => host.Read?.Invoke(file, buffer, token) ?? file.ReadAsync(buffer, token);
            public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
            {
                host.WriteCalls++; return host.Write?.Invoke(file, buffer, token) ?? file.WriteAsync(buffer, token);
            }
            public ValueTask FlushAsync(CancellationToken token) { host.FlushCalls++; return host.Flush?.Invoke(file, token) ?? file.FlushAsync(token); }
            public ValueTask DisposeAsync() => host.Dispose?.Invoke(file) ?? file.DisposeAsync();
        }
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => Inner.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => Inner.ListDirAsync(path, token);
        public ValueTask MkDirAsync(string path, CancellationToken token) => Inner.MkDirAsync(path, token);
        public ValueTask RemoveAsync(string path, CancellationToken token) => Inner.RemoveAsync(path, token);
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => Inner.CopyAsync(source, destination, token);
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => Inner.MoveAsync(source, destination, token);
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => Inner.StatAsync(path, token);
    }
}
