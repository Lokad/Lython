using System.IO.Compression;
using System.Buffers.Binary;
using Lokad.Lython.Benchmarks.Comparison;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PositionalZipTests
{
    [Fact]
    public async Task ComparisonProviderPreservesCursorIdentityAndOpenedExtent()
    {
        var directory = Directory.CreateTempSubdirectory(OwnedWorkerProcess.FixtureDirectoryPrefix).FullName;
        var budget = new LythonMemoryBudget(256 * 1024);
        try
        {
            var host = new ComparisonFileHost(new(new Dictionary<string, byte[]> { ["data"] = "abcdef"u8.ToArray() }, new Dictionary<string, byte[]>()), directory);
            host.Reset();
            await using (var file = await host.OpenFileAsync(new("data", "/"), new(LythonFileAccess.Read), budget, default))
            {
                var positional = Assert.IsAssignableFrom<ILythonPositionalByteFile>(file);
                var buffer = new byte[2];
                Assert.Equal(1, await file.ReadAsync(buffer.AsMemory(0, 1), default));
                Assert.Equal((byte)'a', buffer[0]);
                await host.MoveAsync(new("data", "/"), new("saved", "/"), default);
                await using (var replacement = await host.OpenFileAsync(new("data", "/"), new(LythonFileAccess.Write, create: true), budget, default))
                    await replacement.WriteAsync("new"u8.ToArray(), default);
                Assert.Equal(2, await positional.ReadAtAsync(4, buffer, default));
                Assert.Equal("ef"u8.ToArray(), buffer);
                Assert.Equal(1, file.Position);
                Assert.Equal(2, await file.ReadAsync(buffer, default));
                Assert.Equal("bc"u8.ToArray(), buffer);
                using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await positional.ReadAtAsync(0, buffer, cancellation.Token));
                Assert.Equal(3, file.Position);
                await using (var append = await host.OpenFileAsync(new("saved", "/"), new(LythonFileAccess.Write, append: true), budget, default))
                    await append.WriteAsync("more"u8.ToArray(), default);
                Assert.Equal(6, positional.ReadableLength);
                Assert.Equal(0, await positional.ReadAtAsync(6, buffer, default));
                Assert.Equal(3, file.Position);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        finally
        {
            OwnedWorkerProcess.ValidateFixtureDirectory(directory);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task ComparisonProviderRunsBoundedInspection(bool asynchronous)
    {
        var directory = Directory.CreateTempSubdirectory(OwnedWorkerProcess.FixtureDirectoryPrefix).FullName;
        var budget = new LythonMemoryBudget(256 * 1024);
        try
        {
            var host = new ComparisonFileHost(new(new Dictionary<string, byte[]> { ["t.zip"] = BuildArchive() }, new Dictionary<string, byte[]>()), directory);
            host.Reset();
            using var script = new LythonEngine().Compile("import zipfile\nwith zipfile.ZipFile('t.zip') as z:\n print(z.namelist())\n print(z.read('small'))");
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("['padding', 'small']\nb'hello'\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        finally
        {
            OwnedWorkerProcess.ValidateFixtureDirectory(directory);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task InspectionKeepsOnlyMetadataAndSelectedMember(bool asynchronous, bool replacePath)
    {
        var host = new ZipHost(BuildArchive());
        if (replacePath) host.FirstRead = () => host.Current = [1, 2, 3];
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile("""
            import zipfile
            z=zipfile.ZipFile('t.zip')
            print(z.namelist())
            member=z.open('small')
            print(z.read('small'))
            z.close()
            print(z.namelist())
            return member.read()
            """);
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("['padding', 'small']\nb'hello'\n['padding', 'small']\n", result.StandardOutput);
            Assert.Equal("hello"u8.ToArray(), Assert.IsType<byte[]>(result.ReturnValue));
            Assert.True(budget.ReservedBytes > 0);
            Assert.InRange(host.BytesRead, 1, 80000);
            Assert.Equal(0, host.SequentialReads);
            Assert.Equal(1, host.Opened);
            Assert.Equal(1, host.Closed);
            Assert.All(host.Files, file => Assert.Equal(0, file.Position));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task RecognitionUsesBoundedOpenedReads(bool asynchronous)
    {
        var host = new ZipHost(BuildArchive());
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile("import zipfile\nreturn zipfile.is_zipfile('t.zip')");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(true, result.ReturnValue);
            Assert.InRange(host.BytesRead, 1, 80000);
            Assert.Equal(0, host.SequentialReads);
        }
        Assert.Equal(1, host.Closed);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task Zip64DirectoryOffsetsStayWideWithoutAcquiringTheSparsePrefix(bool asynchronous)
    {
        const long bias = 1L << 32;
        var ordinary = BuildArchive();
        var end = ordinary.AsSpan(ordinary.Length - 22);
        var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(end[12..]);
        var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(end[16..]);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(ordinary.AsSpan(0, ordinary.Length - 22));
            var wideOffset = (ulong)bytes.Position + (ulong)bias;
            writer.Write(0x06064b50u); writer.Write(44UL);
            writer.Write((ushort)45); writer.Write((ushort)45); writer.Write(0u); writer.Write(0u);
            writer.Write(2UL); writer.Write(2UL); writer.Write((ulong)directorySize); writer.Write((ulong)directoryOffset + (ulong)bias);
            writer.Write(0x07064b50u); writer.Write(0u); writer.Write(wideOffset); writer.Write(1u);
            writer.Write(0x06054b50u); writer.Write((ushort)0); writer.Write((ushort)0);
            writer.Write(ushort.MaxValue); writer.Write(ushort.MaxValue); writer.Write(uint.MaxValue); writer.Write(uint.MaxValue); writer.Write((ushort)0);
        }
        var host = new ZipHost(bytes.ToArray()) { Bias = bias };
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile("import zipfile\nwith zipfile.ZipFile('t.zip') as z:\n print(z.namelist())\n return z.read('small')");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("['padding', 'small']\n", result.StandardOutput);
            Assert.Equal("hello"u8.ToArray(), Assert.IsType<byte[]>(result.ReturnValue));
            Assert.InRange(host.BytesRead, 1, 80000);
            Assert.True(host.Files.Single().ReadableLength > uint.MaxValue);
            Assert.Equal(0, host.SequentialReads);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task ForeignInfoKeepsHeaderValidationAndCaptureMetadata(bool asynchronous)
    {
        var host = new ZipHost(BuildArchive());
        using var script = new LythonEngine().Compile("""
            import zipfile
            with zipfile.ZipFile('t.zip') as z:
                info=zipfile.ZipInfo('small')
                info.header_offset=1048613
                info.compress_size=5
                info.file_size=5
                info.CRC=0x3610a686
                print(z.read(info))
                bad=zipfile.ZipInfo('different')
                bad.header_offset=info.header_offset
                bad.compress_size=5
                bad.file_size=5
                bad.CRC=info.CRC
                try:z.read(bad)
                except zipfile.BadZipFile:print('bad name')
            """);
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b'hello'\nbad name\n", result.StandardOutput);
        Assert.Equal(1, host.Closed);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task NameIndexUsesTheDirectorySnapshotBeforeFirstLookup(bool asynchronous)
    {
        var host = new ZipHost(BuildArchive());
        using var script = new LythonEngine().Compile("import zipfile\nwith zipfile.ZipFile('t.zip') as z:\n z.infolist()[1].filename='renamed'\n print(z.read('small'))\n try:z.read('renamed')\n except KeyError:print('missing')");
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b'hello'\nmissing\n", result.StandardOutput);
        Assert.Equal(1, host.Closed);
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task ChangedMemberDataKeepsPythonErrorsAndCleanup(bool asynchronous, bool badCrc)
    {
        var host = new ZipHost(BuildArchive());
        host.PendingRead = (file, offset, destination, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (destination.Length != 5) return ValueTask.FromResult(file.CopyAt(offset, destination));
            if (!badCrc) return ValueTask.FromResult(0); // In-place truncation after metadata inspection.
            var count = file.CopyAt(offset, destination);
            destination.Span[0] ^= 0x20;
            return ValueTask.FromResult(count);
        };
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile("import zipfile\nwith zipfile.ZipFile('t.zip') as z:\n try:z.read('small')\n except zipfile.BadZipFile:print('bad data')");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("bad data\n", result.StandardOutput);
            Assert.Equal(1, host.Closed);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task ListingStillRejectsMalformedLocalHeaders(bool asynchronous)
    {
        var bytes = BuildArchive(); bytes[0] ^= 0xff;
        var host = new ZipHost(bytes);
        using var script = new LythonEngine().Compile("import zipfile\nz=zipfile.ZipFile('t.zip')\nprint('published')");
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.False(result.Success);
        Assert.Equal("BadZipFile", result.Failure?.ExceptionType);
        Assert.Empty(result.StandardOutput);
        Assert.Equal(1, host.Closed);
        Assert.Equal(0, host.SequentialReads);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task RefusalPrecedesFirstDestinationBorrow(bool asynchronous)
    {
        var files = new ZipHost(BuildArchive());
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 32768, files);
        using var script = new LythonEngine().Compile("import zipfile,time\ntime.time()\ntry:\n z=zipfile.ZipFile('t.zip')\nexcept MemoryError:print('suppressed')\nfinally:return 7");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(0, files.Reads);
            Assert.Equal(1, files.Closed);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task PendingReadAndCloseAreJoinedBeforeReturningAllowance(bool refuse, bool memberRead)
    {
        var entered = Signal(); var release = Signal(); var closing = Signal(); var retire = Signal();
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(256 * 1024);
        var host = new ZipHost(BuildArchive()) { CompletesSynchronously = false };
        host.PendingRead = async (file, offset, destination, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            if (memberRead && destination.Length != 5) return file.CopyAt(offset, destination);
            entered.TrySetResult();
            await release.Task;
            if (refuse) throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "positional input refused", requestedBytes: 1000000);
            return file.CopyAt(offset, destination); // Complete the actual borrow after cancellation.
        };
        host.Retire = async () => { closing.TrySetResult(); await retire.Task; };
        var read = memberRead ? "\n value=z.read('small')" : "";
        using var script = new LythonEngine().Compile("import zipfile\ntry:\n z=zipfile.ZipFile('t.zip')" + read + "\nexcept MemoryError:print('suppressed')\nfinally:return 7", budget);
        var baseline = budget.ReservedBytes;
        var run = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var pending = budget.ReservedBytes;
            Assert.True(pending > baseline + (memberRead ? 5 : 65536));
            if (!refuse) cancellation.Cancel();
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes >= pending);
            release.TrySetResult();
            await closing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.Equal(0, host.Closed);
            retire.TrySetResult();
            using (var result = await run.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                Assert.False(result.Success);
                Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                if (refuse) Assert.Contains("positional input refused", result.Failure?.Message);
            }
            Assert.Equal(1, host.Closed);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally
        {
            release.TrySetResult(); retire.TrySetResult();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static byte[] BuildArchive()
    {
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var padding = archive.CreateEntry("padding", CompressionLevel.NoCompression).Open()) padding.Write(new byte[1024 * 1024]);
            using (var small = archive.CreateEntry("small", CompressionLevel.NoCompression).Open()) small.Write("hello"u8);
        }
        return bytes.ToArray();
    }

    private sealed class ZipHost(byte[] bytes) : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost _metadata = new();
        public byte[] Current { get; set; } = bytes;
        public long Bias { get; init; }
        public bool CompletesSynchronously { get; init; } = true;
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public int Opened, Closed, Reads, SequentialReads;
        public long BytesRead;
        public List<File> Files { get; } = [];
        public Action? FirstRead;
        public Func<File, long, Memory<byte>, CancellationToken, ValueTask<int>>? PendingRead;
        public Func<ValueTask>? Retire;
        public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options, ILythonMemoryBudget budget, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal("t.zip", path.Path); Assert.Equal("/", path.Cwd); Assert.Equal(LythonFileAccess.Read, options.Access);
            Opened++;
            var file = new File(this, Current); Files.Add(file);
            return ValueTask.FromResult<ILythonByteFile>(file);
        }
        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => ValueTask.FromResult(true);
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token)
            => ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, Current.Length, DateTimeOffset.UnixEpoch));
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest p, CancellationToken t) => _metadata.ListDirAsync(p, t);
        public ValueTask MkDirAsync(LythonPathRequest p, CancellationToken t) => _metadata.MkDirAsync(p, t);
        public ValueTask RemoveAsync(LythonPathRequest p, CancellationToken t) => _metadata.RemoveAsync(p, t);
        public ValueTask CopyAsync(LythonPathRequest a, LythonPathRequest b, CancellationToken t) => _metadata.CopyAsync(a, b, t);
        public ValueTask MoveAsync(LythonPathRequest a, LythonPathRequest b, CancellationToken t) => _metadata.MoveAsync(a, b, t);

        public sealed class File(ZipHost host, byte[] data) : ILythonPositionalByteFile, ILythonSynchronousHostCapability
        {
            private bool _closed;
            public bool CompletesSynchronously => host.CompletesSynchronously;
            public long Position { get; private set; }
            public long? LengthHint => data.LongLength;
            public long ReadableLength => checked(data.LongLength + host.Bias);
            public async ValueTask<int> ReadAtAsync(long offset, Memory<byte> destination, CancellationToken token)
            {
                ObjectDisposedException.ThrowIf(_closed, this); token.ThrowIfCancellationRequested();
                if (host.Reads++ == 0) host.FirstRead?.Invoke();
                return host.PendingRead is { } pending ? await pending(this, offset, destination, token) : CopyAt(offset, destination);
            }
            public int CopyAt(long offset, Memory<byte> destination)
            {
                var count = (int)Math.Min(Math.Min(destination.Length, 4093), Math.Max(0, ReadableLength - offset));
                if (host.Bias == 0) data.AsMemory((int)offset, count).CopyTo(destination);
                else
                {
                    for (var i = 0; i < count; i++)
                    {
                        var absolute = offset + i;
                        var physical = absolute >= host.Bias ? absolute - host.Bias : absolute;
                        destination.Span[i] = physical < data.LongLength ? data[(int)physical] : (byte)0;
                    }
                }
                host.BytesRead += count;
                return count;
            }
            public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken token)
            {
                host.SequentialReads++; token.ThrowIfCancellationRequested();
                var count = CopyAt(Position, destination); Position += count;
                return ValueTask.FromResult(count);
            }
            public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> b, CancellationToken t) => throw new InvalidOperationException();
            public ValueTask FlushAsync(CancellationToken token) => ValueTask.CompletedTask;
            public async ValueTask DisposeAsync()
            {
                if (_closed) return;
                if (host.Retire is { } retire) await retire();
                _closed = true; host.Closed++;
            }
        }
    }
}
