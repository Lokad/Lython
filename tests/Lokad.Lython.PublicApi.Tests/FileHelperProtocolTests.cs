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
        var host = new RangeHost();
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
            Assert.Equal(0, host.FullReads);
            Assert.Equal(2, host.RangeReads);
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
            var host = new RangeHost { ShortReads = true };
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
                    Assert.Equal(0, host.RangeReads);
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

    private sealed class RangeHost : ILythonHost, ILythonSynchronousHostCapability
    {
        internal MockLythonHost Inner { get; } = new();
        internal int FullReads, RangeReads, StatCalls;
        internal long AcquiredBytes;
        internal bool ShortReads;
        internal Action? AfterStats;
        public bool CompletesSynchronously => true;
        public string Cwd => Inner.Cwd;
        public DateTimeOffset LocalNow => Inner.LocalNow;
        public DateTimeOffset UtcNow => Inner.UtcNow;
        public ILythonTextOutput? StandardOutput => Inner.StandardOutput;
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string p, CancellationToken c) => Inner.ReadTextUtf8Async(p, c);
        public ValueTask WriteTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken c) => Inner.WriteTextUtf8Async(p, b, c);
        public ValueTask AppendTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken c) => Inner.AppendTextUtf8Async(p, b, c);
        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string p, CancellationToken c) { FullReads++; return Inner.ReadBytesAsync(p, c); }
        public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string p, long o, int n, CancellationToken c)
        {
            RangeReads++;
            var bytes = Inner.ReadBytesRangeAsync(p, o, ShortReads ? Math.Min(n, p == "/a" ? 3 : 5) : n, c).Result;
            AcquiredBytes += bytes.Length; return ValueTask.FromResult(bytes);
        }
        public ValueTask<bool> ExistsAsync(string p, CancellationToken c) => Inner.ExistsAsync(p, c);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken c) => Inner.ListDirAsync(p, c);
        public ValueTask MkDirAsync(string p, CancellationToken c) => Inner.MkDirAsync(p, c);
        public ValueTask RemoveAsync(string p, CancellationToken c) => Inner.RemoveAsync(p, c);
        public ValueTask CopyAsync(string p, string q, CancellationToken c) => Inner.CopyAsync(p, q, c);
        public ValueTask MoveAsync(string p, string q, CancellationToken c) => Inner.MoveAsync(p, q, c);
        public ValueTask<LythonPathStat> StatAsync(string p, CancellationToken c)
        { StatCalls++; var result = Inner.StatAsync(p, c); if (StatCalls == 2) AfterStats?.Invoke(); return result; }
    }
}
