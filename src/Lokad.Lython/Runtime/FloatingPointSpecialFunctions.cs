namespace Lokad.Lython.Runtime;

internal static class FloatingPointSpecialFunctions
{
    private const double SqrtPi = 1.772453850905516027298167483341145182798;

    public static double Erf(double value)
    {
        if (double.IsNaN(value) || value == 0.0) return value;
        var magnitude = Math.Abs(value);
        if (magnitude < 1.5) return ErfSeries(value);
        var tail = ErfcFraction(magnitude);
        return value > 0.0 ? 1.0 - tail : tail - 1.0;
    }

    public static double Erfc(double value)
    {
        if (double.IsNaN(value)) return value;
        var magnitude = Math.Abs(value);
        if (magnitude < 1.5) return 1.0 - ErfSeries(value);
        var tail = ErfcFraction(magnitude);
        return value > 0.0 ? tail : 2.0 - tail;
    }

    // Managed adaptations of CPython's portable power-series / continued-
    // fraction kernels. See THIRD_PARTY_NOTICES.md for the source and license.
    private static double ErfSeries(double value)
    {
        var square = value * value;
        var total = 0.0;
        var divisor = 25.5;
        for (var i = 0; i < 25; i++, divisor -= 1.0)
            total = 2.0 + square * total / divisor;
        return total * value * Math.Exp(-square) / SqrtPi;
    }

    private static double ErfcFraction(double value)
    {
        // The positive tail is below binary64's smallest subnormal here.
        // This also handles infinity without squaring it.
        if (value >= 30.0) return 0.0;
        var square = value * value;
        var a = 0.0;
        var increment = 0.5;
        var p = 1.0;
        var previousP = 0.0;
        var q = increment + square;
        var previousQ = 1.0;
        for (var i = 0; i < 50; i++)
        {
            a += increment;
            increment += 2.0;
            var b = increment + square;
            (p, previousP) = (b * p - a * previousP, p);
            (q, previousQ) = (b * q - a * previousQ, q);
        }
        return p / q * value * Math.Exp(-square) / SqrtPi;
    }
}
