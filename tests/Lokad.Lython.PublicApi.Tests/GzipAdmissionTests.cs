using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipAdmissionTests
{
    [Theory]
    [InlineData("gzip.compress(b'payload',mtime=0)")]
    [InlineData("gzip.decompress(data)")]
    public async Task BulkNativeStorageRefusalIsTerminalAndReturnsAllowance(string expression)
    {
        using var script = new LythonEngine().Compile("import gzip,time\ndata=gzip.compress(b'payload',mtime=0)\ntime.time()\ntry:\n " + expression + "\nexcept MemoryError:\n pass\nprint('continued')");
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("continued", result.StandardOutput);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, "gzip.GzipFile")]
    [InlineData(true, "gzip.GzipFile")]
    [InlineData(false, "gzip.open")]
    [InlineData(true, "gzip.open")]
    public async Task RefusedCompressorDoesNotCreateOrTruncate(bool exists, string factory)
    {
        using var script = new LythonEngine().Compile("import gzip,time\ntime.time()\n" + factory + "('data.gz','wb')");
        foreach (var asynchronous in new[] { false, true })
        {
            var immediate = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            byte[] original = [1, 2, 3];
            if (exists) { immediate.SeedBytes("/data.gz", original); delayed.SeedBytes("/data.gz", original); }
            var budget = new AdmissionGateBudget();
            ILythonHost files = asynchronous ? delayed : immediate;
            var host = new AdmissionGateHost(budget, 32768, files);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal(exists, await files.ExistsAsync("/data.gz", CancellationToken.None));
                if (exists) Assert.Equal(original, asynchronous ? delayed.ReadBytes("/data.gz") : immediate.ReadBytes("/data.gz"));
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("gzip.GzipFile")]
    [InlineData("gzip.open")]
    public async Task LargeFilenameUsesOrdinaryOpenAdmission(string factory)
    {
        using var script = new LythonEngine().Compile("import gzip,time\nname='x'*262144\ntime.time()\n" + factory + "(name,'rb')");
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768, new MockLythonHost());
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }
}
