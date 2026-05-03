using System.Numerics;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Tests;

public sealed class NumericArchitectureSubsystemTests
{
    [Fact]
    public void PyNumberOps_KeepsIntegerAndFloatPathsDistinct()
    {
        Assert.True(PyNumberOps.TryAsNumber(new BigInteger(3), out var integer));
        Assert.False(integer.IsFloat);
        Assert.Equal(new BigInteger(3), integer.Integer);

        Assert.True(PyNumberOps.TryAsNumber(3.5, out var floating));
        Assert.True(floating.IsFloat);
        Assert.Equal(3.5, floating.Floating);
    }

    [Fact]
    public void PyNumberOps_NumericHashingMatchesCrossTypeEquality()
    {
        PyNumberOps.TryAsNumber(new BigInteger(4), out var integer);
        PyNumberOps.TryAsNumber(4.0, out var floating);

        Assert.Equal(0, PyNumberOps.Compare(integer, floating));
        Assert.Equal(PyNumberOps.GetHashCode(integer), PyNumberOps.GetHashCode(floating));
    }
}
