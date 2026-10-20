using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// dict.update drains unknown-length pairs without materializing them, retaining
/// exact errors and iterator effects. Pairs arrive through a
/// parameter (list literals compile and validate their elements at runtime) and values read
/// back through get (literal keys are statically tracked by LA3157).
/// </summary>
public sealed class DictUpdatePairsAccountingScenarioTests
{
    private const string UpdateThroughParam =
        """
        def upd(pairs):
            d = {}
            d.update(pairs)
            return [d.get("a"), d.get("b"), len(d)]
        """;

    [Fact]
    public async Task UpdatePairsBehaveLikeBefore()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(UpdateThroughParam + "\nreturn upd([(\"a\", 1), (\"b\", 2)])\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BadPairsKeepTheirErrors()
    {
        var cases = new List<(string Source, string Type, string? Message)>
        {
            ("[(97,)]", "ValueError", "dictionary update sequence element #0 has length 1; 2 is required"),
            ("[()]", "ValueError", "dictionary update sequence element #0 has length 0; 2 is required"),
            ("[(x for x in range(10))]", "ValueError", "dictionary update sequence element #0 has length 10; 2 is required"),
            ("[(x for x in range(257))]", "ValueError", "dictionary update sequence element #0 has length 257; 2 is required"),
            ("[42]", "TypeError", "cannot convert dictionary update sequence element #0 to a sequence"),
        };
        foreach (var (source, expectedType, expectedMessage) in cases)
        {
            var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(UpdateThroughParam + "\nupd(" + source + ")\nreturn 0\n");
            Assert.True(script.IsValid);
            var sync = script.Run(new MockLythonHost());
            Assert.False(sync.Success);
            Assert.Equal(expectedType, sync.Failure?.ExceptionType);
            if (expectedMessage is not null)
            {
                Assert.Equal(expectedMessage, sync.Failure?.Message);
            }

            var asyncResult = await script.RunAsync(new MockLythonHost());
            Assert.False(asyncResult.Success);
            Assert.Equal(expectedType, asyncResult.Failure?.ExceptionType);
            if (expectedMessage is not null)
            {
                Assert.Equal(expectedMessage, asyncResult.Failure?.Message);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidPairPreservesTailEffectsFailureAndPriorEntries(bool asynchronous)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            def pair():
                try:
                    for i in range(257):
                        yield i
                    print("tail")
                    raise RuntimeError("pair failure")
                finally:
                    print("closed")
            def update(pairs):
                d = {}
                try:
                    d.update(pairs)
                except RuntimeError as error:
                    print(str(error))
                return str(list(d.items()))
            return update([("prefix", 1), pair(), ("suffix", 2)])
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost())
            : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("tail\nclosed\npair failure\n", result.StandardOutput);
        Assert.Equal("[('prefix', 1)]", result.ReturnValue);
    }

    [Fact]
    public async Task UpdateAcceptsCounterAndChainMapMappings()
    {
        // dict() and dict.update accept Counter and ChainMap mappings
        // like CPython (multi-map key order follows the merge order).
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            from collections import ChainMap, Counter, defaultdict
            results = []
            d = {}
            d.update(ChainMap({"a": 1}))
            results.append(str(d))
            d.update(Counter("aab"))
            results.append(str(sorted(d.items())))
            results.append(str(dict(Counter("aab"))))
            results.append(str(dict(defaultdict(list, {"x": [1]}))))
            results.append(str({**ChainMap({"a": 1})}))
            cm = ChainMap({"a": 1}, {"a": 2})
            results.append(str(dict(cm)["a"]))
            results.append(str(sorted(dict(cm).items())))
            scm = ChainMap({"a": 1}, {"b": 2})
            results.append(str(sorted(dict(scm).items())))
            results.append(str(sorted({**scm}.items())))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "{'a': 1}",
            "[('a', 2), ('b', 1)]",
            "{'a': 2, 'b': 1}",
            "{'x': [1]}",
            "{'a': 1}",
            "1",
            "[('a', 1)]",
            "[('a', 1), ('b', 2)]",
            "[('a', 1), ('b', 2)]",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task UpdateAcceptsDequePairSequences()
    {
        // A deque of pairs is a valid update source like CPython; the
        // static gate used to reject the deque kind although the runtime
        // validates pair elements the same way as for lists.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            from collections import deque
            d = {}
            d.update(deque([("a", 1), ("b", 2)]))
            results = []
            results.append(str(d))
            results.append(str(dict(deque([("x", 9)]))))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "{'a': 1, 'b': 2}",
            "{'x': 9}",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
