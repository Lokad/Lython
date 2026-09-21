using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R14: ChainMap aliases its underlying mappings instead of copying them.
// Reads see later mutations, maps preserves identity, missing keys follow
// mapping rules (defaultdict factories store, counters zero-fill and stop),
// iteration merges last-map-first, and copies stay kind-preserving.
public sealed class ChainMapAliasingScenarioTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnderlyingMutationVisibleAndIdentityPreserved()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = collections.defaultdict(int, {"a": 1})
            c = collections.ChainMap(d)
            d["a"] = 2
            return [c["a"], c.maps[0] is d]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), true }, result.ReturnValue);
    }

    [Fact]
    public void CounterMissingYieldsZeroAndStopsChain()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            a = collections.ChainMap(collections.Counter(), {"a": 1})
            b = collections.ChainMap({"a": 1}, collections.Counter())
            c = collections.ChainMap({"a": 1}, collections.Counter(), {"missing": "late"})
            return [a["missing"], b["missing"], c["missing"]]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(0), new BigInteger(0), new BigInteger(0) }, result.ReturnValue);
    }

    [Fact]
    public void DefaultdictMissManufacturesAndStores()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = collections.defaultdict(int)
            c = collections.ChainMap(d)
            hit = c["x"]
            return [hit, dict(d) == {"x": 0}, "x" in c]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(0), true, true }, result.ReturnValue);
    }

    [Fact]
    public void ContainmentAndGetNeverManufacture()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = collections.defaultdict(int)
            c = collections.ChainMap(d)
            k = collections.Counter()
            ck = collections.ChainMap(k)
            return ["x" in c, c.get("x", "dflt"), "x" in ck, ck.get("x", "dflt"), dict(d) == {}, "y" in c.keys()]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { false, "dflt", false, "dflt", true, false },
            result.ReturnValue);
    }

    [Fact]
    public void OverlappingIterationOrderMatchesCpython()
    {
        const string code = """
            import collections
            c = collections.ChainMap({"a": 1, "b": 1}, {"b": 2, "c": 2}, {"c": 3, "d": 3})
            return [list(c), len(c), dict(c) == {"a": 1, "b": 1, "c": 2, "d": 3}, list(dict(c)), list(c.keys())]
            """;
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var expected = new List<object?>
        {
            new List<object?> { "c", "d", "b", "a" },
            new BigInteger(4),
            true,
            new List<object?> { "c", "d", "b", "a" },
            new List<object?> { "c", "d", "b", "a" },
        };
        Assert.Equal(expected, sync.ReturnValue);
    }

    [Fact]
    public async Task OverlappingIterationOrderMatchesCpythonAsync()
    {
        const string code = """
            import collections
            c = collections.ChainMap({"a": 1, "b": 1}, {"b": 2, "c": 2}, {"c": 3, "d": 3})
            return list(c)
            """;
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "c", "d", "b", "a" }, result.ReturnValue);
    }

    [Fact]
    public void IterationSnapshotsAgainstLaterMutation()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1}, {"b": 2})
            it = iter(c)
            c["z"] = 26
            return list(it)
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "b", "a" }, result.ReturnValue);
    }

    [Fact]
    public void ValuesViewSeesValueMutationsButRaisesOnDeletion()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1, "b": 2})
            it = iter(c.values())
            first = next(it)
            c["b"] = 99
            rest = list(it)
            c2 = collections.ChainMap({"a": 1, "b": 2})
            it2 = iter(c2.values())
            n2 = next(it2)
            del c2["b"]
            try:
                tail = list(it2)
                outcome = tail
            except KeyError:
                outcome = "KEYERROR"
            return [first, rest, n2, outcome]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new List<object?> { new BigInteger(99) }, new BigInteger(1), "KEYERROR" },
            result.ReturnValue);
    }

    [Fact]
    public void CopyPreservesKindAndAliasesRest()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = collections.defaultdict(int, {"a": 1})
            c = collections.ChainMap(d, {"b": 2})
            cc = c.copy()
            shape = [type(cc.maps[0]).__name__, cc.maps[0] is d, cc.maps[1] is c.maps[1]]
            c["a"] = 99
            return shape + [cc["a"]]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "defaultdict", false, true, new BigInteger(1) }, result.ReturnValue);
    }

    [Fact]
    public void NewChildParentsAndWrites()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1})
            n = c.new_child()
            n["x"] = 1
            writes = [dict(n) == {"a": 1, "x": 1}, dict(c) == {"a": 1}, n.maps[1] is c.maps[0], [len(m) for m in c.parents.maps] == [0]]
            c["w"] = 9
            d = {"o": 0}
            c2 = collections.ChainMap(d)
            del c2["o"]
            c2["q"] = 7
            return writes + [dict(d) == {"q": 7}, dict(c) == {"a": 1, "w": 9}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, true, true, true, true, true }, result.ReturnValue);
    }

    [Fact]
    public void UpdateWritesThroughFirstMapKind()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = collections.defaultdict(int, {"a": 1})
            c = collections.ChainMap(d)
            c.update({"b": 2})
            c.update(e=5)
            return [dict(d) == {"a": 1, "b": 2, "e": 5}, d["b"], type(c.maps[0]).__name__]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(2), "defaultdict" }, result.ReturnValue);
    }

    [Fact]
    public void ContentEqualityAcrossMappingKinds()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            a = collections.ChainMap({"x": 1})
            return [a == collections.ChainMap({"x": 1}), a == {"x": 1},
                a == collections.defaultdict(int, {"x": 1}), a == collections.Counter({"x": 1}),
                a == collections.ChainMap({"x": 2}), a != collections.ChainMap({"x": 2}),
                a == {"x": 1, "y": 2}, {"x": 1} == a]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, true, true, true, false, true, false, true }, result.ReturnValue);
    }

    [Fact]
    public void UnionStructureMatchesCpython()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1}, {"b": 2})
            u = c | {"a": 9, "z": 9}
            v = {"a": 9, "z": 9} | c
            return [len(u.maps), u.maps[1] is c.maps[1], len(v.maps), dict(u) == {"a": 9, "z": 9, "b": 2}, dict(v) == {"a": 1, "z": 9, "b": 2}, list(u.maps[0]), list(v.maps[0])]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new BigInteger(2),
                true,
                new BigInteger(1),
                true,
                true,
                new List<object?> { "a", "z" },
                new List<object?> { "a", "z", "b" },
            },
            result.ReturnValue);
    }

    [Fact]
    public void NonMappingRejected()
    {
        var result = new LythonEngine().Run(
            "import collections\nreturn collections.ChainMap([1, 2])\n",
            new MockLythonHost());
        AssertError(result, "TypeError", "must be dictionaries");
    }

    [Fact]
    public void PopGetDeleteBehaviors()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            c = collections.ChainMap({"a": 1, "w": 0}, {"b": 2})
            popped = c.pop("w")
            same = c.pop("w", "dflt")
            try:
                c.pop("w")
                missing = "no-error"
            except KeyError:
                missing = "KEYERROR"
            return [popped, same, missing, c.get("a"), c.get("zz", "dflt"), dict(c) == {"a": 1, "b": 2}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(0), "dflt", "KEYERROR", new BigInteger(1), "dflt", true }, result.ReturnValue);
    }

    [Fact]
    public void UpdateFromChainMapKeepsMergeOrder()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            d = {}
            d.update(collections.ChainMap({"b": 2}, {"a": 1}))
            return [d == {"a": 1, "b": 2}, list(d)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, new List<object?> { "a", "b" } }, result.ReturnValue);
    }
}
