using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R13b (tables): keys with custom __hash__/__eq__ probe by precomputed hash
// plus element == through a side index, so lookup/insertion/deletion match
// CPython while builtin-key paths stay structural. Set algebra follows.
public sealed class ContextualDictTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomKeyLookup()
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
            b = A(1)
            d = {}
            d[a] = 5
            return [b in d, d[b], len(d)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, new BigInteger(5), new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void MixedKeyMethods()
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

            d = {A(1): "a", A(2): "b", 3: "c"}
            first = [d[A(1)], d.get(A(2)), len(d)]
            del d[A(1)]
            d[A(4)] = "d"
            popped = d.pop(A(4))
            kept = d.setdefault(A(5), "e")
            return first + [len(d), popped, kept, d[A(5)]]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "a", "b", new BigInteger(3), new BigInteger(3), "d", "e", "e" },
            result.ReturnValue);
    }

    [Fact]
    public void CustomKeyDictEquality()
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

            return [{A(1): "a"} == {A(1): "a"}, {A(1): "a"} == {A(2): "a"},
                    {A(1): "a"} != {A(1): "b"}, {A(1): "a", 2: "b"} == {A(1): "a", 2: "b"}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, true, true },
            result.ReturnValue);
    }

    [Fact]
    public void UpdateCtorAndMerge()
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

            d = {}
            d.update([(A(1), 1), (A(2), 2)])
            e = dict([(A(3), 3)])
            m = {A(1): "a"} | {A(1): "b", A(4): "c"}
            return [len(d), d[A(1)] + d[A(2)], e[A(3)], len(m), m[A(1)]]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(3), new BigInteger(3), new BigInteger(2), "b" },
            result.ReturnValue);
    }

    [Fact]
    public void InsertionOrderPreserved()
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

            d = {}
            d[1] = "one"
            d[A(2)] = "two"
            d["s"] = "ess"
            d[A(3)] = "three"
            del d[A(2)]
            d[A(4)] = "four"
            return [[d[k] for k in d] == ["one", "ess", "three", "four"], len(d)]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, new BigInteger(4) },
            result.ReturnValue);
    }

    [Fact]
    public void MissingKeysFail()
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

            d = {A(1): "a"}
            seen = []
            seen.append(d.popitem()[1])
            seen.append(len(d))
            for op in ["get", "del", "in"]:
                try:
                    if op == "get":
                        d[A(9)]
                    elif op == "del":
                        del d[A(9)]
                    else:
                        seen.append(A(1) in d)
                except KeyError:
                    seen.append("missing")
            return seen
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "a", new BigInteger(0), "missing", "missing", false },
            result.ReturnValue);
    }

    [Fact]
    public void CallbackFailuresPropagate()
    {
        var result = new LythonEngine().Run(
            """
            class H:
                def __hash__(self):
                    raise ValueError("hboom")
                def __eq__(self, other):
                    return True
            seen = []
            try:
                {H(): 1}
            except ValueError:
                seen.append("insert")
            d = {}
            try:
                d[H()] = 1
            except ValueError:
                seen.append("setitem")
            return seen
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "insert", "setitem" }, result.ReturnValue);
    }

    [Fact]
    public async Task AsyncCustomKeyLookup()
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

            a = A(1)
            b = A(1)
            d = {}
            d[a] = 5
            return [b in d, d[b], {A(2): "x"} == {A(2): "x"}]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, new BigInteger(5), true },
            result.ReturnValue);
    }

    [Fact]
    public void DerivedForms()
    {
        var result = new LythonEngine().Run(
            """
            import collections
            class A:
                def __init__(self, v):
                    self.v = v
                def __eq__(self, other):
                    return isinstance(other, A) and self.v == other.v
                def __hash__(self):
                    return self.v
            d = dict.fromkeys([A(1), A(2)], 0)
            e = dict(d)
            e2 = d.copy()
            c = collections.Counter()
            c.update([A(1), A(1), A(2)])
            c.subtract([A(1)])
            return [len(d), d[A(1)], e[A(2)], e2[A(1)], c[A(1)] + c[A(2)]]
            """,
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(0), new BigInteger(0), new BigInteger(0), new BigInteger(2) },
            result.ReturnValue);
    }

    [Fact]
    public void RetainedProtocolTablesDeny()
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
                xs.append({A(i): i})
            return len(xs)
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DiscardedProtocolTablesSucceed()
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
                d = {A(i): i}
            """,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }
}
