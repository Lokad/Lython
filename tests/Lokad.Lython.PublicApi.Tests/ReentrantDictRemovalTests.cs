using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N30: reentrant dictionary removal resolves the stored entry once and deletes
// it without re-dispatching guest code. Each scenario pins callback order,
// mutation, returned value and final dictionary against CPython 3.13.2 in both
// execution modes (a lookup followed by a separate Remove ran __eq__ twice,
// so a deletion inside the first dispatch failed spuriously on pop).
public sealed class ReentrantDictRemovalTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static async Task AssertBothModes(string source, object? expected)
    {
        var script = Compile(source);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }


    [Fact]
    public async Task PopDeletingMatchedEntryReportsKeyError()
    {
        // S1: __eq__ deletes the matched entry, then reports a match.
        // CPython raises KeyError with the entry already gone.
        await AssertBothModes(
            """
                calls = []
                class K:
                    def __init__(self, name):
                        self.name = name
                    def __hash__(self):
                        calls.append("hash:" + self.name)
                        return 0
                    def __eq__(self, other):
                        calls.append("eq:" + self.name)
                        return self.name == getattr(other, "name", None)

                d = {}
                a = K("a")
                d[a] = 1
                class Evil(K):
                    __hash__ = K.__hash__
                    def __eq__(self, other):
                        calls.append("eq:evil")
                        del d[a]
                        return True
                calls.clear()
                try:
                    d.pop(Evil("e"))
                    return ["noreturn", sorted(k.name for k in d), calls]
                except KeyError:
                    return ["keyerror", sorted(k.name for k in d), calls]
                """,
            new List<object?> { "keyerror", new List<object?>(), new List<object?> { "hash:e", "eq:evil" } });
    }

    [Fact]
    public async Task PopDeletingOtherEntryCompletes()
    {
        // S2: __eq__ deletes a different entry, then reports a match.
        // CPython returns the matched value exactly once.
        await AssertBothModes(
            """
                calls = []
                class K:
                    def __init__(self, name):
                        self.name = name
                    def __hash__(self):
                        calls.append("hash:" + self.name)
                        return 0
                    def __eq__(self, other):
                        calls.append("eq:" + self.name)
                        return self.name == getattr(other, "name", None)

                d = {}
                a = K("a")
                b = K("b")
                d[a] = 1
                d[b] = 2
                class Evil(K):
                    __hash__ = K.__hash__
                    def __eq__(self, other):
                        calls.append("eq:evil")
                        if getattr(other, "name", None) == "a":
                            del d[b]
                        return True
                calls.clear()
                try:
                    r = d.pop(Evil("e"))
                    return ["returned", r, sorted(k.name for k in d), calls]
                except KeyError:
                    return ["keyerror", sorted(k.name for k in d), calls]
                """,
            new List<object?> { "returned", new BigInteger(1), new List<object?>(), new List<object?> { "hash:e", "eq:evil" } });
    }

    [Fact]
    public async Task PopAfterClearReportsKeyError()
    {
        // S3: __eq__ clears the dictionary, then reports a match.
        // CPython raises KeyError with nothing left.
        await AssertBothModes(
            """
                calls = []
                class K:
                    def __init__(self, name):
                        self.name = name
                    def __hash__(self):
                        calls.append("hash:" + self.name)
                        return 0
                    def __eq__(self, other):
                        calls.append("eq:" + self.name)
                        return self.name == getattr(other, "name", None)

                d = {}
                a = K("a")
                d[a] = 1
                class Evil(K):
                    __hash__ = K.__hash__
                    def __eq__(self, other):
                        calls.append("eq:evil")
                        d.clear()
                        return True
                calls.clear()
                try:
                    d.pop(Evil("e"))
                    return ["noreturn", sorted(k.name for k in d), calls]
                except KeyError:
                    return ["keyerror", sorted(k.name for k in d), calls]
                """,
            new List<object?> { "keyerror", new List<object?>(), new List<object?> { "hash:e", "eq:evil" } });
    }

    [Fact]
    public async Task PopWithDefaultAfterDeleteReturnsDefault()
    {
        // S4: __eq__ deletes the matched entry and reports no match.
        // CPython returns the default with nothing left.
        await AssertBothModes(
            """
                calls = []
                class K:
                    def __init__(self, name):
                        self.name = name
                    def __hash__(self):
                        calls.append("hash:" + self.name)
                        return 0
                    def __eq__(self, other):
                        calls.append("eq:" + self.name)
                        return self.name == getattr(other, "name", None)

                d = {}
                a = K("a")
                d[a] = 1
                class Evil(K):
                    __hash__ = K.__hash__
                    def __eq__(self, other):
                        calls.append("eq:evil")
                        del d[a]
                        return False
                calls.clear()
                try:
                    r = d.pop(Evil("e"), "dflt")
                    return ["returned", r, sorted(k.name for k in d), calls]
                except KeyError:
                    return ["keyerror", sorted(k.name for k in d), calls]
                """,
            new List<object?> { "returned", "dflt", new List<object?>(), new List<object?> { "hash:e", "eq:evil" } });
    }

    [Fact]
    public async Task DefaultDictPopDeletingOtherEntryCompletes()
    {
        // defaultdict shares the single lookup-and-remove primitive.
        await AssertBothModes(
            """
                calls = []
                class K:
                    def __init__(self, name):
                        self.name = name
                    def __hash__(self):
                        calls.append("hash:" + self.name)
                        return 0
                    def __eq__(self, other):
                        calls.append("eq:" + self.name)
                        return self.name == getattr(other, "name", None)

                import collections
                d = collections.defaultdict(int)
                a = K("a")
                b = K("b")
                d[a] = 1
                d[b] = 2
                class Evil(K):
                    __hash__ = K.__hash__
                    def __eq__(self, other):
                        calls.append("eq:evil")
                        if getattr(other, "name", None) == "a":
                            del d[b]
                        return True
                calls.clear()
                try:
                    r = d.pop(Evil("e"))
                    return ["returned", r, sorted(k.name for k in d), calls]
                except KeyError:
                    return ["keyerror", sorted(k.name for k in d), calls]
                """,
            new List<object?> { "returned", new BigInteger(1), new List<object?>(), new List<object?> { "hash:e", "eq:evil" } });
    }
}