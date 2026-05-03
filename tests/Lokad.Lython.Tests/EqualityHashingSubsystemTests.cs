using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class EqualityHashingSubsystemTests
{
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

        var ex = Assert.Throws<InvalidOperationException>(() => comparer.GetHashCode(new PyList([new BigInteger(1)])));

        Assert.Contains("unhashable value", ex.Message, StringComparison.Ordinal);
    }
}
