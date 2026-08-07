using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        internal sealed class StatisticsLinearRegressionResult :
            IPySequenceValue,
            IPyIndexableValue,
            IPyIterableValue,
            IPyRenderableValue,
            IPyDynamicAttributes,
            IPyHashableValue,
            IEquatable<StatisticsLinearRegressionResult>
        {
            public StatisticsLinearRegressionResult(double slope, double intercept)
            {
                Slope = slope;
                Intercept = intercept;
            }

            public double Slope { get; }

            public double Intercept { get; }

            public int Count => 2;

            public int Length => 2;

            public object this[int index] => GetItem(index);

            public object GetItem(int index)
                => index switch
                {
                    0 => Slope,
                    1 => Intercept,
                    _ => throw new ArgumentOutOfRangeException(nameof(index))
                };

            public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

            public object GetIndex(int index) => GetItem(index);

            public object GetSlice(IEnumerable<int> indices)
                => new PyTuple(indices.Select(GetItem));

            public IEnumerator<object> GetEnumerator()
            {
                yield return Slope;
                yield return Intercept;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public IEnumerable<object> Iterate() => this;

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "slope" => Slope,
                    "intercept" => Intercept,
                    "_fields" => new PyTuple([
                        PyString.FromString("slope"),
                        PyString.FromString("intercept")]),
                    "_asdict" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", "LinearRegression._asdict() expects no arguments.", span);
                        }

                        var dict = new PyDict(context.MemoryGovernor, span);
                        dict.SetItem(PyString.FromString("slope"), Slope);
                        dict.SetItem(PyString.FromString("intercept"), Intercept);
                        return dict;
                    }, "LinearRegression._asdict", []),
                    "_replace" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "LinearRegression._replace(slope, intercept) expects zero to two field values.", span);
                        }

                        return new StatisticsLinearRegressionResult(
                            arguments.Length >= 1 && arguments[0] is not PyNone ? ExpectReal(arguments[0], "LinearRegression._replace(..., slope=...)", span) : Slope,
                            arguments.Length >= 2 && arguments[1] is not PyNone ? ExpectReal(arguments[1], "LinearRegression._replace(..., intercept=...)", span) : Intercept);
                    }, "LinearRegression._replace", ["slope", "intercept"], requiredCount: 0),
                    "count" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "LinearRegression.count(value) expects one argument.", span);
                        }

                        var count = 0;
                        foreach (var item in this)
                        {
                            if (PyEquality.AreEqual(item, arguments[0]))
                            {
                                count++;
                            }
                        }

                        return new BigInteger(count);
                    }, "LinearRegression.count", ["value"]),
                    "index" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "LinearRegression.index(value[, start[, stop]]) expects one to three arguments.", span);
                        }

                        var start = NormalizeLinearRegressionSearchBound(arguments.Length >= 2 ? arguments[1] : null, 0, span);
                        var stop = NormalizeLinearRegressionSearchBound(arguments.Length >= 3 ? arguments[2] : null, Count, span);
                        for (var i = start; i < stop; i++)
                        {
                            if (PyEquality.AreEqual(GetItem(i), arguments[0]))
                            {
                                return new BigInteger(i);
                            }
                        }

                        throw new LythonRuntimeException("ValueError", "LinearRegression.index(value): value is not in tuple", span);
                    }, "LinearRegression.index", ["value", "start", "stop"], requiredCount: 1),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public bool Equals(StatisticsLinearRegressionResult? other)
                => other is not null && Slope.Equals(other.Slope) && Intercept.Equals(other.Intercept);

            public override bool Equals(object? obj) => obj is StatisticsLinearRegressionResult other && Equals(other);

            public override int GetHashCode() => GetPyHashCode();

            public int GetPyHashCode() => HashCode.Combine(Slope, Intercept);

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString(FormattableString.Invariant($"LinearRegression(slope={Slope}, intercept={Intercept})"));
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private static int NormalizeLinearRegressionSearchBound(object? value, int defaultValue, LythonSourceSpan span)
            {
                if (value is null)
                {
                    return defaultValue;
                }

                if (value is not BigInteger integer)
                {
                    throw new LythonRuntimeException("TypeError", "LinearRegression.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
                }

                if (integer < int.MinValue)
                {
                    return 0;
                }

                if (integer > int.MaxValue)
                {
                    return 2;
                }

                var index = (int)integer;
                if (index < 0)
                {
                    index += 2;
                }

                return Math.Clamp(index, 0, 2);
            }
        }

        internal sealed class PyNormalDist :
            IPyRenderableValue,
            IPyDynamicAttributes,
            IPyHashableValue,
            IEquatable<PyNormalDist>
        {
            private const double InvSqrtTau = 0.39894228040143267794;
            private const double SqrtTwo = 1.4142135623730950488;
            private static readonly double[] InverseNormalCentralNumerator =
            [
                -3.969683028665376e+01,
                2.209460984245205e+02,
                -2.759285104469687e+02,
                1.383577518672690e+02,
                -3.066479806614716e+01,
                2.506628277459239e+00,
            ];
            private static readonly double[] InverseNormalCentralDenominator =
            [
                -5.447609879822406e+01,
                1.615858368580409e+02,
                -1.556989798598866e+02,
                6.680131188771972e+01,
                -1.328068155288572e+01,
            ];
            private static readonly double[] InverseNormalTailNumerator =
            [
                -7.784894002430293e-03,
                -3.223964580411365e-01,
                -2.400758277161838e+00,
                -2.549732539343734e+00,
                4.374664141464968e+00,
                2.938163982698783e+00,
            ];
            private static readonly double[] InverseNormalTailDenominator =
            [
                7.784695709041462e-03,
                3.224671290700398e-01,
                2.445134137142996e+00,
                3.754408661907416e+00,
            ];

            public PyNormalDist(double mean, double stdev)
            {
                Mean = mean;
                Stdev = stdev;
            }

            public double Mean { get; }

            public double Stdev { get; }

            public double Variance => Stdev * Stdev;

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "mean" => Mean,
                    "median" => Mean,
                    "mode" => Mean,
                    "stdev" => Stdev,
                    "variance" => Variance,
                    "zscore" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.zscore(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("zscore()", span);
                        var x = ExpectReal(arguments[0], "NormalDist.zscore(x)", span);
                        return (x - Mean) / Stdev;
                    }, "NormalDist.zscore", ["x"]),
                    "pdf" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.pdf(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("pdf()", span);
                        var x = ExpectReal(arguments[0], "NormalDist.pdf(x)", span);
                        var z = (x - Mean) / Stdev;
                        return Math.Exp(-0.5 * z * z) * InvSqrtTau / Stdev;
                    }, "NormalDist.pdf", ["x"]),
                    "cdf" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.cdf(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("cdf()", span);
                        var x = ExpectReal(arguments[0], "NormalDist.cdf(x)", span);
                        return 0.5 * (1.0 + FloatingPointSpecialFunctions.Erf((x - Mean) / (Stdev * SqrtTwo)));
                    }, "NormalDist.cdf", ["x"]),
                    "inv_cdf" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.inv_cdf(p) expects one argument.", span);
                        }

                        var p = ExpectReal(arguments[0], "NormalDist.inv_cdf(p)", span);
                        return InvCdf(p, span);
                    }, "NormalDist.inv_cdf", ["p"]),
                    "overlap" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || arguments[0] is not PyNormalDist other)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.overlap(other) expects another NormalDist.", span);
                        }

                        return Overlap(other, span);
                    }, "NormalDist.overlap", ["other"]),
                    "quantiles" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.quantiles(n=4) expects zero or one argument.", span);
                        }

                        var n = arguments.Length == 1 && arguments[0] is not PyNone
                            ? ExpectPositivePartitionCount(arguments[0], "NormalDist.quantiles(..., n=...)", span)
                            : 4;
                        var results = new List<object>(Math.Max(0, n - 1));
                        for (var i = 1; i < n; i++)
                        {
                            results.Add(InvCdf((double)i / n, span));
                        }

                        return new PyList(results, context.MemoryGovernor, span);
                    }, "NormalDist.quantiles", ["n"], requiredCount: 0),
                    "samples" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length is < 1 or > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.samples(n, seed=None) expects one or two arguments.", span);
                        }

                        if (!PyNumberOps.TryAsInteger(arguments[0], out var nInteger) || nInteger < BigInteger.Zero || nInteger > int.MaxValue)
                        {
                            throw new LythonRuntimeException("ValueError", "NormalDist.samples(n, seed=None) expects a non-negative integer n.", span);
                        }

                        var seed = arguments.Length >= 2 && arguments[1] is not PyNone
                            ? ExpectSeed(arguments[1], span)
                            : 0;
                        var random = new Random(seed);
                        var samples = new List<object>((int)nInteger);
                        for (var i = 0; i < (int)nInteger; i++)
                        {
                            samples.Add(Mean + Stdev * NextGaussian(random));
                        }

                        return new PyList(samples, context.MemoryGovernor, span);
                    }, "NormalDist.samples", ["n", "seed"], requiredCount: 1),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public bool Equals(PyNormalDist? other)
                => other is not null && Mean.Equals(other.Mean) && Stdev.Equals(other.Stdev);

            public override bool Equals(object? obj) => obj is PyNormalDist other && Equals(other);

            public override int GetHashCode() => GetPyHashCode();

            public int GetPyHashCode() => HashCode.Combine(Mean, Stdev);

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString(FormattableString.Invariant($"NormalDist(mu={Mean}, sigma={Stdev})"));
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            public double InvCdf(double p, LythonSourceSpan span)
            {
                RequirePositiveStdev("inv_cdf()", span);
                if (p <= 0.0 || p >= 1.0)
                {
                    throw new LythonRuntimeException("StatisticsError", "p must be in the range 0.0 < p < 1.0", span);
                }

                return Mean + Stdev * InverseStandardNormal(p);
            }

            public double Cdf(double x, LythonSourceSpan span)
            {
                RequirePositiveStdev("cdf()", span);
                return 0.5 * (1.0 + FloatingPointSpecialFunctions.Erf((x - Mean) / (Stdev * SqrtTwo)));
            }

            private double Overlap(PyNormalDist other, LythonSourceSpan span)
            {
                RequirePositiveStdev("overlap()", span);
                other.RequirePositiveStdev("overlap()", span);
                if (Stdev == other.Stdev)
                {
                    return 1.0 - FloatingPointSpecialFunctions.Erf(Math.Abs(Mean - other.Mean) / (2.0 * Stdev * SqrtTwo));
                }

                var variance = Variance;
                var otherVariance = other.Variance;
                var varianceDelta = variance - otherVariance;
                var meanDelta = Math.Abs(Mean - other.Mean);
                var a = Mean * otherVariance - other.Mean * variance;
                var b = Stdev * other.Stdev * Math.Sqrt(meanDelta * meanDelta + varianceDelta * Math.Log(variance / otherVariance));
                var x1 = (a + b) / varianceDelta;
                var x2 = (a - b) / varianceDelta;
                return 1.0 - (Math.Abs(Cdf(x1, span) - other.Cdf(x1, span)) + Math.Abs(Cdf(x2, span) - other.Cdf(x2, span)));
            }

            private void RequirePositiveStdev(string owner, LythonSourceSpan span)
            {
                if (Stdev <= 0.0)
                {
                    throw new LythonRuntimeException("StatisticsError", $"{owner} not defined when sigma is zero", span);
                }
            }

            private static int ExpectSeed(object value, LythonSourceSpan span)
            {
                if (!PyNumberOps.TryAsInteger(value, out var integer))
                {
                    throw new LythonRuntimeException("TypeError", "NormalDist.samples(..., seed=...) expects an integer seed.", span);
                }

                return (int)(integer & int.MaxValue);
            }

            private static double NextGaussian(Random random)
            {
                var u1 = 1.0 - random.NextDouble();
                var u2 = 1.0 - random.NextDouble();
                return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            }

            private static double InverseStandardNormal(double p)
            {
                // Peter J. Acklam's piecewise rational approximation uses this split to select
                // the tail coefficients without sacrificing precision in the central region.
                const double low = 0.02425;
                const double high = 1.0 - low;
                if (p < low)
                {
                    var q = Math.Sqrt(-2.0 * Math.Log(p));
                    return (((((InverseNormalTailNumerator[0] * q + InverseNormalTailNumerator[1]) * q + InverseNormalTailNumerator[2]) * q + InverseNormalTailNumerator[3]) * q + InverseNormalTailNumerator[4]) * q + InverseNormalTailNumerator[5]) /
                           ((((InverseNormalTailDenominator[0] * q + InverseNormalTailDenominator[1]) * q + InverseNormalTailDenominator[2]) * q + InverseNormalTailDenominator[3]) * q + 1.0);
                }

                if (p <= high)
                {
                    var q = p - 0.5;
                    var r = q * q;
                    return (((((InverseNormalCentralNumerator[0] * r + InverseNormalCentralNumerator[1]) * r + InverseNormalCentralNumerator[2]) * r + InverseNormalCentralNumerator[3]) * r + InverseNormalCentralNumerator[4]) * r + InverseNormalCentralNumerator[5]) * q /
                           (((((InverseNormalCentralDenominator[0] * r + InverseNormalCentralDenominator[1]) * r + InverseNormalCentralDenominator[2]) * r + InverseNormalCentralDenominator[3]) * r + InverseNormalCentralDenominator[4]) * r + 1.0);
                }

                var upperQ = Math.Sqrt(-2.0 * Math.Log(1.0 - p));
                return -(((((InverseNormalTailNumerator[0] * upperQ + InverseNormalTailNumerator[1]) * upperQ + InverseNormalTailNumerator[2]) * upperQ + InverseNormalTailNumerator[3]) * upperQ + InverseNormalTailNumerator[4]) * upperQ + InverseNormalTailNumerator[5]) /
                       ((((InverseNormalTailDenominator[0] * upperQ + InverseNormalTailDenominator[1]) * upperQ + InverseNormalTailDenominator[2]) * upperQ + InverseNormalTailDenominator[3]) * upperQ + 1.0);
            }
        }
    }
}
