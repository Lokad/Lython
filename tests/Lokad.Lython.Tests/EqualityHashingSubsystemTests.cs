using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class EqualityHashingSubsystemTests
{
    [Fact]
    public void NonReflexiveKeyIdentityDoesNotMakeValueEqualityReflexive()
    {
        object key = double.NaN;
        object other = double.NaN;
        Assert.False(PyEquality.AreEqual(key, key));
        Assert.True(PyValueComparer.Instance.Equals(key, key));
        Assert.False(PyValueComparer.Instance.Equals(key, other));
        var table = new PyDict();
        table.SetItem(key, new BigInteger(7));
        Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(table.GetItem(key)));
        Assert.False(table.ContainsKey(other));
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("amount", "amount", true)]
    [InlineData("café😀\0", "café😀\0", true)]
    [InlineData("café😀", "cafe😀", false)]
    [InlineData("é", "e\u0301", false)]
    [InlineData("amount", "amount\0", false)]
    [InlineData("", "\0", false)]
    public void OwnedStringKeysPreserveEqualityHashingAndFunding(string left, string right, bool expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(null, owner);
            var a = PyString.FromString(left, governor);
            var b = PyString.FromString(right, governor);
            var table = new PyDict(governor);
            table.SetItem(a, new BigInteger(7));
            var baseline = budget.ReservedBytes;
            Assert.Equal(expected, PyValueComparer.Instance.Equals(a, b));
            if (expected) Assert.Equal(PyValueComparer.Instance.GetHashCode(a), PyValueComparer.Instance.GetHashCode(b));
            Assert.Equal(expected, table.TryGetValue(b, out var value));
            if (expected) Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(value));
            Assert.True(baseline > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void PyValueComparer_UsesRuntimeHashingProtocols()
    {
        var comparer = PyValueComparer.Instance;
        var tupleA = new PyTuple([PyString.FromString("a"), new BigInteger(1)]);
        var tupleB = new PyTuple([PyString.FromString("a"), 1.0]);

        Assert.True(comparer.Equals(tupleA, tupleB));
        Assert.Equal(comparer.GetHashCode(tupleA), comparer.GetHashCode(tupleB));
    }

    [Fact]
    public void PyValueComparer_RejectsUnhashableValues()
    {
        var comparer = PyValueComparer.Instance;

        var ex = Assert.Throws<PyUnhashableException>(() => comparer.GetHashCode(new PyList([new BigInteger(1)])));

        Assert.Contains("unhashable value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HashingOwnedTextAndBytesPreservesAllowance()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new Harness.MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var text = PyString.FromString("é😀\0ab", context.MemoryGovernor, null);
        var bytes = new PyBytes([0, 1, 127, 128, 255], context.MemoryGovernor);
        var baseline = budget.ReservedBytes;
        var textHash = text.GetHashCode();
        var bytesHash = bytes.GetHashCode();
        Assert.True(baseline > 0);
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(textHash, text.GetPyHashCode());
        Assert.Equal(bytesHash, bytes.GetPyHashCode());
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
