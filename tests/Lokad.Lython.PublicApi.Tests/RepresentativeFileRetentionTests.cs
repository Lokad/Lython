using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepresentativeFileRetentionTests
{
    [Theory]
    [InlineData("drop", false)]
    [InlineData("one", false)]
    [InlineData("all", false)]
    [InlineData("drop", true)]
    [InlineData("one", true)]
    [InlineData("all", true)]
    public async Task CompleteFileJobBoundsDroppedAndEscapedBuffersButRejectsRetention(string retention, bool asynchronous)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", "file-manifest.py"));
        if (retention == "one")
            source = "first = None\n" + source.Replace("        data = source.read()",
                "        data = source.read()\n        if first is None:\n            first = data") + "\nreturn first\n";
        else if (retention == "all")
            source = "held = []\n" + source.Replace("        data = source.read()", "        data = source.read()\n        held.append(data)");
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var manifest = new List<string>();
        for (var i = 0; i < 64; i++)
        {
            var name = $"file{i:D3}.bin";
            var data = Enumerable.Range(0, 8192).Select(j => (byte)((j + i) % 256)).ToArray();
            inputs[name] = data;
            if (delayed is not null) delayed.SeedBytes("/inbox/" + name, data); else mock!.SeedBytes("/inbox/" + name, data);
            // Independent byte arithmetic, not the guest's aggregation/serializer.
            manifest.Add($"{{\"checksum\":1044480,\"name\":\"{name}\",\"size\":8192}}");
        }
        var expectedManifest = Encoding.UTF8.GetBytes("[" + string.Join(',', manifest) + "]\n");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retention == "all")
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.False(await host.ExistsAsync("/manifest.json", CancellationToken.None));
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("64\n", result.StandardOutput);
            if (retention == "one") Assert.Equal(inputs["file000.bin"], Assert.IsType<byte[]>(result.ReturnValue));
            foreach (var input in inputs)
                Assert.Equal(input.Value, delayed is not null ? delayed.ReadBytes("/outbox/" + input.Key) : mock!.ReadBytes("/outbox/" + input.Key));
            var actual = delayed is not null ? delayed.ReadText("/manifest.json") : mock!.ReadText("/manifest.json");
            Assert.Equal(expectedManifest, Encoding.UTF8.GetBytes(actual));
        }
    }
}
