using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StructuredHostFailureTests
{
    [Theory]
    [InlineData(LythonHostFailureKind.NotFound, "FileNotFoundError")]
    [InlineData(LythonHostFailureKind.PermissionDenied, "PermissionError")]
    [InlineData(LythonHostFailureKind.IsDirectory, "IsADirectoryError")]
    [InlineData(LythonHostFailureKind.NotDirectory, "NotADirectoryError")]
    [InlineData(LythonHostFailureKind.AlreadyExists, "FileExistsError")]
    [InlineData(LythonHostFailureKind.UnsupportedOperation, "NotImplementedError")]
    [InlineData(LythonHostFailureKind.BrokenPipe, "BrokenPipeError")]
    [InlineData(LythonHostFailureKind.Transport, "OSError")]
    public async Task TypedFailuresHaveCatchablePythonCategoriesAndSafeMessages(LythonHostFailureKind kind, string expected)
    {
        using var script = new LythonEngine().Compile("""
            import os
            try:os.listdir('/')
            except Exception as error:return type(error).__name__+':'+str(error)
            """);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new FailureHost(kind, asynchronous);
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected + ":contained failure", result.ReturnValue);
        }
    }

    [Theory]
    [InlineData(LythonHostFailureKind.PermissionDenied, "PermissionError")]
    [InlineData(LythonHostFailureKind.Transport, "OSError")]
    public async Task OsPathPredicatesSuppressFilesystemErrorsWhilePathlibPreservesThem(LythonHostFailureKind kind, string expected)
    {
        using var script = new LythonEngine().Compile("""
            import os
            from pathlib import Path
            values=[os.path.exists('/file'),os.path.isfile('/file'),os.path.isdir('/file')]
            try:Path('/file').is_file()
            except OSError as error:values.append(type(error).__name__)
            return values
            """);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new FailureHost(kind, true)) : script.Run(new FailureHost(kind));
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new object?[] { false, false, false, expected }, Assert.IsType<List<object?>>(result.ReturnValue));
        }
    }

    [Fact]
    public async Task PathPredicatesNeverSuppressMemoryAdmissionRefusal()
    {
        using var script = new LythonEngine().Compile("""
            import os
            try:return os.path.exists('/file')
            except MemoryError:return 'continued'
            finally:return 'suppressed'
            """);
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(new FailureHost(LythonHostFailureKind.MemoryAdmission, true), options)
                : script.Run(new FailureHost(LythonHostFailureKind.MemoryAdmission), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Contains("contained failure", result.Failure?.Message);
                Assert.Null(result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("import os;os.stat('/file')")]
    [InlineData("print('payload')")]
    [InlineData("import time;time.sleep(0)")]
    [InlineData("import time;time.monotonic()")]
    [InlineData("import time;time.time()")]
    [InlineData("import subprocess;subprocess.run(['missing'])")]
    public async Task FilesStreamsClocksAndProcessLaunchShareTypedFailureTranslation(string source)
    {
        using var script = new LythonEngine().Compile(source);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new FailureHost(LythonHostFailureKind.BrokenPipe, asynchronous);
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Equal("BrokenPipeError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
            Assert.Equal("contained failure", result.Failure?.Message);
        }
    }

    [Fact]
    public async Task ACompletedProcessExitIsSeparateFromDirectLaunchFailure()
    {
        using var script = new LythonEngine().Compile("import subprocess\nreturn subprocess.run(['command']).returncode");
        foreach (var asynchronous in new[] { false, true })
        {
            using (var completed = asynchronous ? await script.RunAsync(new FailureHost(null, asynchronous)) : script.Run(new FailureHost(null)))
            {
                Assert.True(completed.Success, completed.Failure?.Message);
                Assert.Equal("7", completed.ReturnValue?.ToString());
            }
            using var failed = asynchronous ? await script.RunAsync(new FailureHost(LythonHostFailureKind.NotFound, asynchronous)) : script.Run(new FailureHost(LythonHostFailureKind.NotFound));
            Assert.Equal("FileNotFoundError", failed.Failure?.ExceptionType);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedRefusalRemainsTerminalAndAwaitsTheActualHostUser(bool falseSynchronousPromise)
    {
        using var script = new LythonEngine().Compile("""
            import subprocess
            try:
             try:subprocess.run(['command'])
             except BaseException:pass
            finally:return 'escaped'
            """);
        var budget = new LythonMemoryBudget(512000);
        var host = new FailureHost(LythonHostFailureKind.MemoryAdmission, true) { Hold = true, FalseSynchronousPromise = falseSynchronousPromise };
        var options = new LythonRunOptions { MemoryBudget = budget };
        var pending = falseSynchronousPromise ? Task.Run(() => script.Run(host, options)) : script.RunAsync(host, options);
        try
        {
            await host.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.False(host.Cleaned);
            Assert.True(budget.ReservedBytes >= 256);
        }
        finally { host.Resume.TrySetResult(); }
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(1_000_000, result.FailureAttribution?.Requested);
            Assert.True(host.Cleaned);
            Assert.Equal("contained failure", result.Failure?.Message);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var recovered = new LythonEngine().Run("try:raise MemoryError('guest')\nexcept MemoryError:return 7", new MockLythonHost(), options))
            Assert.True(recovered.Success, recovered.Failure?.Message);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task BrokenPipeRetainsItsOrdinaryOSErrorHierarchy()
    {
        using var script = new LythonEngine().Compile("""
            import os
            assert issubclass(BrokenPipeError,OSError)
            try:os.listdir('/')
            except OSError as error:return isinstance(error,BrokenPipeError)
            """);
        using var result = await script.RunAsync(new FailureHost(LythonHostFailureKind.BrokenPipe, true));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
    }

    private sealed class FailureHost(LythonHostFailureKind? kind, bool asynchronous = false) : ILythonHost, ILythonSynchronousHostCapability, ILythonTiming, ILythonTextOutput, ILythonSubprocessRunner
    {
        internal bool Hold, FalseSynchronousPromise, Cleaned;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CompletesSynchronously => !asynchronous || FalseSynchronousPromise;
        public string Cwd => "/";
        public DateTimeOffset LocalNow => throw Failure();
        public DateTimeOffset UtcNow => throw Failure();
        public long MonotonicNanoseconds => throw Failure();
        public ILythonTiming Timing => this;
        public ILythonTextOutput StandardOutput => this;
        public ILythonSubprocessRunner SubprocessRunner => this;
        private LythonHostException Failure() => new(kind ?? LythonHostFailureKind.Transport, "contained failure", new IOException("private host cause"), kind == LythonHostFailureKind.MemoryAdmission ? 1_000_000 : null);
        private async ValueTask<T> Fail<T>() { if (asynchronous) await Task.Yield(); throw Failure(); }
        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => Fail<bool>();
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => Fail<IReadOnlyList<string>>();
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => Fail<LythonPathStat>();
        public async ValueTask MkDirAsync(string path, CancellationToken token) { await Fail<int>(); }
        public async ValueTask RemoveAsync(string path, CancellationToken token) { await Fail<int>(); }
        public async ValueTask CopyAsync(string source, string target, CancellationToken token) { await Fail<int>(); }
        public async ValueTask MoveAsync(string source, string target, CancellationToken token) { await Fail<int>(); }
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken token) { await Fail<int>(); }
        public async ValueTask WriteUtf8Async(ReadOnlyMemory<byte> bytes, CancellationToken token) { await Fail<int>(); }
        public ValueTask FlushAsync(CancellationToken token) => ValueTask.CompletedTask;
        public async ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken token)
        {
            using var child = request.MemoryBudget!.CreateScope();
            Assert.True(child.TryReserve(256, out _));
            try
            {
                Entered.TrySetResult();
                if (Hold) await Resume.Task;
                else if (asynchronous) await Task.Yield();
                if (kind == LythonHostFailureKind.MemoryAdmission) Assert.False(child.TryReserve(1_000_000, out _));
                if (kind is not null) throw Failure();
                token.ThrowIfCancellationRequested();
                return new LythonSubprocessResult(7, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, request.MemoryBudget!.CreateScope());
            }
            finally { Cleaned = true; }
        }
    }
}
