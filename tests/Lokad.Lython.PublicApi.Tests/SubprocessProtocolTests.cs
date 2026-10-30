using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BinaryCapturesInputsErrorsAndPipesPreserveArbitraryBytes(bool asynchronous)
    {
        var host = new TestHost(new MockLythonHost());
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
            import subprocess
            p=subprocess.run(['raw'],input=b'\xff\x00',capture_output=True)
            assert p.stdout == b'\xff\x00\nlast'
            assert p.stderr == b'\xfe'
            assert subprocess.check_output(['raw']) == p.stdout
            try:subprocess.run(['fail'],capture_output=True,check=True)
            except subprocess.CalledProcessError as e:
                assert e.output == p.stdout and e.stderr == b'\xfe'
            q=subprocess.Popen(['raw'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
            assert not hasattr(q.stdout,'encoding')
            assert q.encoding is None and q.errors is None
            assert q.stdin.write(b'\xff') == 1
            out,err=q.communicate(b'\x00')
            assert out == p.stdout and err == p.stderr
            r=subprocess.Popen(['raw'],stdout=subprocess.PIPE)
            assert r.stdout.read(1) == b'\xff'
            assert r.stdout.readline() == b'\x00\n'
            assert r.stdout.readlines() == [b'last']
            assert r.stdout.read() == b''
            source=subprocess.Popen(['raw'],stdout=subprocess.PIPE)
            sink=subprocess.Popen(['raw'],stdin=source.stdout)
            sink.wait()
            print('ok')
            """);
        using (var result = await Run(script, host, budget, asynchronous))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal("ok\n", result.StandardOutput);
            Assert.True(budget.ReservedBytes > 0);
            Assert.Equal(new byte[] { 255, 0 }, host.Requests[0].StandardInputUtf8.ToArray());
            Assert.Equal(new byte[] { 255, 0 }, host.Requests[3].StandardInputUtf8.ToArray());
            Assert.Equal(new byte[] { 255, 0, 10, 108, 97, 115, 116 }, host.Requests[^1].StandardInputUtf8.ToArray());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutsUseSecondsAndPreserveOriginalErrorValues(bool asynchronous)
    {
        var host = new TestHost(new MockLythonHost());
        using var script = new LythonEngine().Compile("""
            import subprocess
            for timeout in [2,0.25,None,-1]:subprocess.run(['ok'],timeout=timeout)
            for action in [lambda:subprocess.run(['slow'],timeout=0.25),
                           lambda:subprocess.call(['slow'],timeout=0.25),
                           lambda:subprocess.check_output(['slow'],timeout=0.25),
                           lambda:subprocess.Popen(['slow']).wait(timeout=0.25),
                           lambda:subprocess.Popen(['slow']).communicate(timeout=0.25)]:
                try:action()
                except subprocess.TimeoutExpired as e:print(e.timeout,e.cmd)
            """);
        var budget = new LythonMemoryBudget();
        using (var result = await Run(script, host, budget, asynchronous))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal(string.Concat(Enumerable.Repeat("0.25 ['slow']\n", 5)), result.StandardOutput);
            Assert.Equal(TimeSpan.FromSeconds(2), host.Requests[0].Timeout);
            Assert.Equal(TimeSpan.FromSeconds(.25), host.Requests[1].Timeout);
            Assert.Null(host.Requests[2].Timeout);
            Assert.Equal(TimeSpan.Zero, host.Requests[3].Timeout);
            Assert.All(host.Requests.Skip(4), r => Assert.Equal(TimeSpan.FromSeconds(.25), r.Timeout));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TruthFlagsAndTextConsistencyPreserveEvaluationPoints(bool asynchronous)
    {
        var host = new TestHost(new MockLythonHost());
        using var script = new LythonEngine().Compile("""
            import subprocess
            class Check:
                def __bool__(self):
                    print('checked')
                    return True
            subprocess.run(['ok'],check=Check(),capture_output=1,text=1,universal_newlines='yes')
            try:subprocess.run(['never'],text=True,universal_newlines=False)
            except subprocess.SubprocessError:print('conflict')
            subprocess.run('ok',shell=1)
            """);
        var budget = new LythonMemoryBudget();
        using var result = await Run(script, host, budget, asynchronous);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("checked\nconflict\n", result.StandardOutput);
        Assert.Equal(new[] { "ok", "ok" }, host.Requests.Select(r => r.Args[0]));
        Assert.Equal(LythonSubprocessInvocationMode.Shell, host.Requests[1].InvocationMode);
    }

    [Fact]
    public async Task AsyncPreparationAndCompletionCallbacksCanActuallySuspend()
    {
        var io = new DelayedLythonHost(); io.SeedFile("/arg", "ok");
        var host = new TestHost(io);
        using var script = new LythonEngine().Compile("""
            import subprocess
            events=[]
            class Args:
                def __iter__(self):
                    with open('/arg') as f:value=f.read()
                    events.append('args')
                    yield value
            class Flag:
                def __bool__(self):
                    with open('/arg') as f:f.read()
                    events.append('check')
                    return True
            subprocess.run(Args(),check=Flag())
            p=subprocess.Popen(Args())
            p.wait()
            print(events)
            """);
        var budget = new LythonMemoryBudget();
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal("['args', 'check', 'args']\n", result.StandardOutput);
            Assert.Equal(2, host.Requests.Count);
            Assert.True(io.CompletedAsynchronously >= 6);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringPreparationOrCheckAwaitsCleanup(bool duringCheck)
    {
        var io = new DelayedLythonHost(); io.SeedFile("/arg", "ok");
        var entered = io.PauseReadUntilCancellation("/arg");
        var host = new TestHost(io);
        using var script = new LythonEngine().Compile("""
            import subprocess
            def read():
                with open('/arg') as f:f.read()
            class Args:
                def __iter__(self):
                    read()
                    yield 'ok'
            class Flag:
                def __bool__(self):
                    read()
                    return True
            """ + (duringCheck ? "\nsubprocess.run(['ok'],check=Flag())" : "\nsubprocess.run(Args())") + "\nprint('resumed')");
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget();
        var pending = script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget },cancellation.Token);
        try { Assert.Same(entered,await Task.WhenAny(entered,pending).WaitAsync(TimeSpan.FromSeconds(5))); }
        finally
        {
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(duringCheck ? 1 : 0,host.Requests.Count);
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextEncodingErrorsAndNewlinesFollowTheSelectedMode(bool asynchronous)
    {
        var host = new TestHost(new MockLythonHost());
        using var script = new LythonEngine().Compile("""
            import subprocess
            p=subprocess.run(['text'],input='é',capture_output=True,encoding='utf-8-sig',errors='replace')
            assert p.stdout == 'é\nr\n' and p.stderr == '�'
            q=subprocess.Popen(['text'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,encoding='utf-8-sig')
            assert q.stdin.write('é') == 1
            out,err=q.communicate()
            assert out == p.stdout and err is None
            print('ok')
            """);
        var budget = new LythonMemoryBudget();
        using (var result = await Run(script,host,budget,asynchronous))
        {
            Assert.True(result.Success,Describe(result));
            Assert.Equal("ok\n",result.StandardOutput);
            Assert.All(host.Requests,r=>Assert.Equal(new byte[] {239,187,191,195,169},r.StandardInputUtf8.ToArray()));
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Fact]
    public async Task FormattingCancellationInterruptsNativeOutputGrowth()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new ObservedBudget(cancellation);
        var host = new TestHost(new MockLythonHost(), request => budget.Arm());
        using var script = new LythonEngine().Compile("import subprocess\nsubprocess.run(['arm'])\nvalue=subprocess.list2cmdline([arg])\nprint('finished')");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["arg"] = new string('x',65536) } }, cancellation.Token))
        {
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.True(budget.Cancelled);
            // Entry/exit-only checking would retain the complete 64 KiB output.
            Assert.True(budget.Root.PeakBytes < 2 * 65536);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    private static Task<LythonExecutionResult> Run(LythonCompiledScript script, ILythonHost host,
        ILythonMemoryBudget budget, bool asynchronous)
        => asynchronous ? script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
            : Task.FromResult(script.Run(host,new LythonRunOptions { MemoryBudget=budget }));
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join("; ",result.Diagnostics.Select(d=>d.Message));

    private sealed class TestHost(ILythonHost io, Action<LythonSubprocessRequest>? onRequest=null)
        : ILythonHost, ILythonSubprocessRunner, ILythonSynchronousHostCapability
    {
        public List<LythonSubprocessRequest> Requests { get; } = [];
        public bool CompletesSynchronously => io is ILythonSynchronousHostCapability { CompletesSynchronously:true };
        public bool AllowsShellInvocation => true;
        public ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Requests.Add(request); onRequest?.Invoke(request);
            if (request.Args[0]=="slow") throw new TimeoutException();
            ReadOnlyMemory<byte> stdout = request.Args[0] == "text" ? new byte[] {239,187,191,195,169,13,10,114,13}
                : request.Args[0] is "raw" or "fail" ? new byte[] {255,0,10,108,97,115,116} : ReadOnlyMemory<byte>.Empty;
            ReadOnlyMemory<byte> stderr = request.Args[0] is "text" or "raw" or "fail" ? new byte[] {254} : ReadOnlyMemory<byte>.Empty;
            return LythonSubprocessCompletion.CompleteBufferedAsync(request, request.Args[0] == "fail" ? 3 : 0, stdout, stderr,
                static (_, _) => ValueTask.CompletedTask, static (_, _) => ValueTask.CompletedTask, token);
        }
        public string Cwd=>io.Cwd;
        public DateTimeOffset LocalNow=>io.LocalNow;
        public DateTimeOffset UtcNow=>io.UtcNow;
        public ILythonSubprocessRunner SubprocessRunner=>this;
        public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path,LythonFileOpenOptions options,ILythonMemoryBudget budget,CancellationToken token)
            => io.OpenFileAsync(path,options,budget,token);
        public ValueTask<bool> ExistsAsync(LythonPathRequest p,CancellationToken t)=>io.ExistsAsync(p,t);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest p,CancellationToken t)=>io.ListDirAsync(p,t);
        public ValueTask MkDirAsync(LythonPathRequest p,CancellationToken t)=>io.MkDirAsync(p,t);
        public ValueTask RemoveAsync(LythonPathRequest p,CancellationToken t)=>io.RemoveAsync(p,t);
        public ValueTask CopyAsync(LythonPathRequest a, LythonPathRequest b,CancellationToken t)=>io.CopyAsync(a,b,t);
        public ValueTask MoveAsync(LythonPathRequest a, LythonPathRequest b,CancellationToken t)=>io.MoveAsync(a,b,t);
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest p,CancellationToken t)=>io.StatAsync(p,t);
    }

    private sealed class ObservedBudget(CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public LythonMemoryBudget Root { get; } = new();
        private long _cutoff=long.MaxValue;
        public bool Cancelled { get; private set; }
        public void Arm()=>_cutoff=Root.ReservedBytes+8192;
        private void Changed()
        {
            if (Root.ReservedBytes < _cutoff) return;
            _cutoff=long.MaxValue; Cancelled=true; cancellation.Cancel();
        }
        public ILythonMemoryScope CreateScope()=>new Owner(this,Root.CreateScope());
        public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)=>Reserve(Root,bytes,out reservation);
        private bool Reserve(ILythonMemoryBudget owner,long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!owner.TryReserve(bytes,out var inner)) { reservation=null; return false; }
            reservation=new Admission(this,inner); Changed(); return true;
        }
        private sealed class Owner(ObservedBudget outer,ILythonMemoryScope inner):ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope()=>new Owner(outer,inner.CreateScope());
            public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)=>outer.Reserve(inner,bytes,out reservation);
            public void Dispose()=>inner.Dispose();
        }
        private sealed class Admission(ObservedBudget outer,ILythonMemoryReservation inner):ILythonMemoryReservation
        {
            public long Bytes=>inner.Bytes;
            public bool TryResize(long bytes) { if(!inner.TryResize(bytes)) return false; outer.Changed(); return true; }
            public void Dispose()=>inner.Dispose();
        }
    }
}
