using System.Numerics;

namespace Lokad.Lython.Runtime.Numbers;

internal readonly record struct PyNumber
{
    private PyNumber(BigInteger integer)
    {
        IsFloat = false;
        Integer = integer;
        Floating = 0.0;
    }

    private PyNumber(double floating)
    {
        IsFloat = true;
        Integer = BigInteger.Zero;
        Floating = floating;
    }

    public bool IsFloat { get; }

    public BigInteger Integer { get; }

    public double Floating { get; }

    public bool IsZero => IsFloat ? Floating == 0.0 : Integer == BigInteger.Zero;

    public double ToDouble() => IsFloat ? Floating : (double)Integer;

    public static PyNumber FromBoolean(bool value)
        => new(value ? BigInteger.One : BigInteger.Zero);

    public static PyNumber FromInteger(BigInteger value)
        => new(value);

    public static PyNumber FromFloat(double value)
        => new(value);
}
