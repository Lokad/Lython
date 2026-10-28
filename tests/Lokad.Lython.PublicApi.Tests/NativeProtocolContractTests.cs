using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeProtocolContractTests
{
    private static IEnumerable<object[]> Cases(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NativeProtocols", file);
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!,
                row.GetProperty("output").GetString()!, row.TryGetProperty("budget", out var budget) ? budget.GetInt64() : 2097152L];
    }

    // Outputs frozen independently with CPython 3.13.2; random values are
    // checked by their domain, not by CPython's different generator sequence.
    public static IEnumerable<object[]> PureCases() => Cases("pure.json");
    public static IEnumerable<object[]> HostedCases() => Cases("hosted.json");

    [Theory, MemberData(nameof(PureCases))]
    public async Task NativeProtocolsMatchIndependentGoldens(string name, string source, string output, long capacity)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(capacity);
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

    [Theory, MemberData(nameof(HostedCases))]
    public async Task PreparationAndTruthActuallySuspend(string name, string source, string output, long capacity)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(capacity);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/input", "alpha");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/input", "alpha");
            using (var result = mode == "sync" ? script.Run(immediate, new LythonRunOptions { MemoryBudget = budget })
                : await script.RunAsync(mode == "delayed" ? delayed : immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task CancellationInterruptsAnActiveNonallocatingRejectionLoop(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import random
            random.vonmisesvariate(0, 1)
            kappa = float('nan')
            print('ready')
            random.vonmisesvariate(0, kappa)
            print('unreachable')
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(65536);
        using var cancellation = new CancellationTokenSource();
        var started = 0;
        var host = new MockLythonHost { OnStandardOutputWrite = () =>
        {
            if (Interlocked.Exchange(ref started, 1) == 0) cancellation.CancelAfter(25);
        }};
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token }))
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.DoesNotContain("unreachable", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("descriptor"), InlineData("truth"), InlineData("index")]
    public async Task SuspendedPreparationCancellationUnwindsItsOwners(string operation)
    {
        var source = operation switch
        {
            "descriptor" => "class D:\n def __get__(self,obj,owner):\n  with open('/input') as f:f.read(1)\n  return lambda *a:False\nclass C:\n __enter__=D()\n def __exit__(self,*a):pass\nwith C():pass",
            "truth" => "class F:\n def __bool__(self):\n  with open('/input') as f:f.read(1)\n  return False\nclass C:\n def __enter__(self):return self\n def __exit__(self,*a):return F()\nwith C():raise ValueError('body')",
            _ => "import random\nclass I:\n def __index__(self):\n  with open('/input') as f:return len(f.read(1))\nrandom.randrange(I())",
        };
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2097152);
        var host = new DelayedLythonHost(); host.SeedFile("/input", "alpha");
        var entered = host.PauseReadUntilCancellation("/input");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await entered.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(budget.ReservedBytes > 0);
        cancellation.Cancel();
        using (var result = await pending)
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("descriptor"), InlineData("truth"), InlineData("index")]
    public async Task RefusedProtocolStorageRemainsTerminal(string operation)
    {
        var callback = "with open('/input') as f:f.read(1)\n  return 'x'*4096";
        var source = operation switch
        {
            "descriptor" => "class D:\n def __get__(self,obj,owner):\n  " + callback + "\nclass C:\n __enter__=D()\n def __exit__(self,*a):pass\nwith C():pass",
            "truth" => "class F:\n def __bool__(self):\n  " + callback + "\nclass C:\n def __enter__(self):return self\n def __exit__(self,*a):return F()\nwith C():raise ValueError('body')",
            _ => "import random\nclass I:\n def __index__(self):\n  " + callback + "\nrandom.randrange(I())",
        };
        using var script = new LythonEngine().Compile("try:\n" + string.Join("\n", source.Split('\n').Select(line => " " + line))
            + "\nexcept MemoryError:pass\nprint('unreachable')");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2097152);
        ILythonMemoryReservation? padding = null;
        var host = new DelayedLythonHost(); host.SeedFile("/input", "alpha");
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

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task WideSelectionsRetainFundingUntilAllResultOwnersEnd(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import random\nvalue=random.randrange(2**1024)\nreturn [value,value]");
        var budget = new LythonMemoryBudget(65536);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var first = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        try
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > 0);
            using (var second = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        finally { first.Dispose(); }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
