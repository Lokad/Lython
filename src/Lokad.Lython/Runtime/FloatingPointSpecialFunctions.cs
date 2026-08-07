namespace Lokad.Lython.Runtime;

internal static class FloatingPointSpecialFunctions
{
    public static double Erf(double value)
    {
        if (double.IsNaN(value))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value))
        {
            return 1.0;
        }

        if (double.IsNegativeInfinity(value))
        {
            return -1.0;
        }

        if (value == 0.0)
        {
            return value;
        }

        var sign = Math.Sign(value);
        var x = Math.Abs(value);
        var tail = ApproximatePositiveTail(x);
        var result = 1.0 - tail;
        return sign < 0 ? -result : result;
    }

    public static double Erfc(double value)
    {
        if (double.IsNaN(value))
        {
            return double.NaN;
        }

        if (double.IsPositiveInfinity(value))
        {
            return 0.0;
        }

        if (double.IsNegativeInfinity(value))
        {
            return 2.0;
        }

        if (value == 0.0)
        {
            return 1.0;
        }

        var tail = ApproximatePositiveTail(Math.Abs(value));
        return value < 0 ? 2.0 - tail : tail;
    }

    private static double ApproximatePositiveTail(double value)
    {
        var t = 1.0 / (1.0 + 0.3275911 * value);
        var polynomial = (((((1.061405429 * t) - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t;
        return polynomial * Math.Exp(-value * value);
    }
}
