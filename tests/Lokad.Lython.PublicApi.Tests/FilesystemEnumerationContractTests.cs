using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FilesystemEnumerationContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        const string imports = "import glob\nfrom pathlib import Path\np=Path('/tree')\n";
        yield return new object[] { imports + "print(sorted(glob.glob('*/*.md',root_dir=p)))\nprint(sorted(str(x.relative_to(p)) for x in p.glob('*/*.md')))", "['sub/b.md']\n['sub/b.md']\n" };
        yield return new object[] { imports + "print(sorted(str(x.relative_to(p)) for x in p.rglob('sub/*.md')))\nprint(sorted(str(x.relative_to(p)) for x in p.rglob('*')))", "['sub/b.md']\n['.hidden', 'a.md', 'sub', 'sub/b.md']\n" };
        yield return new object[] { imports + "print(sorted(glob.glob('**',root_dir=p,recursive='yes',include_hidden=1)))\nprint(sorted(str(x.relative_to(p)) for x in p.glob('**')))", "['.hidden', 'a.md', 'sub', 'sub/b.md']\n['.', '.hidden', 'a.md', 'sub', 'sub/b.md']\n" };
        yield return new object[] { imports + "print(glob.glob('sub/**',root_dir=p,recursive=True))\nprint(glob.glob('*/',root_dir=p))\nprint([str(x.relative_to(p)) for x in p.glob('*/')])", "['sub/', 'sub/b.md']\n['sub/']\n['sub']\n" };
        yield return new object[] { imports + "it=glob.iglob('/tree/*.md');alias=it\nprint(iter(it) is it,next(it));del it\nprint(list(alias))\nit=p.glob('*.md');print(iter(it) is it,next(it))\nit=p.iterdir();print(iter(it) is it)", "True /tree/a.md\n[]\nTrue /tree/a.md\nTrue\n" };
        yield return new object[] { imports + "it=glob.iglob('/tree/*')\n(p/'after').write_text('')\nprint('/tree/after' in list(it))\nit=p.iterdir()\n(p/'later').write_text('')\nprint('/tree/later' in [str(x) for x in it])", "True\nFalse\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeEnumerationMatchesPython(string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            await host.MkDirAsync("/tree", default);
            await host.MkDirAsync("/tree/sub", default);
            foreach (var path in new[] { "/tree/a.md", "/tree/.hidden", "/tree/sub/b.md" }) await FixtureFiles.WriteAsync(host,path, Array.Empty<byte>(), default);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(expected, result.StandardOutput); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyExitDoesNotAcquireLaterDirectories(bool asynchronous)
    {
        var host = new TraversalHost(false);
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("import glob\nit=glob.iglob('/tree/**/a.md',recursive=True)\nprint(next(it))");
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        { Assert.True(result.Success, result.Failure?.Message); Assert.Equal("/tree/a.md\n", result.StandardOutput); }
        Assert.Equal(0, host.DirectoryReads);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("import glob\nnext(glob.iglob('/tree/*'))")]
    [InlineData("from pathlib import Path\nnext(Path('/tree').glob('*'))")]
    [InlineData("from pathlib import Path\nPath('/tree').iterdir()")]
    public async Task PendingEnumerationRemainsFundedUntilCancellationCleanup(string source)
    {
        var host = new TraversalHost(true);
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(source + "\nprint('continued')");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
        try
        { await host.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > 0); }
        finally { cancellation.Cancel(); }
        using (var result = await run.WaitAsync(TimeSpan.FromSeconds(10)))
        { Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason); Assert.Empty(result.StandardOutput); }
        Assert.True(host.Cleaned);
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class TraversalHost(bool pause) : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => !pause;
        public int DirectoryReads { get; private set; }
        public bool Cleaned { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Cwd => "/tree";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public async ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token)
        {
            DirectoryReads++;
            if (!pause) throw new InvalidOperationException("Early exit must not scan directories");
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); } finally { Cleaned = true; }
            return Array.Empty<string>();
        }
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(new LythonPathStat(path.Path is "/" or "/tree" ? LythonPathKind.Directory : path.Path == "/tree/a.md" ? LythonPathKind.File : LythonPathKind.Missing, 0, null)); }
        public ValueTask<bool> ExistsAsync(LythonPathRequest p, CancellationToken t) => ValueTask.FromResult(false);
        public ValueTask MkDirAsync(string p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask CopyAsync(string s, string d, CancellationToken t) => throw new NotSupportedException();
        public ValueTask MoveAsync(string s, string d, CancellationToken t) => throw new NotSupportedException();
    }
}
