using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R06: exact integer work (factorial/comb/perm/lcm/isqrt/prod) and fsum deny
// before computing under tiny budgets and stay responsive to work budgets and
// cancellation mid-loop. Exact arithmetic is never approximated: funded runs
// match CPython bit-for-bit in both execution paths.
public sealed class BoundedNumericWorkTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FundedExactResults()
    {
        const string code = """
import math
return [math.factorial(0), math.factorial(1), math.factorial(20), math.comb(52, 5), math.comb(20, 10),
    math.perm(20, 3), math.isqrt(17), math.isqrt(10**30), math.lcm(12, 18), math.gcd(12, 18),
    math.prod([1, 2, 3, 4]), math.prod([], start=7), math.fsum([0.1, 0.2, 0.3])]
""";
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new BigInteger(1), new BigInteger(1), new BigInteger(2432902008176640000),
                new BigInteger(2598960), new BigInteger(184756), new BigInteger(6840),
                new BigInteger(4), new BigInteger(1000000000000000), new BigInteger(36), new BigInteger(6),
                new BigInteger(24), new BigInteger(7), 0.6,
            },
            sync.ReturnValue);
    }

    [Fact]
    public async Task FundedExactResultsAsync()
    {
        const string code = """
import math
return [math.factorial(20), math.comb(52, 5), math.perm(20, 3), math.isqrt(17),
    math.lcm(12, 18), math.prod([1, 2, 3, 4]), math.fsum([0.1, 0.2, 0.3])]
""";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new BigInteger(2432902008176640000), new BigInteger(2598960), new BigInteger(6840),
                new BigInteger(4), new BigInteger(36), new BigInteger(24), 0.6,
            },
            result.ReturnValue);
    }

    [Fact]
    public void FactorialHundredMatchesCpython()
    {
        var result = new LythonEngine().Run("import math\nreturn str(math.factorial(100))\n", new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "93326215443944152681699238856266700490715968264381621468592963895217599993229915608941463976156518286253697920827223758251185210916864000000000000000000000000",
            result.ReturnValue);
    }

    [Fact]
    public void SqrtAndLcmLargeMagnitudesMatchCpython()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn [math.isqrt(10**20000) == 10**10000, math.lcm(10**30, 10**30 + 1) == 10**30 * (10**30 + 1)]\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, true }, result.ReturnValue);
    }

    [Fact]
    public void FactorialDeniesBeforeComputingUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.factorial(20000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        // Preflight denies before any multiply traffic: the peak stays far below
        // the terminal result charge the old code reached after computing it.
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void DeniedFactorialAllocatesAlmostNothing()
    {
        const string code = "import math\nreturn math.factorial(20000)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        _ = new LythonEngine().Run(code, new MockLythonHost(), options);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = new LythonEngine().Run(code, new MockLythonHost(), options);
        var allocated = (long)GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(result.Success);
        // Pre-fix traffic was ~300 MB of BigInteger reallocations; the denial now
        // lands before the loop with five orders of magnitude to spare.
        Assert.True(allocated < 10_000_000, $"allocated {allocated}");
    }

    [Fact]
    public void FactorialDeniesUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.factorial(20000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FactorialRespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = new LythonEngine().Run(
            "import math\nreturn math.factorial(20000)\n",
            new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cts.Token });
        Assert.False(result.Success);
        Assert.Contains("execution canceled", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CombDeniesUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.comb(20000, 10000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PermSmallIterationLargeMagnitudeDeniesUnderTinyMemory()
    {
        // Two iterations, ~200k-bit result: the iteration cap alone is no bound.
        var result = new LythonEngine().Run(
            "import math\nreturn math.perm(10**30000, 2)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void PermDeniesUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.perm(20000, 10000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsqrtHugeDeniesUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.isqrt(10**100000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void LcmHugePairDeniesUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.lcm(10**50000, 10**50000 + 1)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void ProdHugeMagnitudesDenyUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.prod([10**30000, 10**30000])\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void ProdLongScanDeniesUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.prod(range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FsumLongScanDeniesUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.fsum(float(i) for i in range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProdDelayedAsyncInputsSuspendThroughHost()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            with open("/v.txt") as f:
                t = math.prod(map(int, f))
            return t
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.txt", "2\n3\n4\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(24), result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task FsumDelayedAsyncInputsSuspendThroughHost()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            with open("/v.txt") as f:
                t = math.fsum(map(float, f))
            return t
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.txt", "0.1\n0.2\n0.3\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0.6, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
