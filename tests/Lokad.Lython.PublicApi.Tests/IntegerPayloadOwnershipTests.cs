using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class IntegerPayloadOwnershipTests : LifetimeScenario
{

    private static string Nines(int count) => new string('9', count);

    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonRetainedPopulationDenies()
    {
        var payload = Nines(10001);
        var code = "import json\nxs = [json.loads(\"" + payload + "\") for _ in range(1000)]\nreturn len(xs)\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public async Task JsonRetainedPopulationDeniesAsync()
    {
        var payload = Nines(10001);
        var code = "import json\nxs = [json.loads(\"" + payload + "\") for _ in range(1000)]\nreturn len(xs)\n";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void JsonSingleHugeDeniesBeforeParsing()
    {
        var code = "import json\nreturn json.loads(\"" + Nines(10001) + "\")\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        // Digit-scale preflight denies before the parse allocates the limbs.
        Assert.True(result.PeakExecutionMemoryBytes < 4096, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void IntParseFundedExact()
    {
        var payload = Nines(101);
        var result = new LythonEngine().Run(
            "return [int(\"" + payload + "\") == int(\"" + payload + "\"), int(\"123\"), int(\"-0x10\", 16)]\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(123), new BigInteger(-16) }, result.ReturnValue);
    }

    [Fact]
    public async Task IntParseFundedExactAsync()
    {
        var payload = Nines(101);
        var result = await new LythonEngine().RunAsync(
            "return int(\"" + payload + "\") == int(\"" + payload + "\")\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
    }

    [Fact]
    public void IntParseHugeDeniesBeforeParsing()
    {
        var result = new LythonEngine().Run(
            "return int(\"" + Nines(10001) + "\")\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void GetRandBitsBounded()
    {
        var funded = new LythonEngine().Run("import random\nr = random.getrandbits(100000)\nreturn 2**99999 <= r < 2**100000\n", new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(true, funded.ReturnValue);
        var denied = new LythonEngine().Run(
            "import random\nreturn random.getrandbits(100000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) });
        AssertError(denied, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void EnumerateHugeStartBounded()
    {
        var funded = new LythonEngine().Run("it = iter([1])\n_, v = next(enumerate(it, 1 << 100000))\nreturn v\n", new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(1), funded.ReturnValue);
        var denied = new LythonEngine().Run(
            "it = iter([1])\n_, v = next(enumerate(it, 1 << 100000))\nreturn v\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) });
        AssertError(denied, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void HostGlobalPayloadCountedRelativeToBaseline()
    {
        var smallGlobals = new Dictionary<string, object?> { ["big"] = new BigInteger(7) };
        var bigGlobals = new Dictionary<string, object?> { ["big"] = BigInteger.Parse(Nines(100000)) };
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(256 * 1024 * 1024) };
        var small = new LythonEngine().Run("return big\n", new MockLythonHost(), smallGlobals);
        Assert.True(small.Success, small.Failure?.Message);
        var big = new LythonEngine().Run("return big\n", new MockLythonHost(), bigGlobals);
        Assert.True(big.Success, big.Failure?.Message);
        // The ~41KB magnitude must appear in the accounted peak, not ride free.
        Assert.True(big.PeakExecutionMemoryBytes - small.PeakExecutionMemoryBytes >= 40000,
            $"small={small.PeakExecutionMemoryBytes} big={big.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void HostGlobalHugeDenies()
    {
        var bigGlobals = new Dictionary<string, object?> { ["big"] = BigInteger.Parse(Nines(100000)) };
        var result = new LythonEngine().Run(
            "return big\n",
            new MockLythonHost(),
            new LythonRunOptions { Globals = bigGlobals, MemoryBudget = new LythonMemoryBudget(32768) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public Task JsonMagnitudeBatchesReturnSharedAccounting()
        => AssertBatchCompletes("import json\ns = \"7\" * 5000\nfor _ in range(8):\n json.loads(s)\nreturn 0\n", "0", 1024 * 1024);

    [Fact]
    public void RetainedLoadsRejectUnderTheSameFixedCap()
    {
        const long cap = 1024 * 1024;
        var code = "import json\ns = \"7\" * 5000\nkept = []\nfor _ in range(2000):\n kept.append(json.loads(s))\nreturn 0\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, cap);
    }

    [Fact]
    public void AliasedBoxesKeepPythonIdentityWithConservativeStorage()
    {
        // Keep a major magnitude, without making alias identity depend on the
        // old parser's unfunded UTF-16/computation scratch at this fixed cap.
        // Large-input preallocation refusals have separate regression coverage.
        var payload = Nines(101);
        var result = new LythonEngine().Run(
            "x = int(\"" + payload + "\")\nxs = [x, x]\nreturn [len(xs), xs[0] is xs[1]]\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(30000) });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), true }, result.ReturnValue);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 30000);
    }

    [Fact]
    public void RangeBoundLimbsOwnedAtConstruction()
    {
        // The lazy range stays constructible, but its retained bound limbs are
        // charged: the peak must cover ~37KB of payload, not just the shell.
        var result = new LythonEngine().Run(
            "r = range(1 << 100000, (1 << 100000) + 1000)\nreturn len(r)\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(1000), result.ReturnValue);
        Assert.True(result.PeakExecutionMemoryBytes >= 30000, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void RangeBoundLimbsDenyAbsurdScale()
    {
        var result = new LythonEngine().Run(
            "r = range(1 << 100000, (1 << 100000) + 1000)\nreturn len(r)\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }
}
