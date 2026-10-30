using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FilesystemMutationContractTests
{
    [Theory]
    [InlineData("os.remove('blocker/../keep')")]
    [InlineData("os.replace('source','blocker/../keep')")]
    [InlineData("Path('source').replace('blocker/../keep')")]
    [InlineData("Path('blocker/../keep').unlink()")]
    [InlineData("os.rmdir('blocker/../directory')")]
    [InlineData("os.mkdir('blocker/../new')")]
    [InlineData("os.listdir('blocker/../directory')")]
    [InlineData("shutil.copyfile('source','blocker/../keep')")]
    [InlineData("shutil.move('source','blocker/../new')")]
    [InlineData("filecmp.cmp('blocker/../source','keep')")]
    public async Task InvalidIntermediateComponentsCannotRedirectAnOperation(string operation)
    {
        using var script = new LythonEngine().Compile("import os,shutil,filecmp\nfrom pathlib import Path\n" + operation);
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            var inner = new MockLythonHost("/repo");
            inner.SeedFile("/repo/blocker", "file"); inner.SeedFile("/repo/source", "replacement");
            inner.SeedFile("/repo/keep", "original"); inner.MkDir("/repo/directory");
            using (var result = asynchronous ? await script.RunAsync(inner, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(inner, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal("NotADirectoryError", result.Failure?.ExceptionType);
                Assert.Equal("original", inner.ReadText("/repo/keep"));
                Assert.Equal("replacement", inner.ReadText("/repo/source"));
                Assert.True(inner.Stat("/repo/directory").IsDir);
                Assert.False(inner.Exists("/repo/new"));
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("os.makedirs('new/../target')", null, "new,target")]
    [InlineData("Path('new/../target').mkdir(parents=True)", null, "new,target")]
    [InlineData("os.makedirs('new/..')", "FileExistsError", "new")]
    [InlineData("Path('new/..').mkdir(parents=True)", "FileExistsError", "new")]
    [InlineData("os.makedirs('new/./.')", null, "new")]
    [InlineData("os.makedirs('new/')", null, "new")]
    [InlineData("os.makedirs('')", "FileNotFoundError", "")]
    public async Task RecursiveCreationPreservesPrefixesAndFinalLeafEffects(string operation, string? error, string names)
    {
        using var script = new LythonEngine().Compile("import os\nfrom pathlib import Path\n" + operation);
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost("/repo");
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(error is null, result.Success);
                Assert.Equal(error, result.Failure?.ExceptionType);
                Assert.Equal(names.Length == 0 ? [] : names.Split(','), host.ListDir("/repo"));
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RefusedCopyCannotDeleteAnExistingDestination()
    {
        using var script = new LythonEngine().Compile("import shutil\ntry:shutil.copyfile('source','keep')\nexcept MemoryError:return 'continued'\nfinally:return 'suppressed'");
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new RefusedCopyHost(budget, asynchronous);
            host.Inner.SeedFile("/source", "replacement"); host.Inner.SeedFile("/keep", "original");
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Contains("copy replacement refused", result.Failure?.Message);
                Assert.Null(result.ReturnValue);
                Assert.Equal("original", host.Inner.ReadText("/keep"));
                Assert.Equal(0, host.GenericMutations);
            }
            Assert.Equal(1, host.CopyCalls);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private sealed class RefusedCopyHost(ILythonMemoryBudget budget, bool asynchronous) : LegacyHost, ILythonHost, ILythonSynchronousHostCapability
    {
        public new bool CompletesSynchronously => !asynchronous;
        public int CopyCalls { get; private set; }
        public new async ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token)
        {
            CopyCalls++;
            Assert.Equal(new LythonPathRequest("source", "/"), source);
            Assert.Equal(new LythonPathRequest("keep", "/"), destination);
            if (asynchronous) await Task.Yield();
            token.ThrowIfCancellationRequested();
            Assert.False(budget.TryReserve(1048576, out _));
            throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "copy replacement refused", requestedBytes: 1048576);
        }
    }

    public static IEnumerable<object[]> Operations()
    {
        yield return new object[] { "Path('/keep').replace('/keep')", true, true, true };
        yield return new object[] { "os.replace('/keep','/keep')", true, true, true };
        yield return new object[] { "Path('/missing').replace('/keep')", false, true, true };
        yield return new object[] { "os.replace('/missing','/keep')", false, true, true };
        yield return new object[] { "Path('/keep').replace('/empty')", false, true, true };
        yield return new object[] { "Path('/empty').replace('/keep')", false, true, true };
        yield return new object[] { "Path('/keep').rmdir()", false, true, true };
        yield return new object[] { "os.rmdir('/keep')", false, true, true };
        yield return new object[] { "Path('/empty').unlink()", false, true, true };
        yield return new object[] { "os.unlink('/empty')", false, true, true };
        yield return new object[] { "Path('/keep').unlink()", true, false, true };
        yield return new object[] { "os.rmdir('/empty')", true, true, false };
        yield return new object[] { "Path('/missing').unlink(missing_ok=True)", true, true, true };
        yield return new object[] { "Path('/source').replace('/keep')", true, true, true };
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task MutationsPreserveIntentAndExistingStorage(string operation, bool success, bool keep, bool empty)
    {
        using var script = new LythonEngine().Compile("import os\nfrom pathlib import Path\n" + operation);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            await FixtureFiles.WriteAsync(host,"/keep", "original"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/source", "replacement"u8.ToArray(), default);
            await host.MkDirAsync(new LythonPathRequest("/empty", "/"), default);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
                Assert.Equal(success, result.Success);
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(keep, (await host.StatAsync(new LythonPathRequest("/keep", "/"), default)).Exists);
            Assert.Equal(empty, (await host.StatAsync(new LythonPathRequest("/empty", "/"), default)).Exists);
            if (keep)
            {
                var expected = operation == "Path('/source').replace('/keep')" ? "replacement" : "original";
                Assert.Equal(expected, System.Text.Encoding.UTF8.GetString((await FixtureFiles.ReadAsync(host,"/keep", default)).Span));
            }
        }
    }

    [Theory]
    [InlineData("Path('/source').replace('/keep')")]
    [InlineData("Path('/keep').unlink()")]
    [InlineData("Path('/empty').rmdir()")]
    public void MissingCapabilitiesNeverUseGenericMutationFallback(string operation)
    {
        var host = new LegacyHost(); var budget = new LythonMemoryBudget(262144);
        host.Inner.SeedFile("/keep", "original"); host.Inner.SeedFile("/source", "replacement"); host.Inner.MkDir("/empty");
        using (var result = new LythonEngine().Run("from pathlib import Path\n" + operation, host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, host.GenericMutations); Assert.True(host.Inner.Exists("/empty")); Assert.Equal("original", host.Inner.ReadText("/keep"));
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("Path('/source').replace('/keep')")]
    [InlineData("Path('/keep').unlink()")]
    [InlineData("Path('/empty').rmdir()")]
    public async Task PendingMutationCancellationIsAwaitedBeforeOwnershipEnds(string operation)
    {
        var host = new PendingHost(); var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("from pathlib import Path\n" + operation + "\nprint('finished')");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
        try { await host.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(run.IsCompleted); Assert.True(budget.ReservedBytes > 0); }
        finally { cancellation.Cancel(); }
        using (var result = await run.WaitAsync(TimeSpan.FromSeconds(10)))
        { Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason); Assert.Empty(result.StandardOutput); }
        Assert.True(host.Cleaned); Assert.Equal(0, host.GenericMutations); Assert.Equal(0, budget.ReservedBytes);
    }

    private class LegacyHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public MockLythonHost Inner { get; } = new(); public int GenericMutations { get; private set; }
        public bool CompletesSynchronously => true; public string Cwd => Inner.Cwd;
        public DateTimeOffset LocalNow => Inner.LocalNow; public DateTimeOffset UtcNow => Inner.UtcNow;
        public ValueTask<bool> ExistsAsync(LythonPathRequest p, CancellationToken t) => Inner.ExistsAsync(p, t);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest p, CancellationToken t) => Inner.ListDirAsync(p, t);
        public ValueTask MkDirAsync(LythonPathRequest p, CancellationToken t) => Inner.MkDirAsync(p, t);
        public ValueTask RemoveAsync(LythonPathRequest p, CancellationToken t) { GenericMutations++; return Inner.RemoveAsync(p, t); }
        public ValueTask CopyAsync(LythonPathRequest s, LythonPathRequest d, CancellationToken t) => Inner.CopyAsync(s, d, t);
        public ValueTask MoveAsync(LythonPathRequest s, LythonPathRequest d, CancellationToken t) { GenericMutations++; return Inner.MoveAsync(s, d, t); }
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest p, CancellationToken t) => Inner.StatAsync(p, t);
    }
    private sealed class PendingHost : LegacyHost, ILythonHost, ILythonSynchronousHostCapability
    {
        public new bool CompletesSynchronously => false;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cleaned { get; private set; }
        private async ValueTask Pending(CancellationToken token)
        { Entered.TrySetResult(); try { await Task.Delay(Timeout.Infinite, token); } finally { Cleaned = true; } }
        public ValueTask ReplaceAsync(LythonPathRequest s, LythonPathRequest d, CancellationToken t) => Pending(t);
        public ValueTask RemoveFileAsync(LythonPathRequest p, bool missingOk, CancellationToken t) => Pending(t);
        public ValueTask RemoveDirectoryAsync(LythonPathRequest p, CancellationToken t) => Pending(t);
    }
}
