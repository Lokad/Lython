using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N10 part 1: protocol-table side/store snapshots preflight before copying,
// long collision/non-collision scans honor the step budget, and ordinary-key
// paths stay uncharged. Funded behavior matches CPython in both modes.
public sealed class ProtocolSnapshotBudgetTests
{
    // The asymmetric scripts below hold 100000 ordinary entries when the
    // first protocol insert arrives, so the store snapshot dwarfs every
    // population transient: dict 32 + 16 per entry, set 24 + 8 per entry.
    // N06: set populations also adopt one 64 B coupon per distinct small int
    // (~6.4 MB for 100000), so the set scripts moved to their own budgets:
    // asymmetric 9.5 MB (adds peak ~9.01 MB, snapshot 800024 still binds),
    // ordinary 10 MiB (peak ~9.01 MB, lookups add nothing).
    private const long DictBudgetBytes = 4_000_000;
    private const long SetAsymmetricBudgetBytes = 9_500_000;
    private const long SetOrdinaryBudgetBytes = 10_485_760;
    private const long DictStoreSnapshotBytes = 1600032;
    private const long SetStoreSnapshotBytes = 800024;

    private const int ScanKeyCount = 3000;
    private const int ScanProbeCount = 6000;
    private const int DictScanStepBudget = 600_000;
    private const int SetScanStepBudget = 400_000;

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
        for i in range(100000):
            d[i] = i
        for i in range(3):
            d[K(1000000 + i)] = -i
        return len(d)
        """;

    private const string SetAsymmetricSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(100000):
            s.add(i)
        for i in range(3):
            s.add(K(1000000 + i))
        return len(s)
        """;

    private const string DictOrdinarySource = """
        d = {}
        for i in range(100000):
            d[i] = i
        r = d.pop(2000000, None)
        caught = 0
        try:
            del d[3000000]
        except KeyError:
            caught = 456
        return [len(d), r, caught, 123 in d, d.get(99999), len(d)]
        """;

    private const string SetOrdinarySource = """
        s = set()
        for i in range(100000):
            s.add(i)
        s.remove(99999)
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
        for i in range(3000):
            d[K(i)] = i
        return len(d)
        """;

    private const string DictScanSource = CustomKeyType + "\n" + """
        d = {}
        for i in range(3000):
            d[K(i)] = i
        total = 0
        for i in range(6000):
            total += d.get(K(1000000 + i), -1)
        return [len(d), total]
        """;

    private const string SetBuildSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(3000):
            s.add(K(i))
        return len(s)
        """;

    private const string SetScanSource = CustomKeyType + "\n" + """
        s = set()
        for i in range(3000):
            s.add(K(i))
        hits = 0
        for i in range(6000):
            if K(1000000 + i) in s:
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
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public void DictStoreSnapshotDeniesBeforeCopying()
    {
        var script = Compile(DictAsymmetricSource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DictBudgetBytes };
        var result = script.Run(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        Assert.Equal(DictStoreSnapshotBytes, result.DeniedReservationBytes);
    }

    [Fact]
    public async Task DictStoreSnapshotDeniesBeforeCopyingAsync()
    {
        var script = Compile(DictAsymmetricSource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DictBudgetBytes };
        var result = await script.RunAsync(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        Assert.Equal(DictStoreSnapshotBytes, result.DeniedReservationBytes);
    }

    [Fact]
    public void DictAsymmetricFunded()
    {
        var script = Compile(DictAsymmetricSource);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(100003), sync.ReturnValue);
    }

    [Fact]
    public async Task DictAsymmetricFundedAsync()
    {
        var script = Compile(DictAsymmetricSource);
        var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(100003), result.ReturnValue);
    }

    [Fact]
    public void SetStoreSnapshotDeniesBeforeCopying()
    {
        var script = Compile(SetAsymmetricSource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SetAsymmetricBudgetBytes };
        var result = script.Run(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        Assert.Equal(SetStoreSnapshotBytes, result.DeniedReservationBytes);
    }

    [Fact]
    public async Task SetStoreSnapshotDeniesBeforeCopyingAsync()
    {
        var script = Compile(SetAsymmetricSource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SetAsymmetricBudgetBytes };
        var result = await script.RunAsync(new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        Assert.Equal(SetStoreSnapshotBytes, result.DeniedReservationBytes);
    }

    [Fact]
    public void SetAsymmetricFunded()
    {
        var script = Compile(SetAsymmetricSource);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(100003), sync.ReturnValue);
    }

    [Fact]
    public async Task SetAsymmetricFundedAsync()
    {
        var script = Compile(SetAsymmetricSource);
        var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(100003), result.ReturnValue);
    }

