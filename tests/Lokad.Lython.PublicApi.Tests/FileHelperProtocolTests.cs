using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FileHelperProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyDrainsPartialReadsAndSupportsNativeTextAndBytes(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import io,shutil
            class Source:
                def __init__(self):self.parts=iter(['ab','cd',''])
                def read(self,size):return next(self.parts)
            target=io.StringIO()
            shutil.copyfileobj(Source(),target)
            print(target.getvalue())
            for length in [0,2,-1]:
                source=io.BytesIO(b'abcdef')
                target=io.BytesIO()
                shutil.copyfileobj(source,target,length)
                print(target.getvalue())
            target=io.StringIO()
            shutil.copyfileobj(io.StringIO(''),target,2)
            print(repr(target.getvalue()))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("abcd\nb'abcdef'\nb'abcdef'\nb'abcdef'\n''\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyKeepsEscapingChunksFundedUntilResultDisposal(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import shutil
            class Source:
                def __init__(self):self.remaining=2
                def read(self,size):
                    if not self.remaining:return ''
                    self.remaining-=1
                    with open('/chunk') as source:return source.read()
            class Sink:
                def __init__(self):self.parts=[]
                def write(self,value):self.parts.append(value)
            sink=Sink()
            shutil.copyfileobj(Source(),sink,2)
            return sink.parts
            """);
        var budget = new LythonMemoryBudget(262144);
        for (var run = 0; run < 2; run++)
        {
            var host = new MockLythonHost(); host.SeedFile("/chunk", "retained");
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object[] { "retained", "retained" }, Assert.IsAssignableFrom<IEnumerable<object>>(result.ReturnValue));
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComparisonUsesBoundedRangesAndNativeTruthProtocols(bool asynchronous)
    {
        var host = new ByteHost();
        host.Inner.SeedBytes("/a", new byte[65536]);
        var changed = new byte[65536]; changed[0] = 1;
        host.Inner.SeedBytes("/b", changed);
        using var script = new LythonEngine().Compile("""
            import filecmp
            class Exact:
                def __bool__(self):
                    print('flag')
                    return False
            print(filecmp.cmp('/a','/b',shallow=Exact()))
            """);
        var budget = new LythonMemoryBudget(65536);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("flag\nFalse\n", result.StandardOutput);
            Assert.Equal(2, host.StatCalls);
            Assert.Equal(2, host.ReadCalls);
            Assert.Equal(16384, host.AcquiredBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComparisonFillsUnevenShortReadsAndUnwindsRefusedAdmission(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import filecmp\nreturn filecmp.cmp('/a','/b',False)");
        foreach (var deny in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(65536);
            var host = new ByteHost { ShortReads = true };
            host.Inner.SeedBytes("/a", [1, 2, 3, 4, 5, 6]);
            host.Inner.SeedBytes("/b", [1, 2, 3, 4, 5, 6]);
            ILythonMemoryReservation? pin = null;
            if (deny) host.AfterStats = () => Assert.True(budget.TryReserve(65536 - budget.ReservedBytes - 100, out pin));
            try
            {
                using var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget });
                if (deny)
                {
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Equal(0, host.ReadCalls);
                }
                else { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(true, result.ReturnValue); }
            }
            finally { pin?.Dispose(); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileCallbacksActuallySuspendAndCancelAfterEntry(bool copying)
    {
        var source = copying ? """
            import shutil
            class Source:
                def __init__(self):self.remaining=2
                def read(self,size):
                    if not self.remaining:return ''
                    self.remaining-=1
                    with open('/flag') as stream:return stream.read()
            class Sink:
                def write(self,value):
                    with open('/out','a') as stream:stream.write(value)
            shutil.copyfileobj(Source(),Sink(),2)
            """ : """
            import filecmp
            class Exact:
                def __bool__(self):
                    with open('/flag') as stream:return stream.read()=='shallow'
            print(filecmp.cmp('/a','/b',Exact()))
            """;
        using var script = new LythonEngine().Compile(source);
        var budget = new LythonMemoryBudget(262144);
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost(); host.SeedFile("/flag", "exact");
            host.SeedBytes("/a", [0]); host.SeedBytes("/b", [1]);
            var entered = cancel ? host.PauseReadUntilCancellation("/flag") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted); cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.True(host.CompletedAsynchronously > 0);
                    if (copying) Assert.Equal("exactexact", host.ReadText("/out"));
                    else Assert.Equal("False\n", result.StandardOutput);
                }
            }
            finally { cancellation.Cancel(); using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CopyAdmissionRefusalCannotBeSuppressedByItsWriter()
    {
        using var script = new LythonEngine().Compile("""
            import io,shutil
            class Sink:
                def write(self,value):
                    try:oversized=value*262144
                    finally:return None
            try:shutil.copyfileobj(io.StringIO('value'),Sink(),2)
            except MemoryError:print('caught')
            print('resumed')
            """);
        var budget = new LythonMemoryBudget(131072);
        using (var result = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComparisonRetiresBothFilesWithoutReplacingTheReadFailure(bool asynchronous)
    {
        var host = new ByteHost { FailReadAndClose = true };
        host.Inner.SeedBytes("/a", [1, 2]);
        host.Inner.SeedBytes("/b", [1, 2]);
        var budget = new LythonMemoryBudget(65536);
        using var script = new LythonEngine().Compile("import filecmp\nfilecmp.cmp('/a','/b',False)");
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal("OSError", result.Failure?.ExceptionType);
            Assert.Equal("primary read failure", result.Failure?.Message);
            Assert.Equal(2, host.Inner.ClosedFileCount);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ByteHost : ILythonHost, ILythonSynchronousHostCapability
    {
        internal MockLythonHost Inner { get; } = new();
        internal int ReadCalls, StatCalls;
        internal long AcquiredBytes;
        internal bool ShortReads, FailReadAndClose;
        internal Action? AfterStats;
        public bool CompletesSynchronously => true;
        public string Cwd => Inner.Cwd;
        public DateTimeOffset LocalNow => Inner.LocalNow;
        public DateTimeOffset UtcNow => Inner.UtcNow;
        public ILythonTextOutput? StandardOutput => Inner.StandardOutput;
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request,LythonFileOpenOptions options,ILythonMemoryBudget budget,CancellationToken token)
        {
            var file=await Inner.OpenFileAsync(request,options,budget,token);
            return new TestByteFile(file){Read=async(buffer,current)=>
            {
                ReadCalls++;
                if (FailReadAndClose) throw new LythonHostException(LythonHostFailureKind.Transport, "primary read failure");
                var count=await file.ReadAsync(ShortReads?buffer[..Math.Min(buffer.Length,request.Path=="/a"?3:5)]:buffer,current);
                AcquiredBytes+=count;return count;
            }, Close = async () =>
            {
                await file.DisposeAsync();
                if (FailReadAndClose) throw new LythonHostException(LythonHostFailureKind.PermissionDenied, "secondary close failure");
            }};
        }
        public ValueTask<bool> ExistsAsync(LythonPathRequest p, CancellationToken c) => Inner.ExistsAsync(p, c);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest p, CancellationToken c) => Inner.ListDirAsync(p, c);
        public ValueTask MkDirAsync(LythonPathRequest p, CancellationToken c) => Inner.MkDirAsync(p, c);
        public ValueTask RemoveAsync(LythonPathRequest p, CancellationToken c) => Inner.RemoveAsync(p, c);
        public ValueTask CopyAsync(LythonPathRequest p, LythonPathRequest q, CancellationToken c) => Inner.CopyAsync(p, q, c);
        public ValueTask MoveAsync(LythonPathRequest p, LythonPathRequest q, CancellationToken c) => Inner.MoveAsync(p, q, c);
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest p, CancellationToken c)
        { StatCalls++; var result = Inner.StatAsync(p, c); if (StatCalls == 2) AfterStats?.Invoke(); return result; }
    }
}
