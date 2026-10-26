using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedCompressorDoesNotCreateOrTruncate(bool exists)
    {
        using var script = new LythonEngine().Compile("import gzip,time\ntime.time()\ngzip.GzipFile('data.gz','wb')");
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

    [Fact]
    public async Task LargeFilenameUsesOrdinaryOpenAdmission()
    {
        using var script = new LythonEngine().Compile("import gzip,time\nname='x'*262144\ntime.time()\ngzip.GzipFile(name,'rb')");
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
