using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R03a: fresh heap magnitudes produced outside ordinary arithmetic own their
// payload at the cold production boundary (parses, random draws, enumerated
// indices, copied host inputs, range bound limbs) and pool-track the box so
// dropped values reclaim on sweep. Hot arithmetic and store-level retention
// keep the existing policy for R11/R16; range yields and GetSubscript stay
// unowned until the shared ownership design lands.
public sealed class IntegerPayloadOwnershipTests
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
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public async Task JsonRetainedPopulationDeniesAsync()
    {
        var payload = Nines(10001);
        var code = "import json\nxs = [json.loads(\"" + payload + "\") for _ in range(1000)]\nreturn len(xs)\n";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void JsonSingleHugeDeniesBeforeParsing()
    {
        var code = "import json\nreturn json.loads(\"" + Nines(10001) + "\")\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
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
            new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
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
            new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        AssertError(denied, "MemoryError", "execution memory budget exceeded");
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
            new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        AssertError(denied, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void HostGlobalPayloadCountedRelativeToBaseline()
    {
        var smallGlobals = new Dictionary<string, object?> { ["big"] = new BigInteger(7) };
        var bigGlobals = new Dictionary<string, object?> { ["big"] = BigInteger.Parse(Nines(100000)) };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 256 * 1024 * 1024 };
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
            new LythonRunOptions { Globals = bigGlobals, MaxExecutionMemoryBytes = 32768 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DiscardedLoadsReclaimWithoutFalseDenial()
    {
        // Dropped magnitudes release through the pool on sweep: twenty thousand
        // discarded loads must not accumulate permanent charges (the R11 shape).
        var code = "import json\ns = \"7\" * 5000\nfor _ in range(2000):\n json.loads(s)\nreturn 0\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 8 * 1024 * 1024 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(result.PeakExecutionMemoryBytes < 4 * 1024 * 1024, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void AliasedBoxesChargeOnce()
    {
        var payload = Nines(10001);
        var result = new LythonEngine().Run(
            "x = int(\"" + payload + "\")\nxs = [x, x]\nreturn len(xs)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 30000 });
        Assert.True(result.Success, result.Failure?.Message);
        // One ~4.2KB payload plus entry/backing: far below twice that.
        Assert.True(result.PeakExecutionMemoryBytes < 10000, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void RangeBoundLimbsOwnedAtConstruction()
    {
        // The lazy range stays constructible, but its retained bound limbs are
        // charged: the peak must cover ~37KB of payload, not just the shell.
        var result = new LythonEngine().Run(
            "r = range(1 << 100000, (1 << 100000) + 1000)\nreturn len(r)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
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
            new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }
}
