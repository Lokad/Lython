using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipCompressionContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "GzipFactory", "compression.json");
        foreach (var item in JsonSerializer.Deserialize<CompressionCase[]>(File.ReadAllText(path))!)
            yield return [item.Name, item];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompressionAndEofFollowPython313(string name, CompressionCase item)
    {
        Assert.Equal(name, item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(item.StandardOutput, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("compresslevel", "__index__")]
    [InlineData("mtime", "__int__")]
    public async Task SuspendedCompressionConversionCancelsAndReleasesItsOwner(string option, string protocol)
    {
        using var script = new LythonEngine().Compile("import gzip\nclass Value:\n def " + protocol + "(self):\n  with open('pause') as f:return int(f.read())\nreturn gzip.decompress(gzip.compress(b'payload'," + option + "=Value()))");
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/pause", "1");
        var reached = host.PauseReadUntilCancellation("/pause");
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await reached.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var next = new DelayedLythonHost();
        next.SeedFile("/pause", "1");
        using (var result = await script.RunAsync(next, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("payload"u8.ToArray(), Assert.IsType<byte[]>(result.ReturnValue));
            Assert.True(next.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public sealed record CompressionCase(string Name, string Source, string StandardOutput);
}
