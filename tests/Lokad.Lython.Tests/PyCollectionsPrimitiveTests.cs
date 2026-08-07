using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PyCollectionsPrimitiveTests
{
    [Fact]
    public void PyDict_UsesPythonLikeNumericKeyEquality()
    {
        var dict = new PyDict();

        dict.SetItem(new BigInteger(1), "one");
        dict.SetItem(true, "bool");

        Assert.Single(dict);
        Assert.True(dict.ContainsKey(new BigInteger(1)));
        Assert.True(dict.ContainsKey(true));
        Assert.True(dict.TryGetValue(new BigInteger(1), out var value));
        Assert.Equal("bool", value);
    }

    [Fact]
    public void PyDict_SupportsTupleAndPyStringKeys()
    {
        var dict = new PyDict();
        var tupleKey = new PyTuple([new BigInteger(2), PyString.FromString("x")]);
        var stringKey = PyString.FromString("name");

        dict.SetItem(tupleKey, "tuple");
        dict.SetItem(stringKey, 5);

        Assert.True(dict.ContainsKey(new PyTuple([new BigInteger(2), PyString.FromString("x")])));
        Assert.True(dict.TryGetValue(stringKey, out var value));
        Assert.Equal((object)5, value);
        Assert.Equal("tuple", dict.GetItem(new PyTuple([new BigInteger(2), PyString.FromString("x")])));
    }

    [Fact]
    public void PyDict_CopyRemoveClearAndNoneKey_AreIndependent()
    {
        var original = new PyDict();
        original.SetItem(PyString.FromString("a"), 1);
        original.SetItem(PyString.FromString("b"), 2);
        original.SetItem(PyNone.Instance, "none");

        var copy = new PyDict(original);
        original.Remove(PyString.FromString("a"));
        copy.SetItem(PyString.FromString("c"), 3);

        Assert.Single(original, pair => !ReferenceEquals(pair.Key, PyNone.Instance));
        Assert.Equal(4, copy.Count);
        Assert.True(copy.ContainsKey(PyNone.Instance));
        Assert.Equal("none", copy.GetItem(PyNone.Instance));

        copy.Clear();
        Assert.Empty(copy);
        Assert.Equal(2, original.Count);
    }

    [Fact]
    public void PyList_RequiresExplicitPyNoneAndCopyIsIndependent()
    {
        var original = new PyList([PyNone.Instance, new BigInteger(1), PyString.FromString("x")]);
        var copy = new PyList(original);

        original.Add(true);
        copy[1] = new BigInteger(9);
        copy.RemoveAt(2);

        Assert.Same(PyNone.Instance, original[0]);
        Assert.Equal(4, original.Count);
        Assert.Equal(new BigInteger(1), original[1]);
        Assert.Equal(2, copy.Count);
        Assert.Equal(new BigInteger(9), copy[1]);
    }

    [Fact]
    public void PyTuple_RequiresExplicitPyNone_AndSupportsPythonEqualityAndHashing()
    {
        var left = new PyTuple([PyNone.Instance, new BigInteger(1), PyString.FromString("x")]);
        var right = new PyTuple([PyNone.Instance, true, PyString.FromString("x")]);

        Assert.Same(PyNone.Instance, left[0]);
        Assert.True(LythonRuntime.AreEqual(left, right));
        Assert.Equal(PyValueComparer.Instance.GetHashCode(left), PyValueComparer.Instance.GetHashCode(right));
        Assert.Equal(3, left.Length);
    }

    [Fact]
    public void PySet_ActsAsExplicitRuntimeValue()
    {
        var left = new PySet([new BigInteger(1), PyString.FromString("x")]);
        var right = new PySet([true, PyString.FromString("y")]);

        left.UnionWith(right);

        Assert.Equal(3, left.Count);
        Assert.True(left.Contains(new BigInteger(1)));
        Assert.True(left.Contains(PyString.FromString("y")));
        Assert.True(left.IsTruthy());

        var intersection = new PySet(left);
        intersection.IntersectWith(new PySet([true, PyString.FromString("y")]));

        Assert.Equal(2, intersection.Count);
        Assert.True(intersection.SetEquals(new PySet([new BigInteger(1), PyString.FromString("y")])));
    }

    [Fact]
    public void PyDequeIndexesFromEitherEndAndSlicesInLinearTime()
    {
        var deque = new PyDeque(Enumerable.Range(0, 10_000).Select(static value => (object)new BigInteger(value)));

        Assert.Equal(new BigInteger(0), deque.GetItem(0));
        Assert.Equal(new BigInteger(9_999), deque.GetItem(9_999));

        var slice = Assert.IsType<PyDeque>(deque.GetSlice(Enumerable.Range(0, 5_000).Select(static value => value * 2)));
        Assert.Equal(5_000, slice.Count);
        Assert.Equal(new BigInteger(0), slice.GetItem(0));
        Assert.Equal(new BigInteger(9_998), slice.GetItem(4_999));
    }

    [Fact]
    public void PyDequeEqualityTraversesLinkedStorageOnce()
    {
        var values = Enumerable.Range(0, 20_000).Select(static value => (object)new BigInteger(value)).ToArray();
        var left = new PyDeque(values);
        var right = new PyDeque(values);

        Assert.True(PyEquality.AreEqual(left, right));

        right.SetItem(right.Count - 1, new BigInteger(-1));
        Assert.False(PyEquality.AreEqual(left, right));
    }
}
