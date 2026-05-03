using System.Numerics;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Tests;

public sealed class PyNumberPrimitiveTests
{
    [Fact]
    public void TryAsNumberAndTryAsInteger_RecognizeSupportedValues()
    {
        Assert.True(PyNumberOps.TryAsNumber(true, out var booleanNumeric));
        Assert.False(booleanNumeric.IsFloat);
        Assert.Equal(BigInteger.One, booleanNumeric.Integer);

        Assert.True(PyNumberOps.TryAsNumber(new BigInteger(42), out var integerNumeric));
        Assert.False(integerNumeric.IsFloat);
        Assert.Equal(new BigInteger(42), integerNumeric.Integer);

        Assert.True(PyNumberOps.TryAsNumber(1.5, out var floatNumeric));
        Assert.True(floatNumeric.IsFloat);
        Assert.Equal(1.5, floatNumeric.ToDouble());

        Assert.True(PyNumberOps.TryAsInteger(new BigInteger(7), out var integer));
        Assert.Equal(new BigInteger(7), integer);
        Assert.False(PyNumberOps.TryAsInteger(2.5, out _));
    }

    [Fact]
    public void ArithmeticOperations_PreservePythonNumericBehavior()
    {
        PyNumberOps.TryAsNumber(new BigInteger(7), out var seven);
        PyNumberOps.TryAsNumber(new BigInteger(3), out var three);
        PyNumberOps.TryAsNumber(2.5, out var twoPointFive);

        Assert.Equal(new BigInteger(10), PyNumberOps.Add(seven, three));
        Assert.Equal(9.5, PyNumberOps.Add(seven, twoPointFive));
        Assert.Equal(new BigInteger(4), PyNumberOps.Subtract(seven, three));
        Assert.Equal(new BigInteger(21), PyNumberOps.Multiply(seven, three));
        Assert.Equal(2.8, (double)PyNumberOps.TrueDivide(seven, twoPointFive), 10);
        Assert.Equal(new BigInteger(2), PyNumberOps.FloorDivide(seven, three));
        Assert.Equal(new BigInteger(1), PyNumberOps.Modulo(seven, three));
        Assert.Equal(new BigInteger(343), PyNumberOps.Power(seven, three));
    }

    [Fact]
    public void ComparisonAndBitwiseOperations_AreTypedInternally()
    {
        PyNumberOps.TryAsNumber(new BigInteger(4), out var four);
        PyNumberOps.TryAsNumber(4.0, out var fourFloat);
        PyNumberOps.TryAsNumber(new BigInteger(5), out var five);

        Assert.Equal(0, PyNumberOps.Compare(four, fourFloat));
        Assert.True(PyNumberOps.Compare(four, five) < 0);
        Assert.Equal(new BigInteger(7), PyNumberOps.BitwiseOr(new BigInteger(5), new BigInteger(2)));
        Assert.Equal(new BigInteger(4), PyNumberOps.BitwiseAnd(new BigInteger(6), new BigInteger(5)));
        Assert.Equal(new BigInteger(4), PyNumberOps.LeftShift(new BigInteger(1), new BigInteger(2)));
        Assert.Equal(new BigInteger(3), PyNumberOps.RightShift(new BigInteger(12), new BigInteger(2)));
    }
}
