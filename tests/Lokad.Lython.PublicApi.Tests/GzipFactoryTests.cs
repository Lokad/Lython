using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFactoryTests
{
    // Independent CPython 3.13.2 outputs and final payloads. Concurrent writers
    // to one path require a separate host contract and are not acceptance cases.
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "GzipFactory", "cases.json");
        foreach (var item in JsonSerializer.Deserialize<FactoryCase[]>(File.ReadAllText(path))!)
            yield return [item.Name, item];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FactoriesSharePythonEffectsAndCleanup(string name, FactoryCase item)
    {
        Assert.Equal(name, item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(x => x.Message)));
        var budget = new LythonMemoryBudget(item.Budget);
        foreach (var asynchronous in new[] { false, true })
        {
            var immediate = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            foreach (var file in item.Files)
            {
                var bytes = Convert.FromBase64String(file.Value);
                immediate.SeedBytes('/' + file.Key, bytes);
                delayed.SeedBytes('/' + file.Key, bytes);
            }
            ILythonHost host = asynchronous ? delayed : immediate;
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(item.StandardOutput, result.StandardOutput);
                Assert.Equal(item.FinalExists, await host.ExistsAsync("/data.gz", CancellationToken.None));
                if (item.FinalExists)
                {
                    var bytes = asynchronous ? delayed.ReadBytes("/data.gz") : immediate.ReadBytes("/data.gz");
                    if (item.FinalSeedUnchanged) Assert.Equal(Convert.FromBase64String(item.Files["data.gz"]), bytes);
                    else if (item.FinalData is { } expected)
                    {
                        using var input = new MemoryStream(bytes);
                        using var gzip = new GZipStream(input, CompressionMode.Decompress);
                        using var payload = new MemoryStream();
                        await gzip.CopyToAsync(payload);
                        Assert.Equal(expected.length, payload.Length);
                        Assert.Equal(expected.sha256, Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant());
                    }
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
            if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        }
    }

    [Theory]
    [InlineData("rt")]
    [InlineData("rb")]
    public async Task ClosedReaderResultsRemainFundedUntilRelinquished(string mode)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("import gzip\nwith gzip.open('data.gz','" + mode + "') as f: value=f.read()\nreturn value", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            using var stream = new MemoryStream();
            using (var compressor = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
                compressor.Write(new byte[65536]);
            host.SeedBytes("/data.gz", stream.ToArray());
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(65536, mode == "rt" ? Assert.IsType<string>(result.ReturnValue).Length : Assert.IsType<byte[]>(result.ReturnValue).Length);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var next = script.Run(host)) Assert.True(next.Success, next.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public sealed record FactoryCase(string Name, string Source, Dictionary<string,string> Files, long Budget,
        string StandardOutput, bool FinalSeedUnchanged, bool FinalExists, Payload? FinalData);
    public sealed record Payload(long length, string sha256);
}
