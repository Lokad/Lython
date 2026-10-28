using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
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

            // N17: hot fixed signatures hoisted per family (see ListMembers).
            private static readonly LythonCallableSignature NormalDistZscoreSignature = LythonCallableSignature.Create("NormalDist.zscore", ["x"]);
            private static readonly LythonCallableSignature NormalDistPdfSignature = LythonCallableSignature.Create("NormalDist.pdf", ["x"]);
            private static readonly LythonCallableSignature NormalDistCdfSignature = LythonCallableSignature.Create("NormalDist.cdf", ["x"]);
            private static readonly LythonCallableSignature NormalDistInvCdfSignature = LythonCallableSignature.Create("NormalDist.inv_cdf", ["p"]);
            private static readonly LythonCallableSignature NormalDistOverlapSignature = LythonCallableSignature.Create("NormalDist.overlap", ["other"]);
            private static readonly LythonCallableSignature NormalDistQuantilesSignature = LythonCallableSignature.Create("NormalDist.quantiles", ["n"], requiredCount: 0);
            private static readonly LythonCallableSignature NormalDistSamplesSignature = LythonCallableSignature.Create("NormalDist.samples", ["n", "seed"], requiredCount: 1, maximumPositionalArgumentCount: 1);

            public double Mean { get; }

            public double Stdev { get; }

            public double Variance => Stdev * Stdev;

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("statistics"),
                    "mean" => Mean,
                    "median" => Mean,
                    "mode" => Mean,
                    "stdev" => Stdev,
                    "variance" => Variance,
                    "zscore" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.zscore(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("zscore()", span);
                        var x = RuntimeArgumentValidation.ExpectReal(arguments[0], "NormalDist.zscore(x)", span);
                        return (x - Mean) / Stdev;
                    }, NormalDistZscoreSignature),
                    "pdf" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.pdf(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("pdf()", span);
                        var x = RuntimeArgumentValidation.ExpectReal(arguments[0], "NormalDist.pdf(x)", span);
                        var z = (x - Mean) / Stdev;
                        return Math.Exp(-0.5 * z * z) * InvSqrtTau / Stdev;
                    }, NormalDistPdfSignature),
                    "cdf" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.cdf(x) expects one argument.", span);
                        }

                        RequirePositiveStdev("cdf()", span);
                        var x = RuntimeArgumentValidation.ExpectReal(arguments[0], "NormalDist.cdf(x)", span);
                        return 0.5 * (1.0 + FloatingPointSpecialFunctions.Erf((x - Mean) / (Stdev * SqrtTwo)));
                    }, NormalDistCdfSignature),
                    "inv_cdf" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.inv_cdf(p) expects one argument.", span);
                        }

                        var p = RuntimeArgumentValidation.ExpectReal(arguments[0], "NormalDist.inv_cdf(p)", span);
                        return InvCdf(p, span);
                    }, NormalDistInvCdfSignature),
                    "overlap" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 1 || arguments[0] is not PyNormalDist other)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.overlap(other) expects another NormalDist.", span);
                        }

                        return Overlap(other, span);
                    }, NormalDistOverlapSignature),
                    "quantiles" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "NormalDist.quantiles(n=4) expects zero or one argument.", span);
                        }

                        var n = arguments.Length == 1
                            ? ExpectPositivePartitionCount(arguments[0], "NormalDist.quantiles(..., n=...)", span)
                            : 4;
                        var results = new PyList(n - 1, context.MemoryGovernor, span);
                        for (var i = 1; i < n; i++)
                        {
                            results.Add(InvCdf((double)i / n, span));
                            if ((i & 63) == 0)
                            {
                                context.CheckExecution(span);
                            }
                        }

                        return results;
                    }, NormalDistQuantilesSignature),
                    "samples" => BoundCallable.Create(Samples, NormalDistSamplesSignature, SamplesAsync),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public bool Equals(PyNormalDist? other)
                            => other is not null && Mean == other.Mean && Stdev == other.Stdev;

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
                    throw StatisticsError("p must be in the range 0.0 < p < 1.0", span);
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
                    throw StatisticsError($"{owner} not defined when sigma is zero", span);
                }
            }

            private object Samples(object[] arguments, LythonSourceSpan span, ExecutionContext context)
                => SamplesCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

            private ValueTask<object> SamplesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
                => SamplesCoreAsync(arguments, span, context, true);

            private async ValueTask<object> SamplesCoreAsync(object[] arguments, LythonSourceSpan span,
                ExecutionContext context, bool asynchronous)
            {
                context.CheckExecution(span);
                var state = RandomModule.SamplingState(arguments.Length > 1 ? arguments[1] : PyNone.Instance, context, span);
                var index = asynchronous
                    ? await ConvertIntegerIndexAsync(arguments[0], context, span).ConfigureAwait(false)
                    : CoerceIndexProtocol(arguments[0], context, span);
                if (!PyNumberOps.TryAsInteger(index, out var count))
                    throw RuntimeErrors.Type("'" + RuntimeErrors.DatetimeQualifiedTypeName(arguments[0], context) + "' object cannot be interpreted as an integer", span);
                if (count > Array.MaxLength)
                    throw new LythonRuntimeException("OverflowError", "sample count exceeds the managed array representation", span);
                var length = count.Sign < 0 ? 0 : (int)count;
                // One retained backing admits capacity and numeric-box headroom
                // before the shared generator advances. No staging list/copy.
                var samples = new PyList(length, context.MemoryGovernor, span);
                for (var i = 0; i < length; i++)
                {
                    context.CheckExecution(span);
                    var probability = state.NextDouble();
                    if (probability == 0.0)
                        throw new LythonRuntimeException("ValueError", "inv_cdf undefined for these parameters", span);
                    samples.Add(Mean + Stdev * InverseStandardNormal(probability));
                }
                return samples;
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
