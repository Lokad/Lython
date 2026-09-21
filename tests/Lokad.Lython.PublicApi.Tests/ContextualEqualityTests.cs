using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R13a: nested == positions share the top-level protocol (identity shortcut,
// reflected __eq__, NotImplemented, truthiness) instead of context-free
// comparison. Dict/set key hashing stays structural until R13b; set equality
// with default hashes already agrees with CPython.
public sealed class ContextualEqualityTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomEqNestedSequences()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
            a = A()
            b = A()
            return [a == b, [a] == [b], [a] != [b], [a].count(b), [a].index(b),
                    (a,) == (b,), (a,).count(b), (a,).index(b)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, false, new BigInteger(1), new BigInteger(0), true, new BigInteger(1), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public void CustomEqDequeAndRemove()
    {
        const string code = """
            import collections
            class A:
                def __eq__(self, other):
                    return True
            a = A()
            b = A()
            same = collections.deque([a]) == collections.deque([b])
            counted = collections.deque([a]).count(b)
            found = collections.deque([a]).index(b)
            x = [a]
            x.remove(b)
            return [same, counted, found, len(x)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, new BigInteger(1), new BigInteger(0), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public void SameNanIdentityInContainers()
    {
        const string code = """
            import math
            n = math.inf - math.inf
            return [n == n, [n] == [n], (n,) == (n,), [n].count(n), [n].index(n)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { false, true, true, new BigInteger(1), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public void DictValuesCustom()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
            a = A()
            b = A()
            return [{"k": a} == {"k": b}, {"k": b} == {"k": a}, {"k": a} != {"k": b}]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, true, false }, result.ReturnValue);
    }

    [Fact]
    public void DataclassNestedFields()
    {
        const string code = """
            from dataclasses import dataclass
            @dataclass
            class P:
                x: object
            class Q:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return self.v == other.v
            return [P(1) == P(1), P(Q(1)) == P(Q(2)), P(Q(1)) == P(Q(1))]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, false, true }, result.ReturnValue);
    }

    [Fact]
    public void ReflectedNotImplemented()
    {
        const string code = """
            class L:
                def __eq__(self, other):
                    return NotImplemented
            class R:
                def __eq__(self, other):
                    return True
            return [[L()] == [R()], [R()] == [L()], [L()] == [L()], [L()] != [L()]]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, false, true },
            result.ReturnValue);
    }

    [Fact]
    public void CustomTruthNested()
    {
        const string code = """
            class T:
                def __eq__(self, other):
                    return "yes"
            a = T()
            b = T()
            return [[a] == [b], [a] != [b], [a].count(b)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void EqExceptionsPropagate()
    {
        const string code = """
            class B:
                def __eq__(self, other):
                    raise ValueError("boom")
            try:
                [B()] == [B()]
                return "no-error"
            except ValueError:
                return "raised"
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("raised", result.ReturnValue);
    }

    [Fact]
    public void TupleKeyIdentityPreserved()
    {
        const string code = """
            d = {}
            k1 = (1, 2)
            k2 = (1, 2)
            d[k1] = 7
            return [d[k2], list(d.keys())[0] is k1]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(7), true }, result.ReturnValue);
    }

    [Fact]
    public void CyclicListsStayRecursionBounded()
    {
        const string code = """
            l1 = []
            l2 = []
            l1.append(l1)
            l2.append(l2)
            try:
                l1 == l2
                return "no-error"
            except RecursionError:
                return "recursion"
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("recursion", result.ReturnValue);
    }

    [Fact]
    public void RecursiveEqCycleStayBounded()
    {
        const string code = """
            class H:
                def __init__(self, tag):
                    self.tag = tag
                def __eq__(self, other):
                    return self.tag == other.tag
            x = [H(1)]
            x.append(x)
            y = [H(1)]
            y.append(y)
            try:
                x == y
                return "no-error"
            except RecursionError:
                return "recursion"
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("recursion", result.ReturnValue);
    }

    [Fact]
    public async Task AsyncCustomEqNested()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
            a = A()
            b = A()
            return [[a] == [b], (a,) == (b,), {"k": a} == {"k": b}, [a] != [b]]
            """;
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, true, false },
            result.ReturnValue);
    }

    [Fact]
    public async Task AsyncHostEqSuspendsThroughNesting()
    {
        const string code = """
            class H:
                def __init__(self, tag):
                    self.tag = tag
                def __eq__(self, other):
                    with open("/a.txt") as f:
                        pass
                    return self.tag == other.tag
            return [[H(1)] == [H(1)], [H(1)] == [H(2)]]
            """;
        var host = new DelayedLythonHost();
        host.SeedFile("/a.txt", "x");
        var result = await new LythonEngine().RunAsync(code, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, false }, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task AsyncSearchMembers()
    {
        const string code = """
            import collections
            class A:
                def __eq__(self, other):
                    return True
            a = A()
            b = A()
            x = [a]
            x.remove(b)
            return [[a].count(b), [a].index(b), (a,).count(b), (a,).index(b),
                    collections.deque([a]).count(b), collections.deque([a]).index(b), len(x)]
            """;
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(0), new BigInteger(1), new BigInteger(0), new BigInteger(1), new BigInteger(0), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public async Task AsyncHostCountSuspends()
    {
        const string code = """
            class H:
                def __init__(self, tag):
                    self.tag = tag
                def __eq__(self, other):
                    with open("/a.txt") as f:
                        pass
                    return self.tag == other.tag
            return [[H(1)].count(H(1)), [H(1)].count(H(2))]
            """;
        var host = new DelayedLythonHost();
        host.SeedFile("/a.txt", "x");
        var result = await new LythonEngine().RunAsync(code, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(0) }, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void SetDefaultHashStillStructural()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
                def __hash__(self):
                    return id(self) % 1000003
            a = A()
            b = A()
            return [a == b, {a} == {b}, a in {b}]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, false, false }, result.ReturnValue);
    }
}
