using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R39: the outermost generator iterable is evaluated and acquired when the
/// generator is created; element expressions, filters, and later clauses stay
/// deferred. Host-requirement analysis follows the same boundary.
/// </summary>
public sealed class GeneratorConstructionScenarioTests
{
    [Fact]
    public async Task OuterIterableRebindingUsesCreationValue()
    {
        var script = new LythonEngine().Compile(
            """
            data = [1, 2]
            g = (x for x in data)
            data = [3, 4]
            return list(g)
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task OuterIterableEffectsRunAtConstruction()
    {
        const string source = "g = (x for x in print(\"outer\") or [1, 2])\nprint(\"after\")\nprint(list(g))\n";
        var syncHost = new MockLythonHost();
        var sync = new LythonEngine().Run(source, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("outer\nafter\n[1, 2]\n", syncHost.CapturedStandardOutput());

        var asyncHost = new MockLythonHost();
        var asyncResult = await new LythonEngine().RunAsync(source, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("outer\nafter\n[1, 2]\n", asyncHost.CapturedStandardOutput());
    }

    [Fact]
    public async Task OuterIterableExceptionsRaiseAtConstruction()
    {
        const string zeroDivision = "def probe():\n    try:\n        g = (x for x in 1 // len([]))\n        return \"deferred\"\n    except ZeroDivisionError:\n        return \"eager\"\nreturn probe()\n";
        const string nonIterable = "def probe():\n    try:\n        g = (x for x in 1)\n        return \"deferred\"\n    except TypeError:\n        return \"eager\"\nreturn probe()\n";
        foreach (var source in new[] { zeroDivision, nonIterable })
        {
            var sync = new LythonEngine().Run(source, new MockLythonHost());
            Assert.True(sync.Success, sync.Failure?.Message);
            Assert.Equal("eager", sync.ReturnValue);

            var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
            Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
            Assert.Equal("eager", asyncResult.ReturnValue);
        }
    }

    [Fact]
    public async Task LaterClausesStayDeferred()
    {
        var script = new LythonEngine().Compile(
            """
            log = []
            def inner():
                log.append("inner")
                return [10]
            g = (y for x in [1, 2] for y in inner())
            at_creation = list(log)
            result = list(g)
            return [at_creation, result, len(log)]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Empty(Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new List<object?> { new BigInteger(10), new BigInteger(10) }, Assert.IsType<List<object?>>(values[1]));
        Assert.Equal(new BigInteger(2), values[2]);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Empty(Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Equal(new BigInteger(2), asyncValues[2]);
    }

    [Fact]
    public async Task UserIterRunsAtConstruction()
    {
        var script = new LythonEngine().Compile(
            """
            log = []
            class It:
                def __iter__(self):
                    log.append("iter")
                    return self
                def __next__(self):
                    log.append("next")
                    raise StopIteration()
            g = (x for x in It())
            at_creation = list(log)
            result = list(g)
            return [at_creation, result]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new List<object?> { "iter" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Empty(Assert.IsType<List<object?>>(values[1]));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(new List<object?> { "iter" }, Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Empty(Assert.IsType<List<object?>>(asyncValues[1]));
    }

    [Fact]
    public async Task PureOuterNeedsNoSuspension()
    {
        var script = new LythonEngine().Compile(
            """
            data = [1, 2]
            g = (x for x in data)
            data = [3, 4]
            return list(g)
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2) };
        var asyncResult = await script.RunAsync(new DelayedLythonHost("/"));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public void LocalRebindingStillReportsReadBeforeAssigned()
    {
        var compiled = new LythonEngine().Compile(
            """
            def f():
                g = (x for x in data)
                data = [1]
                return list(g)
            """);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3146");
    }
}
