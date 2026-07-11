using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime.Numbers;

internal static class PyNumberOps
{
    public static bool TryAsNumber(object value, out PyNumber number)
    {
        switch (value)
        {
            case bool boolean:
                number = PyNumber.FromBoolean(boolean);
                return true;
            case BigInteger integer:
                number = PyNumber.FromInteger(integer);
                return true;
            case double floating:
                number = PyNumber.FromFloat(floating);
                return true;
            default:
                number = default;
                return false;
        }
    }

    public static bool TryAsInteger(object value, out BigInteger integer)
    {
        if (TryAsNumber(value, out var number) && !number.IsFloat)
        {
            integer = number.Integer;
            return true;
        }

        integer = default;
        return false;
    }

    public static object Add(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer + rhs.Integer : lhs.ToDouble() + rhs.ToDouble();

    public static object Subtract(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer - rhs.Integer : lhs.ToDouble() - rhs.ToDouble();

    public static object Multiply(PyNumber lhs, PyNumber rhs)
        => !lhs.IsFloat && !rhs.IsFloat ? lhs.Integer * rhs.Integer : lhs.ToDouble() * rhs.ToDouble();

    public static object TrueDivide(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        var result = lhs.ToDouble() / rhs.ToDouble();
        if (!lhs.IsFloat && !rhs.IsFloat && !double.IsFinite(result))
        {
            throw new OverflowException("integer division result too large for a float");
        }

        return result;
    }

    public static object FloorDivide(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            return FloorDivideIntegers(lhs.Integer, rhs.Integer);
        }

        return Math.Floor(lhs.ToDouble() / rhs.ToDouble());
    }

    public static object Modulo(PyNumber lhs, PyNumber rhs)
    {
        if (rhs.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            return lhs.Integer - FloorDivideIntegers(lhs.Integer, rhs.Integer) * rhs.Integer;
        }

        var left = lhs.ToDouble();
        var right = rhs.ToDouble();
        if (double.IsFinite(left) && double.IsInfinity(right))
        {
            if (left == 0.0 || Math.CopySign(1.0, left) == Math.CopySign(1.0, right))
            {
                return left;
            }

            return right;
        }

        var quotient = Math.Floor(left / right);
        return left - quotient * right;
    }

    public static object Power(PyNumber lhs, PyNumber rhs)
    {
        if (!lhs.IsFloat && !rhs.IsFloat && rhs.Integer >= BigInteger.Zero)
        {
            return BigInteger.Pow(lhs.Integer, checked((int)rhs.Integer));
        }

        return Math.Pow(lhs.ToDouble(), rhs.ToDouble());
    }

    public static BigInteger BitwiseOr(BigInteger lhs, BigInteger rhs) => lhs | rhs;

    public static BigInteger BitwiseXor(BigInteger lhs, BigInteger rhs) => lhs ^ rhs;

    public static BigInteger BitwiseAnd(BigInteger lhs, BigInteger rhs) => lhs & rhs;

    public static BigInteger LeftShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new InvalidOperationException("negative shift count");
        }

        return lhs << checked((int)rhs);
    }

    public static BigInteger RightShift(BigInteger lhs, BigInteger rhs)
    {
        if (rhs < BigInteger.Zero)
        {
            throw new InvalidOperationException("negative shift count");
        }

        return lhs >> checked((int)rhs);
    }

    public static int Compare(PyNumber lhs, PyNumber rhs)
        => TryCompare(lhs, rhs, out var comparison) ? comparison : 0;

    public static bool AreEqual(PyNumber lhs, PyNumber rhs)
        => TryCompare(lhs, rhs, out var comparison) && comparison == 0;

    public static bool TryCompare(PyNumber lhs, PyNumber rhs, out int comparison)
    {
        if (lhs.IsFloat && double.IsNaN(lhs.Floating) || rhs.IsFloat && double.IsNaN(rhs.Floating))
        {
            comparison = 0;
            return false;
        }

        if (!lhs.IsFloat && !rhs.IsFloat)
        {
            comparison = lhs.Integer.CompareTo(rhs.Integer);
            return true;
        }

        if (!lhs.IsFloat)
        {
            comparison = CompareIntegerToFloat(lhs.Integer, rhs.Floating);
            return true;
        }

        if (!rhs.IsFloat)
        {
            comparison = -CompareIntegerToFloat(rhs.Integer, lhs.Floating);
            return true;
        }

        comparison = lhs.Floating.CompareTo(rhs.Floating);
        return true;
    }

    private static int CompareIntegerToFloat(BigInteger integer, double floating)
    {
        if (double.IsPositiveInfinity(floating))
        {
            return -1;
        }

        if (double.IsNegativeInfinity(floating))
        {
            return 1;
        }

        var truncated = new BigInteger(floating);
        var comparison = integer.CompareTo(truncated);
        if (comparison != 0 || floating == Math.Truncate(floating))
        {
            return comparison;
        }

        return floating > 0 ? -1 : 1;
    }

    public static object Negate(PyNumber number)
        => number.IsFloat ? -number.Floating : -number.Integer;

    public static BigInteger BitwiseNot(BigInteger integer) => ~integer;

    public static BigInteger ParseInteger(string text)
        => BigInteger.Parse(text.Replace("_", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

    public static double ParseFloat(string text)
        => double.Parse(text.Replace("_", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

    public static string RenderFloat(double value)
    {
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        var rendered = value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
        return rendered.Contains('.', StringComparison.Ordinal) || rendered.Contains('e', StringComparison.Ordinal)
            ? rendered
            : rendered + ".0";
    }

    public static int GetHashCode(PyNumber number)
    {
        if (number.IsFloat)
        {
            var value = number.ToDouble();
            if (double.IsFinite(value) && Math.Truncate(value) == value)
            {
                return ((BigInteger)value).GetHashCode();
            }

            return value.GetHashCode();
        }

        return number.Integer.GetHashCode();
    }

    private static BigInteger FloorDivideIntegers(BigInteger left, BigInteger right)
    {
        var quotient = BigInteger.DivRem(left, right, out var remainder);
        if (remainder != BigInteger.Zero && ((remainder > BigInteger.Zero) != (right > BigInteger.Zero)))
        {
            quotient -= BigInteger.One;
        }

        return quotient;
    }
}
