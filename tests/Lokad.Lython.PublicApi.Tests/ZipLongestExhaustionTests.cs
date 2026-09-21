using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R15: zip_longest latches per-input exhaustion instead of retrying spent
// inputs every row. An input that raises StopIteration once stays exhausted
// (fill value) even if later pulls would yield, matching CPython row counts
// and side-effect counts in both execution paths.
public sealed class ZipLongestExhaustionTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LatchedExhaustionStopsBesideFiniteInput()
    {
        var result = new LythonEngine().Run(
            """
            import itertools
            calls = []
            class Flaky:
                def __iter__(self):
                    return self
                def __next__(self):
                    calls.append(1)
                    if len(calls) == 1:
                        raise StopIteration
                    return 99
            return [list(itertools.zip_longest(Flaky(), [1, 2, 3], fillvalue=0)), len(calls)]
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new List<object?> { new BigInteger(0), new BigInteger(1) }, new List<object?> { new BigInteger(0), new BigInteger(2) }, new List<object?> { new BigInteger(0), new BigInteger(3) } },
                new BigInteger(1),
            },
            result.ReturnValue);
    }

    [Fact]
    public async Task LatchedExhaustionStopsBesideFiniteInputAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
            import itertools
            calls = []
            class Flaky:
                def __iter__(self):
                    return self
                def __next__(self):
                    calls.append(1)
                    if len(calls) == 1:
                        raise StopIteration
                    return 99
            return [list(itertools.zip_longest(Flaky(), [1, 2, 3], fillvalue=0)), len(calls)]
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new List<object?> { new BigInteger(0), new BigInteger(1) }, new List<object?> { new BigInteger(0), new BigInteger(2) }, new List<object?> { new BigInteger(0), new BigInteger(3) } },
                new BigInteger(1),
            },
            result.ReturnValue);
    }

    [Fact]
    public void RepeatedlyRaisingInputPulledOnce()
    {
        var result = new LythonEngine().Run(
            """
            import itertools
            calls = []
            class AlwaysSpent:
                def __iter__(self):
                    return self
                def __next__(self):
                    calls.append(1)
                    raise StopIteration
            return [list(itertools.zip_longest(AlwaysSpent(), [1, 2])), len(calls)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new List<object?> { null, new BigInteger(1) }, new List<object?> { null, new BigInteger(2) } },
                new BigInteger(1),
            },
            result.ReturnValue);
    }

    [Fact]
    public void EmptyAndUnevenShapes()
    {
        var result = new LythonEngine().Run(
            """
            import itertools
            return [list(itertools.zip_longest()), list(itertools.zip_longest([], [])),
                list(itertools.zip_longest([1], [1, 2])), list(itertools.zip_longest([1, 2], [1]))]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?>(),
                new List<object?>(),
                new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(1) }, new List<object?> { null, new BigInteger(2) } },
                new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(1) }, new List<object?> { new BigInteger(2), null } },
            },
            result.ReturnValue);
    }

    [Fact]
    public void PartialConsumptionThenRest()
    {
        var result = new LythonEngine().Run(
            """
            import itertools
            it = itertools.zip_longest([1, 2, 3], [1])
            first = next(it)
            return [first, list(it)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new BigInteger(1), new BigInteger(1) },
                new List<object?> { new List<object?> { new BigInteger(2), null }, new List<object?> { new BigInteger(3), null } },
            },
            result.ReturnValue);
    }

    [Fact]
    public void RepeatedNextAfterExhaustion()
    {
        var result = new LythonEngine().Run(
            """
            import itertools
            it = iter(itertools.zip_longest([1], [1, 2]))
            vals = [next(it), next(it)]
            try:
                next(it)
                outcome = "no-error"
            except StopIteration:
                outcome = "STOP"
            try:
                next(it)
                outcome2 = "no-error"
            except StopIteration:
                outcome2 = "STOP"
            return [vals, outcome, outcome2]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(1) }, new List<object?> { null, new BigInteger(2) } },
                "STOP",
                "STOP",
            },
            result.ReturnValue);
    }

    [Fact]
    public void NonStopExceptionPropagates()
    {
        var sync = new LythonEngine().Run(
            """
            import itertools
            class Boom:
                def __iter__(self):
                    return self
                def __next__(self):
                    raise ValueError("boom")
            try:
                list(itertools.zip_longest(Boom(), [1]))
                outcome = "no-error"
            except ValueError:
                outcome = "VALUE"
            return outcome
            """,
            new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("VALUE", sync.ReturnValue);
    }

    [Fact]
    public async Task NonStopExceptionPropagatesAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
            import itertools
            class Boom:
                def __iter__(self):
                    return self
                def __next__(self):
                    raise ValueError("boom")
            try:
                list(itertools.zip_longest(Boom(), [1]))
                outcome = "no-error"
            except ValueError:
                outcome = "VALUE"
            return outcome
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("VALUE", result.ReturnValue);
    }

    [Fact]
    public async Task DelayedAsyncInputsSuspendThroughHost()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            with open("/v.txt") as f:
                rows = list(itertools.zip_longest(map(float, f), [1.0, 2.0, 3.0], fillvalue=0.0))
            return rows
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.txt", "10.0\n20.0\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { 10.0, 1.0 },
                new List<object?> { 20.0, 2.0 },
                new List<object?> { 0.0, 3.0 },
            },
            result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
