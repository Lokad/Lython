using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R13b (foundation): contextual key hashing. Custom __hash__ slots dispatch
// (inherited included) with integer validation; __eq__ without __hash__ (or
// __hash__ = None) is unhashable like CPython; everything else keeps the
// structural funnel bit-for-bit. Table probing for protocol keys follows.
public sealed class ContextualHashTests
{
    [Fact]
    public void EqWithoutHashIsUnhashable()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
            seen = []
            try:
                hash(A())
            except TypeError:
                seen.append("hash")
            try:
                {A(): 1}
            except TypeError:
                seen.append("dict")
            try:
                {A()}
            except TypeError:
                seen.append("set")
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "hash", "dict", "set" },
            result.ReturnValue);
    }

    [Fact]
    public void CustomHashDispatched()
    {
        const string code = """
            class B:
                def __hash__(self):
                    return 42
            class C(B):
                pass
            return [hash(B()), hash(C())]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(42), new BigInteger(42) },
            result.ReturnValue);
    }

    [Fact]
    public void BadHashResultsRaise()
    {
        const string code = """
            class D:
                def __hash__(self):
                    return "x"
            class E:
                __hash__ = None
            class F:
                __hash__ = 5
            seen = []
            for make in [D, E, F]:
                try:
                    hash(make())
                except TypeError:
                    seen.append(1)
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(1), new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void TupleHashConsistent()
    {
        const string code = """
            class B:
                def __hash__(self):
                    return 7
            a = B()
            b = B()
            return [hash((1, 2)) == hash((1, 2)), hash((a, 1)) == hash((b, 1))]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, true }, result.ReturnValue);
    }

    [Fact]
    public void TupleWithUnhashableElementRaises()
    {
        const string code = """
            class A:
                def __eq__(self, other):
                    return True
            try:
                {(A(), 1): 2}
                return "no-error"
            except TypeError:
                return "unhashable"
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("unhashable", result.ReturnValue);
    }

    [Fact]
    public void PlainInstancesStayIdentityKeyed()
    {
        const string code = """
            class P:
                pass
            a = P()
            b = P()
            d = {}
            d[a] = 3
            return [d[a], b in d, hash(a) == hash(a)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(3), false, true },
            result.ReturnValue);
    }

    [Fact]
    public void DataclassHashModesPreserved()
    {
        const string code = """
            from dataclasses import dataclass
            @dataclass(frozen=True)
            class P:
                x: int
            @dataclass
            class Q:
                x: int
            d = {}
            d[P(2)] = 9
            seen = [hash(P(1)) == hash(P(1)), d[P(2)]]
            try:
                hash(Q(1))
            except TypeError:
                seen.append("unhashable")
            return seen
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, new BigInteger(9), "unhashable" },
            result.ReturnValue);
    }

    [Fact]
    public void UnashableKeyMessages()
    {
        const string code = """
            try:
                {[1]: 2}
            except TypeError as e:
                return str(e)
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("unhashable type: 'list'", result.ReturnValue);
    }
}
