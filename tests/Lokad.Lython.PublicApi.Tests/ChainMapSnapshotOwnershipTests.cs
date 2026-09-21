using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R05: merged key snapshots escaping through iterators own their lifetime.
// Retained iterators stay charged (and deny), abandoned ones release on sweep,
// partial iterators stay independent, and scalar len/membership never build
// retained snapshots.
public sealed class ChainMapSnapshotOwnershipTests
{
    private const string ThousandKeys = "{i: i for i in range(1000)}";

    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedIteratorsDeny()
    {
        var code = "import collections\nc = collections.ChainMap(" + ThousandKeys + ")\nits = [iter(c) for _ in range(300)]\nreturn len(its)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = new LythonEngine().Run(code, new MockLythonHost(), options);
        AssertError(sync, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public async Task RetainedIteratorsDenyAsync()
    {
        var code = "import collections\nc = collections.ChainMap(" + ThousandKeys + ")\nits = [iter(c) for _ in range(300)]\nreturn len(its)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void RetainedViewIteratorsDeny()
    {
        var code = "import collections\nc = collections.ChainMap(" + ThousandKeys + ")\nits = [iter(c.keys()) for _ in range(300)]\nreturn len(its)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = new LythonEngine().Run(code, new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void SimultaneousPartialsStayIndependent()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"b": 2}, {"a": 1})
            it1 = iter(c)
            it2 = iter(c)
            first = next(it1)
            return [first, next(it2), list(it1), list(it2)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "a", "a", new List<object?> { "b" }, new List<object?> { "b" } },
            result.ReturnValue);
    }

    [Fact]
    public void AbandonedPartialThenReuseSucceeds()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"b": 2}, {"a": 1})
            it = iter(c)
            first = next(it)
            dropped = [iter(c) for _ in range(50)]
            return [first, len(list(iter(c)))]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "a", new BigInteger(2) }, result.ReturnValue);
    }

    [Fact]
    public void ExhaustedIteratorRaisesStopIteration()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1})
            it = iter(c)
            first = next(it)
            try:
                next(it)
                outcome = "no-error"
            except StopIteration:
                outcome = "STOP"
            return [first, outcome]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "a", "STOP" }, result.ReturnValue);
    }

    [Fact]
    public void IterationDeniesUnderTinyCollectionLimit()
    {
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nreturn len(list(c))\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxCollectionSize = 10 });
        Assert.False(result.Success);
        Assert.Contains("maximum collection size exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IterationDeniesUnderTinyStepBudget()
    {
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nreturn len(list(c))\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IterationRespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nreturn len(list(c))\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { CancellationToken = cts.Token });
        Assert.False(result.Success);
        Assert.Contains("execution canceled", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LenDeniesUnderTinyBudget()
    {
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nreturn [len(c), len(c.keys()), len(c.values())]\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 10000 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void ScalarMembershipNeedsNoSnapshot()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"b": 2}, {"a": 1})
            return ["b" in c.keys(), "zz" in c.keys(), ("a", 1) in c.items(), ("a", 9) in c.items(),
                2 in c.values(), 99 in c.values(), "b" in c, "zz" in c]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, false, true, false, true, false, true, false }, result.ReturnValue);
    }

    [Fact]
    public void EnumerateAndZipRetainOwnedSnapshots()
    {
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nits = [enumerate(c) for _ in range(100)]\nreturn len(its)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = new LythonEngine().Run(code, new MockLythonHost(), options);
        AssertError(sync, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public async Task EnumerateAndZipRetainOwnedSnapshotsAsync()
    {
        var code = "import collections\nd = " + ThousandKeys + "\nc = collections.ChainMap(d)\nits = [enumerate(c) for _ in range(100)]\nreturn len(its)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost(), options);
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }
}
