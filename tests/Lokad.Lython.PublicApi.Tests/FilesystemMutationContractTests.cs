using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FilesystemMutationContractTests
{
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
            await host.MkDirAsync("/empty", default);
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
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => Inner.ListDirAsync(p, t);
        public ValueTask MkDirAsync(string p, CancellationToken t) => Inner.MkDirAsync(p, t);
        public ValueTask RemoveAsync(string p, CancellationToken t) { GenericMutations++; return Inner.RemoveAsync(p, t); }
        public ValueTask CopyAsync(string s, string d, CancellationToken t) => Inner.CopyAsync(s, d, t);
        public ValueTask MoveAsync(string s, string d, CancellationToken t) { GenericMutations++; return Inner.MoveAsync(s, d, t); }
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest p, CancellationToken t) => Inner.StatAsync(p, t);
    }
    private sealed class PendingHost : LegacyHost, ILythonHost, ILythonSynchronousHostCapability
    {
        public new bool CompletesSynchronously => false;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cleaned { get; private set; }
        private async ValueTask Pending(CancellationToken token)
        { Entered.TrySetResult(); try { await Task.Delay(Timeout.Infinite, token); } finally { Cleaned = true; } }
        public ValueTask ReplaceAsync(string s, string d, CancellationToken t) => Pending(t);
        public ValueTask RemoveFileAsync(string p, bool missingOk, CancellationToken t) => Pending(t);
        public ValueTask RemoveDirectoryAsync(string p, CancellationToken t) => Pending(t);
    }
}
