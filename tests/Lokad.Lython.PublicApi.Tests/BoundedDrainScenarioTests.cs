using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R02: fixed-shape consumers must not raw-drain arbitrary iterables. Paired math
// inputs stream lockstep, struct_time bounds arity before materialization, and
// adjacent drain sites stay governed. Observable behavior lives here; every case
// runs sync (executable) and async (lowered) unless host-gated.
public sealed class BoundedDrainScenarioTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DistFundedEquivalence()
    {
        const string code = """
import math
return [math.dist([0, 0], [3, 4]), math.dist([], []), math.dist((x for x in [0, 0]), (y for y in [3, 4]))]
""";
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { 5.0, 0.0, 5.0 }, sync.ReturnValue);
    }

    [Fact]
    public async Task DistFundedEquivalenceAsync()
    {
        const string code = """
import math
return [math.dist([0, 0], [3, 4]), math.dist([], []), math.dist((x for x in [0, 0]), (y for y in [3, 4]))]
""";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { 5.0, 0.0, 5.0 }, result.ReturnValue);
    }

    [Fact]
    public void DistLengthCheckPrecedesElementErrors()
    {
        // Sized inputs report the dimension mismatch like CPython even when an
        // early element would fail conversion.
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist(['x', 1], [1, 2, 3])\n",
            new MockLythonHost());
        AssertError(result, "ValueError", "same number of dimensions");
    }

    [Fact]
    public void DistElementErrorNamesFunction()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist([1, 'x'], [1, 2])\n",
            new MockLythonHost());
        AssertError(result, "TypeError", "math.dist");
    }

    [Fact]
    public void DistHugeInputsDenyUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void DistHugeInputsDenyUnderTinyCollectionLimit()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 10 });
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DistHugeInputsDenyUnderTinyStepBudget()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DistRespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cts.Token });
        Assert.False(result.Success);
        Assert.Contains("execution canceled", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DistUnequalInfiniteFailsFast()
    {
        var result = new LythonEngine().Run(
            "import itertools, math\nreturn math.dist(itertools.count(), range(5))\n",
            new MockLythonHost());
        AssertError(result, "ValueError", "same number of dimensions");
    }

    [Fact]
    public void DistInfinitePairDeniesOnSteps()
    {
        var result = new LythonEngine().Run(
            "import itertools, math\nreturn math.dist(itertools.count(), itertools.count())\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 1000 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DistDelayedAsyncInputsSuspendThroughHost()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            with open("/p.txt") as f1, open("/q.txt") as f2:
                d = math.dist(map(float, f1), map(float, f2))
            return d
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/p.txt", "0.0\n0.0\n");
        host.SeedFile("/q.txt", "3.0\n4.0\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(5.0, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void DistNumericalAccuracy()
    {
        var result = new LythonEngine().Run(
            """
import math
return [math.dist([float('inf')], [1]), math.dist([1e308], [-1e308]), math.dist([1e-300], [0]), math.dist([1, 2, 3], [1, 2, 3])]
""",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(double.PositiveInfinity, values[0]);
        Assert.Equal(double.PositiveInfinity, values[1]);
        Assert.Equal(1e-300, values[2]);
        Assert.Equal(0.0, values[3]);
    }

    [Fact]
    public void DistNaNPayload()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.dist([float('nan')], [1])\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(double.IsNaN((double)result.ReturnValue!));
    }

    [Fact]
    public void SumProdFundedEquivalence()
    {
        const string code = """
import math
return [math.sumprod([1, 2, 3], [4, 5, 6]), math.sumprod([1.0, 2.0], [3, 4]), math.sumprod((x for x in [1, 2]), (y for y in [3, 4])), math.sumprod([], [])]
""";
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(32), 11.0, new BigInteger(11), new BigInteger(0) }, sync.ReturnValue);
    }

    [Fact]
    public async Task SumProdFundedEquivalenceAsync()
    {
        const string code = """
import math
return [math.sumprod([1, 2, 3], [4, 5, 6]), math.sumprod([1.0, 2.0], [3, 4]), math.sumprod((x for x in [1, 2]), (y for y in [3, 4])), math.sumprod([], [])]
""";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(32), 11.0, new BigInteger(11), new BigInteger(0) }, result.ReturnValue);
    }

    [Fact]
    public void SumProdLengthMismatch()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.sumprod([1, 2], [3])\n",
            new MockLythonHost());
        AssertError(result, "ValueError", "same length");
    }

    [Fact]
    public void SumProdElementError()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.sumprod([1, 'x'], [3, 4])\n",
            new MockLythonHost());
        AssertError(result, "TypeError", "real numbers");
    }

    [Fact]
    public void SumProdHugeDeniedByCollectionLimit()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.sumprod(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 10 });
        Assert.False(result.Success);
        Assert.Contains("maximum collection size exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SumProdHugeDeniedBySteps()
    {
        var result = new LythonEngine().Run(
            "import math\nreturn math.sumprod(range(200000), range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SumProdInfinitePairDeniesOnSteps()
    {
        var result = new LythonEngine().Run(
            "import itertools, math\nreturn math.sumprod(itertools.count(), itertools.count())\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 1000 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SumProdDelayedAsyncInputsSuspendThroughHost()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            with open("/p.txt") as f1, open("/q.txt") as f2:
                t = math.sumprod(map(float, f1), map(float, f2))
            return t
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/p.txt", "1.0\n2.0\n");
        host.SeedFile("/q.txt", "3.0\n4.0\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(11.0, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void StructTimeArityPreserved()
    {
        var shortResult = new LythonEngine().Run("import time\nreturn time.struct_time((1, 2))\n", new MockLythonHost());
        AssertError(shortResult, "TypeError", "at least 9-sequence (2-sequence given)");
        var longResult = new LythonEngine().Run("import time\nreturn time.struct_time(tuple(range(12)))\n", new MockLythonHost());
        AssertError(longResult, "TypeError", "at most 11-sequence (12-sequence given)");
    }

    [Fact]
    public async Task StructTimeArityPreservedAsync()
    {
        var shortResult = await new LythonEngine().RunAsync("import time\nreturn time.struct_time((1, 2))\n", new MockLythonHost());
        AssertError(shortResult, "TypeError", "at least 9-sequence (2-sequence given)");
        var longResult = await new LythonEngine().RunAsync("import time\nreturn time.struct_time(tuple(range(12)))\n", new MockLythonHost());
        AssertError(longResult, "TypeError", "at most 11-sequence (12-sequence given)");
    }

    [Fact]
    public void StructTimeHugeReportsExactArityWithoutMaterializing()
    {
        var result = new LythonEngine().Run(
            "import time\nreturn time.struct_time(range(200000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "TypeError", "at most 11-sequence (200000-sequence given)");
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void StructTimeInfiniteDeniedByCollectionLimit()
    {
        var result = new LythonEngine().Run(
            "import itertools, time\nreturn time.struct_time(itertools.count())\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 20 });
        Assert.False(result.Success);
        Assert.Contains("maximum collection size exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StructTimeAcceptsGenerator()
    {
        var result = new LythonEngine().Run(
            "import time\nreturn time.struct_time(x for x in range(9))[0]\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(0), result.ReturnValue);
    }

    [Fact]
    public void PopenPassFdsHugeRangeFailsWithoutDraining()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var result = new LythonEngine().Run(
            "import subprocess\nreturn subprocess.Popen(['t'], pass_fds=range(200000))\n",
            host);
        AssertError(result, "NotImplementedError", "pass_fds");
    }

    [Fact]
    public void JsonSeparatorsOversizedFailsFast()
    {
        // Built dynamically so the static separators check cannot see it: the
        // runtime count-first check fails without copying 5002 items.
        var result = new LythonEngine().Run(
            "import json\nseps = [',', ':']\nseps.extend(['x'] * 5000)\nreturn json.dumps({'a': 1}, separators=seps)\n",
            new MockLythonHost());
        AssertError(result, "TypeError", "two-item tuple/list");
    }

    [Fact]
    public void JsonSeparatorsOversizedRangeFailsFast()
    {
        var result = new LythonEngine().Run(
            "import json\nreturn json.dumps({'a': 1}, separators=tuple(range(200000)))\n",
            new MockLythonHost());
        AssertError(result, "TypeError", "two-item tuple/list");
    }
}
