using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R13b (tables, sets): items with custom __hash__/__eq__ probe by precomputed
// hash plus element == through a side index; algebra, views, and comparisons
// share the semantics while builtin-only sets keep the fast path.
public sealed class ContextualSetTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomItemBasics()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            s = {A(1), A(2)}
            seen = [A(1) in s, A(9) in s, len(s)]
            s.add(A(3))
            s.add(A(1))
            seen.append(len(s))
            s.discard(A(2))
            seen.append(len(s))
            s.remove(A(3))
            return seen + [len(s)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, new BigInteger(2), new BigInteger(3), new BigInteger(2), new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void CustomAlgebra()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            s1 = {A(1), A(2)}
            s2 = {A(2), A(3)}
            return [s1 | s2 == {A(1), A(2), A(3)}, s1 & s2 == {A(2)},
                    s1 - s2 == {A(1)}, s1 ^ s2 == {A(1), A(3)}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, true, true },
            result.ReturnValue);
    }

    [Fact]
    public void CustomInplaceAndRelations()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            s1 = {A(1), A(2)}
            s1 |= {A(3)}
            first = [len(s1), {A(1)}.issubset(s1), s1.issuperset({A(2)})]
            s1 &= {A(2), A(3)}
            second = [len(s1), s1 <= {A(2), A(3)}, s1 < {A(2), A(3), A(4)}]
            return [first, second, s1.isdisjoint({A(9)}), s1 == {A(2), A(3)}, s1 != {A(9)}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new BigInteger(3), true, true },
                new List<object?> { new BigInteger(2), true, true },
                true, true, true,
            },
            result.ReturnValue);
    }

    [Fact]
    public void CustomViews()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            d = {A(1): "a", A(2): "b"}
            return [A(1) in d.keys(), A(9) in d.keys(),
                    (A(1), "a") in d.items(), (A(9), "a") in d.items()]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, true, false },
            result.ReturnValue);
    }

    [Fact]
    public void DefaultDictCounterChainMap()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            import collections
            d = collections.defaultdict(int)
            d[A(1)] = 5
            c = collections.Counter()
            c[A(1)] = 3
            c[A(1)] = c[A(1)] + 1
            m = collections.ChainMap({A(1): "a"}, {A(2): "b"})
            return [d[A(1)], A(1) in d, d.get(A(1)), c[A(1)], A(2) in c,
                    m[A(1)], m[A(2)], A(1) in m]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(5), true, new BigInteger(5), new BigInteger(4), false, "a", "b", true },
            result.ReturnValue);
    }

    [Fact]
    public void ConstructionForms()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            a = A(1)
            s1 = {a, A(2)}
            s2 = set([a, A(2), A(2)])
            s3 = {x for x in [a, A(3)]}
            d = collections.Counter([a, a, A(4)]) if False else {}
            return [len(s1), len(s2), len(s3), A(2) in s2, A(3) in s3]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(2), new BigInteger(2), true, true },
            result.ReturnValue);
    }

    [Fact]
    public async Task AsyncCustomSetOps()
    {
        var result = await new LythonEngine().RunAsync(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            s = {A(1), A(2)}
            return [A(1) in s, s == {A(1), A(2)}, len(s | {A(3)})]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, new BigInteger(3) },
            result.ReturnValue);
    }

    [Fact]
    public void RetainedProtocolSetsDeny()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            xs = []
            for i in range(2000):
                xs.append({A(i)})
            return len(xs)
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DiscardedProtocolSetsSucceed()
    {
        var result = new LythonEngine().Run(
            """
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v

            for i in range(20000):
                s = {A(i)}
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }
}
