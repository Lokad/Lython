using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Protocol storage uses conservative container capacity, not scalar coupons.
// Keep small ordinary regressions and preserve original long scales as stress.
public sealed class ProtocolSnapshotBudgetTests
{
    private const long SetAsymmetricBudgetBytes = 8192;
    private const long SetOrdinaryBudgetBytes = 262144;
    private const long DictAsymmetricBudgetBytes = 8192;
    private const long DictOrdinaryBudgetBytes = 262144;
    private const int ScanKeyCount = 64;
    private const int DictScanProbeCount = 128;
    private const int SetScanProbeCount = 128;

    private const string CustomKeyType = """
        class K:
            def __init__(self, v):
                self.v = v
            def __eq__(self, other):
                return isinstance(other, K) and self.v == other.v
            def __hash__(self):
                return self.v
        """;

    private const string DictAsymmetricSource = CustomKeyType + "\n" + """
        d = {}
        for i in range(512):
            d[i] = i
        for i in range(3):
            d[K(5120 + i)] = -i
        return len(d)
        """;

    private const string SetAsymmetricSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(512):
            s.add(i)
        for i in range(3):
            s.add(K(5120 + i))
        return len(s)
        """;

    private const string DictOrdinarySource = """
        d = {}
        for i in range(512):
            d[i] = i
        r = d.pop(2000000, None)
        caught = 0
        try:
            del d[3000000]
        except KeyError:
            caught = 456
        return [len(d), r, caught, 123 in d, d.get(511), len(d)]
        """;

    private const string SetOrdinarySource = """
        s = set()
        for i in range(512):
            s.add(i)
        s.remove(511)
        caught = 0
        try:
            s.remove(2000000)
        except KeyError:
            caught = 7
        return [len(s), caught, 123 in s, 2000000 in s]
        """;

    private const string ParitySource = CustomKeyType + "\n" + """
        d = {}
        d[K(1)] = 10
        d[K(1)] = 11
        d[K(2)] = 12
        r = [len(d), d[K(1)], d[K(2)], K(9) in d, d.get(K(9), -1)]
        del d[K(2)]
        r += [len(d), K(2) in d]
        s = set()
        s.add(K(1))
        s.add(K(1))
        s.add(K(2))
        r += [len(s), K(1) in s]
        s.remove(K(1))
        r += [len(s), K(1) in s]
        return r
        """;

    private const string DictBuildSource = CustomKeyType + "\n" + """
        d = {}
        for i in range(64):
            d[K(i)] = i
        return len(d)
        """;

    private const string DictScanSource = CustomKeyType + "\n" + """
        d = {}
        for i in range(64):
            d[K(i)] = i
        total = 0
        for i in range(128):
            total += d.get(K(5120 + i), -1)
        return [len(d), total]
        """;

    private const string SetBuildSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(64):
            s.add(K(i))
        return len(s)
        """;

