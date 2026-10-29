using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DifflibContractTests
{
    private static IEnumerable<object[]> Read(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DifflibContracts", file);
        foreach (var item in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("output").GetString()!];
    }
    public static IEnumerable<object[]> Cases() => Read("cases.json");
    public static IEnumerable<object[]> Hosted() => Read("hosted.json");

    [Theory, MemberData(nameof(Cases))]
    public async Task ResultsAndEventsMatchIndependentPythonGoldens(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join(";", script.Diagnostics.Select(x => x.Message)));
        var budget = new LythonMemoryBudget(2097152);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(Hosted))]
    public async Task ComparisonAndTruthActuallyAwaitIO(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(2097152);
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

    private const string ComparisonSource = """
        import difflib
        class Item:
            def __hash__(self): return 1
            def __eq__(self, other):
                with open('/input') as f: value=f.read(1)
                return value == '1'
        s=difflib.SequenceMatcher(lambda x: True, [Item()], [Item()])
        """;

    [Theory, InlineData("ratio"), InlineData("find_longest_match")]
    public async Task SuspendedComparisonCancellationAwaitsCleanup(string method)
    {
        using var script = new LythonEngine().Compile(ComparisonSource + "\ns." + method + "()");
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

    [Fact]
    public async Task RefusedComparisonAdmissionCannotBeSuppressed()
    {
        var source = ComparisonSource.Replace("return value == '1'", "value='x'*4096\n        return True")
            + "\ntry:s.ratio()\nexcept MemoryError:pass\nprint('unreachable')";
        using var script = new LythonEngine().Compile(source);
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

    [Theory, InlineData(false), InlineData(true)]
    public async Task RetainedMatchesAndCachesShareBudgetWithoutEarlyRefund(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import difflib\ns=difflib.SequenceMatcher(None,'xab','yab')\nreturn [s.find_longest_match(),s.get_matching_blocks(),s.b2j]");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1048576);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var first = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > 0);
            using (var second = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > retained);
                Assert.Equal(3, Assert.IsType<List<object?>>(second.ReturnValue).Count);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            Assert.Equal(3, Assert.IsType<List<object?>>(first.ReturnValue).Count);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using var fresh = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(fresh.Success, fresh.Failure?.Message);
        fresh.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
