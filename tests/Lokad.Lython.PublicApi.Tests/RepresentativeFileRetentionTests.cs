using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepresentativeFileRetentionTests
{
    [Theory]
    [InlineData(false, "file-manifest")]
    [InlineData(true, "file-manifest")]
    [InlineData(false, "file-manifest-helpers")]
    [InlineData(true, "file-manifest-helpers")]
    public Task FileHostBatchesReuseSharedAllowance(bool asynchronous, string scriptName)
        => VerifyBatches(asynchronous, scriptName, 2);

    [Theory]
    [Trait("Category", "Stress")]
    [InlineData(false, "file-manifest")]
    [InlineData(true, "file-manifest")]
    [InlineData(false, "file-manifest-helpers")]
    [InlineData(true, "file-manifest-helpers")]
    public Task SixtyFourFilesInHostBatchesReuseSharedAllowance(bool asynchronous, string scriptName)
        => VerifyBatches(asynchronous, scriptName, 16);

    private static async Task VerifyBatches(bool asynchronous, string scriptName, int batches)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", scriptName + ".py")), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        for (var batch = 0; batch < batches; batch++)
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            ILythonHost host = (ILythonHost?)delayed ?? mock!;
            var inputs = new Dictionary<string, byte[]>();
            var manifest = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var name = $"file{batch * 4 + i:D3}.bin";
                var bytes = Enumerable.Range(0, 8192).Select(j => (byte)((j + batch + i) % 256)).ToArray();
                inputs[name] = bytes;
                if (delayed is not null) delayed.SeedBytes("/inbox/" + name, bytes);
                else mock!.SeedBytes("/inbox/" + name, bytes);
                manifest.Add($"{{\"checksum\":1044480,\"name\":\"{name}\",\"size\":8192}}");
            }
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4\n", result.StandardOutput);
                foreach (var input in inputs)
                    Assert.Equal(input.Value, delayed is not null ? delayed.ReadBytes("/outbox/" + input.Key) : mock!.ReadBytes("/outbox/" + input.Key));
                var actual = delayed is not null ? delayed.ReadText("/manifest.json") : mock!.ReadText("/manifest.json");
                Assert.Equal(Encoding.UTF8.GetBytes("[" + string.Join(',', manifest) + "]\n"), Encoding.UTF8.GetBytes(actual));
                Assert.True(budget.ReservedBytes > baseline);
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("drop", false)]
    [InlineData("one", false)]
    [InlineData("all", false)]
    [InlineData("drop", true)]
    [InlineData("one", true)]
    [InlineData("all", true)]
    public Task CompleteFiniteFileBatchAndRejectLargerRetainedInput(string retention, bool asynchronous)
        => Verify(retention, asynchronous, "file-manifest");

    [Theory]
    [InlineData("drop", false)]
    [InlineData("one", false)]
    [InlineData("all", false)]
    [InlineData("drop", true)]
    [InlineData("one", true)]
    [InlineData("all", true)]
    public Task AnnotatedFiniteFileBatchAndLargerRetainedInput(string retention, bool asynchronous)
        => Verify(retention, asynchronous, "file-manifest-helpers");

    private static async Task Verify(string retention, bool asynchronous, string scriptName)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", scriptName + ".py"));
        if (retention == "one")
            source = "first = None\n" + source.Replace("        data = source.read()",
                "        data = source.read()\n        if first is None:\n            first = data") + "\nreturn first\n";
        else if (retention == "all")
            source = "held = []\n" + source.Replace("        data = source.read()", "        data = source.read()\n        held.append(data)");
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var manifest = new List<string>();
        var fileCount = retention == "all" ? 64 : 4;
        // These are different finite inputs, not a retention-only comparison.
        // Dropped guest data may stay charged until the execution owner ends.
        for (var i = 0; i < fileCount; i++)
        {
            var name = $"file{i:D3}.bin";
            var data = Enumerable.Range(0, 8192).Select(j => (byte)((j + i) % 256)).ToArray();
            inputs[name] = data;
            if (delayed is not null) delayed.SeedBytes("/inbox/" + name, data); else mock!.SeedBytes("/inbox/" + name, data);
            // Independent byte arithmetic, not the guest's aggregation/serializer.
            manifest.Add($"{{\"checksum\":1044480,\"name\":\"{name}\",\"size\":8192}}");
        }
        var expectedManifest = Encoding.UTF8.GetBytes("[" + string.Join(',', manifest) + "]\n");
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retention == "all")
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.False(await host.ExistsAsync("/manifest.json", CancellationToken.None));
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal($"{fileCount}\n", result.StandardOutput);
            if (retention == "one") Assert.Equal(inputs["file000.bin"], Assert.IsType<byte[]>(result.ReturnValue));
            foreach (var input in inputs)
                Assert.Equal(input.Value, delayed is not null ? delayed.ReadBytes("/outbox/" + input.Key) : mock!.ReadBytes("/outbox/" + input.Key));
            var actual = delayed is not null ? delayed.ReadText("/manifest.json") : mock!.ReadText("/manifest.json");
            Assert.Equal(expectedManifest, Encoding.UTF8.GetBytes(actual));
        }
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
