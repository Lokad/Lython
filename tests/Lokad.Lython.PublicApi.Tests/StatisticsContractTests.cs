using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StatisticsContractTests
{
    private static IEnumerable<object[]> Read(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StatisticsContracts", file);
        foreach (var item in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("output").GetString()!];
    }

    public static IEnumerable<object[]> Moments() => Read("moments.json");
    public static IEnumerable<object[]> Records() => Read("records.json");
    public static IEnumerable<object[]> Distributions() => Read("distributions.json");
    public static IEnumerable<object[]> Hosted() => Read("hosted.json");

    [Theory, MemberData(nameof(Moments)), MemberData(nameof(Records)), MemberData(nameof(Distributions))]
    public async Task StatisticalResultsMatchIndependentPythonGoldens(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(Hosted))]
    public async Task StatisticalProtocolsActuallyAwaitIO(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var host = new MockLythonHost(); host.SeedFile("value.txt", "1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("value.txt", "1");
            using (var result = mode == "sync" ? script.Run(host, new LythonRunOptions { MemoryBudget = budget })
                : await script.RunAsync(mode == "delayed" ? delayed : host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task SampleAcquisitionActuallySuspendsAndReturnsItsOwner()
    {
        const string source = """
            import statistics
            def data():
                yield 10000000000000000
                with open('/value') as f:
                    yield 10000000000000000+int(f.read())
                yield 10000000000000002
            d=statistics.NormalDist.from_samples(data())
            return [d.stdev,'x'*8192]
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1048576);
        var host = new DelayedLythonHost(); host.SeedFile("/value", "1");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
            Assert.Equal(1.0, values[0]);
            Assert.Equal(new string('x', 8192), values[1]);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("float"), InlineData("index")]
    public async Task SuspendedConversionCancellationReturnsItsOwners(string protocol)
    {
        var expression = protocol == "float" ? "statistics.NormalDist(Value())" : "statistics.NormalDist().samples(Value())";
        using var script = new LythonEngine().Compile("import statistics\nclass Value:\n def __" + protocol
            + "__(self):\n  with open('/input') as f:value=f.read(1)\n  return " + (protocol == "float" ? "1.0" : "1") + "\n" + expression);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2097152);
        var host = new DelayedLythonHost(); host.SeedFile("/input", "1");
        var entered = host.PauseReadUntilCancellation("/input");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(budget.ReservedBytes > 0);
            cancellation.Cancel();
            using var result = await pending;
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally { cancellation.Cancel(); using var settled = await pending; }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("float"), InlineData("index")]
    public async Task RefusalInConversionCannotBeSuppressed(string protocol)
    {
        var expression = protocol == "float" ? "statistics.NormalDist(Value())" : "statistics.NormalDist().samples(Value())";
        using var script = new LythonEngine().Compile("import statistics\nclass Value:\n def __" + protocol
            + "__(self):\n  with open('/input') as f:value=f.read(1)\n  value='x'*4096\n  return "
            + (protocol == "float" ? "1.0" : "1") + "\ntry:" + expression + "\nexcept MemoryError:pass\nprint('unreachable')");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2097152);
        ILythonMemoryReservation? padding = null;
        var host = new DelayedLythonHost(); host.SeedFile("/input", "1");
        host.OnTextReadCompleted = () => Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 1024, out padding));
        try
        {
            using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.NotNull(padding);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("unreachable", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(padding!.Bytes, budget.ReservedBytes);
        }
        finally { padding?.Dispose(); }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
