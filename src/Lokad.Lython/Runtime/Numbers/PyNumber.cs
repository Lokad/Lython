using System.Numerics;

namespace Lokad.Lython.Runtime.Numbers;

internal readonly record struct PyNumber
{
    private PyNumber(bool isFloat, BigInteger integer, double floating)
    {
        IsFloat = isFloat;
        Integer = integer;
        Floating = floating;
    }

    public bool IsFloat { get; }

    public BigInteger Integer { get; }

    public double Floating { get; }

    public bool IsZero => IsFloat ? Floating == 0.0 : Integer == BigInteger.Zero;

    public double ToDouble() => IsFloat ? Floating : (double)Integer;

    public static PyNumber FromBoolean(bool value)
        => new(false, value ? BigInteger.One : BigInteger.Zero, value ? 1.0 : 0.0);

    public static PyNumber FromInteger(BigInteger value)
        => new(false, value, 0.0);

    public static PyNumber FromFloat(double value)
        => new(true, BigInteger.Zero, value);
}