    [Fact]
    public void DictOrdinaryPathsStayUncharged()
    {
        var script = Compile(DictOrdinarySource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DictBudgetBytes };
        var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(100000), null, new BigInteger(456), true, new BigInteger(99999), new BigInteger(100000) },
            result.ReturnValue);
    }

    [Fact]
    public async Task DictOrdinaryPathsStayUnchargedAsync()
    {
        var script = Compile(DictOrdinarySource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DictBudgetBytes };
        var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(100000), null, new BigInteger(456), true, new BigInteger(99999), new BigInteger(100000) },
            result.ReturnValue);
    }

    [Fact]
    public void SetOrdinaryPathsStayUncharged()
    {
        var script = Compile(SetOrdinarySource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SetOrdinaryBudgetBytes };
        var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(99999), new BigInteger(7), true, false },
            result.ReturnValue);
    }

    [Fact]
    public async Task SetOrdinaryPathsStayUnchargedAsync()
    {
        var script = Compile(SetOrdinarySource);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SetOrdinaryBudgetBytes };
        var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(99999), new BigInteger(7), true, false },
            result.ReturnValue);
    }

    [Fact]
    public void FundedProtocolOpsMatchCpython()
    {
        var script = Compile(ParitySource);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(11), new BigInteger(12), false, new BigInteger(-1), new BigInteger(1), false, new BigInteger(2), true, new BigInteger(1), false },
            result.ReturnValue);
    }

    [Fact]
    public async Task FundedProtocolOpsMatchCpythonAsync()
    {
        var script = Compile(ParitySource);
        var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(11), new BigInteger(12), false, new BigInteger(-1), new BigInteger(1), false, new BigInteger(2), true, new BigInteger(1), false },
            result.ReturnValue);
    }

    [Fact]
    public void DictBuildFitsScanStepBudget()
    {
        var script = Compile(DictBuildSource);
        var options = new LythonRunOptions { MaxExecutionSteps = DictScanStepBudget };
        var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public async Task DictBuildFitsScanStepBudgetAsync()
    {
        var script = Compile(DictBuildSource);
        var options = new LythonRunOptions { MaxExecutionSteps = DictScanStepBudget };
        var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public void DictScansRespectStepBudget()
    {
        var script = Compile(DictScanSource);
        var options = new LythonRunOptions { MaxExecutionSteps = DictScanStepBudget };
        AssertError(script.Run(new MockLythonHost(), options), "RuntimeError", "maximum execution step count exceeded");
    }

    [Fact]
    public async Task DictScansRespectStepBudgetAsync()
    {
        var script = Compile(DictScanSource);
        var options = new LythonRunOptions { MaxExecutionSteps = DictScanStepBudget };
        AssertError(await script.RunAsync(new MockLythonHost(), options), "RuntimeError", "maximum execution step count exceeded");
    }

    [Fact]
    public void DictScanFunded()
    {
        var script = Compile(DictScanSource);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(-ScanProbeCount) },
            result.ReturnValue);
    }

    [Fact]
    public async Task DictScanFundedAsync()
    {
        var script = Compile(DictScanSource);
        var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(-ScanProbeCount) },
            result.ReturnValue);
    }

    [Fact]
    public void SetBuildFitsScanStepBudget()
    {
        var script = Compile(SetBuildSource);
        var options = new LythonRunOptions { MaxExecutionSteps = SetScanStepBudget };
        var result = script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public async Task SetBuildFitsScanStepBudgetAsync()
    {
        var script = Compile(SetBuildSource);
        var options = new LythonRunOptions { MaxExecutionSteps = SetScanStepBudget };
        var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(ScanKeyCount), result.ReturnValue);
    }

    [Fact]
    public void SetScansRespectStepBudget()
    {
        var script = Compile(SetScanSource);
        var options = new LythonRunOptions { MaxExecutionSteps = SetScanStepBudget };
        AssertError(script.Run(new MockLythonHost(), options), "RuntimeError", "maximum execution step count exceeded");
    }

    [Fact]
    public async Task SetScansRespectStepBudgetAsync()
    {
        var script = Compile(SetScanSource);
        var options = new LythonRunOptions { MaxExecutionSteps = SetScanStepBudget };
        AssertError(await script.RunAsync(new MockLythonHost(), options), "RuntimeError", "maximum execution step count exceeded");
    }

    [Fact]
    public void SetScanFunded()
    {
        var script = Compile(SetScanSource);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public async Task SetScanFundedAsync()
    {
        var script = Compile(SetScanSource);
        var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(ScanKeyCount), new BigInteger(0) },
            result.ReturnValue);
    }
}