    private const string SetScanSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(64):
            s.add(K(i))
        hits = 0
        for i in range(128):
            if K(5120 + i) in s:
                hits += 1
        return [len(s), hits]
        """;

    private static void AssertError(LythonExecutionResult result, string type, string fragment)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(fragment, result.Failure!.Message, StringComparison.Ordinal);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public void RetainedDictionaryPopulationRefusesAdmission()
    {
        using var script = Compile(DictAsymmetricSource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(DictAsymmetricBudgetBytes) };
        using var result = script.Run(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.DeniedReservationBytes > 0);
    }

    [Fact]
    public async Task RetainedDictionaryPopulationRefusesAdmissionAsync()
    {
        using var script = Compile(DictAsymmetricSource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(DictAsymmetricBudgetBytes) };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.DeniedReservationBytes > 0);
    }

    [Fact]
    public void DictAsymmetricFunded()
    {
        using var script = Compile(DictAsymmetricSource);
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(515), sync.ReturnValue);
    }

    [Fact]
    public async Task DictAsymmetricFundedAsync()
    {
        using var script = Compile(DictAsymmetricSource);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(515), result.ReturnValue);
    }

    [Fact]
    public void RetainedSetPopulationRefusesAdmission()
    {
        using var script = Compile(SetAsymmetricSource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SetAsymmetricBudgetBytes) };
        using var result = script.Run(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.DeniedReservationBytes > 0);
    }

    [Fact]
    public async Task RetainedSetPopulationRefusesAdmissionAsync()
    {
        using var script = Compile(SetAsymmetricSource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SetAsymmetricBudgetBytes) };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.DeniedReservationBytes > 0);
    }

    [Fact]
    public void SetAsymmetricFunded()
    {
        using var script = Compile(SetAsymmetricSource);
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(515), sync.ReturnValue);
    }

    [Fact]
    public async Task SetAsymmetricFundedAsync()
    {
        using var script = Compile(SetAsymmetricSource);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(515), result.ReturnValue);
    }

    [Fact]
    public void DictOrdinaryPathsPreserveBehavior()
    {
        using var script = Compile(DictOrdinarySource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(DictOrdinaryBudgetBytes) };
        using var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(512), null, new BigInteger(456), true, new BigInteger(511), new BigInteger(512) },
            result.ReturnValue);
    }

    [Fact]
    public async Task DictOrdinaryPathsPreserveBehaviorAsync()
    {
        using var script = Compile(DictOrdinarySource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(DictOrdinaryBudgetBytes) };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(512), null, new BigInteger(456), true, new BigInteger(511), new BigInteger(512) },
            result.ReturnValue);
    }

    [Fact]
    public void SetOrdinaryPathsPreserveBehavior()
    {
        using var script = Compile(SetOrdinarySource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SetOrdinaryBudgetBytes) };
        using var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(511), new BigInteger(7), true, false },
            result.ReturnValue);
    }

    [Fact]
    public async Task SetOrdinaryPathsPreserveBehaviorAsync()
    {
        using var script = Compile(SetOrdinarySource);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SetOrdinaryBudgetBytes) };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(511), new BigInteger(7), true, false },
            result.ReturnValue);
    }

    [Fact]
    public void FundedProtocolOpsMatchCpython()
    {
        using var script = Compile(ParitySource);
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(11), new BigInteger(12), false, new BigInteger(-1), new BigInteger(1), false, new BigInteger(2), true, new BigInteger(1), false },
            result.ReturnValue);
    }

    [Fact]
    public async Task FundedProtocolOpsMatchCpythonAsync()
    {
        using var script = Compile(ParitySource);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(11), new BigInteger(12), false, new BigInteger(-1), new BigInteger(1), false, new BigInteger(2), true, new BigInteger(1), false },
            result.ReturnValue);
    }

    [Fact]
    public void DictBuildKeepsAllKeys()
    {
        using var script = Compile(DictBuildSource);
        var options = new LythonRunOptions { };
        using var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public async Task DictBuildKeepsAllKeysAsync()
    {
        using var script = Compile(DictBuildSource);
        var options = new LythonRunOptions { };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public void DictScanFunded()
    {
        using var script = Compile(DictScanSource);
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(-DictScanProbeCount) },
            result.ReturnValue);
    }

    [Fact]
    public async Task DictScanFundedAsync()
    {
        using var script = Compile(DictScanSource);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(-DictScanProbeCount) },
            result.ReturnValue);
    }

    [Fact]
    public void SetBuildKeepsAllItems()
    {
        using var script = Compile(SetBuildSource);
        var options = new LythonRunOptions { };
        using var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public async Task SetBuildKeepsAllItemsAsync()
    {
        using var script = Compile(SetBuildSource);
        var options = new LythonRunOptions { };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public void SetScanFunded()
    {
        using var script = Compile(SetScanSource);
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public async Task SetScanFundedAsync()
    {
        using var script = Compile(SetScanSource);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(0) },
            result.ReturnValue);
    }
    [Theory]
    [Trait("Category", "Stress")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OriginalLargeProtocolPopulationAndScan(bool set, bool asynchronous)
    {
        foreach (var source in new[]
        {
            (set ? SetAsymmetricSource : DictAsymmetricSource).Replace("range(512)", "range(100000)"),
            (set ? SetScanSource : DictScanSource).Replace("range(64)", "range(3000)").Replace("range(128)", "range(18000)"),
        })
        {
            using var script = Compile(source);
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            if (source.Contains("range(100000)", StringComparison.Ordinal)) Assert.Equal(new BigInteger(100003), result.ReturnValue);
            else Assert.Equal(new List<object?> { new BigInteger(3000), new BigInteger(set ? 0 : -18000) }, result.ReturnValue);
        }
    }

}
