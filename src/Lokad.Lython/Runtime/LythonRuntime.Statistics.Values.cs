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
                        try
                        {
                            for (var i = 1; i < n; i++)
                            {
                                context.CheckExecution(span);
                                results.Add(InvCdf((double)i / n, span));
                            }
                            return results;
                        }
                        catch { results.DiscardUnpublishedStorage(); throw; }
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
                if (p <= 0.0 || p >= 1.0)
                {
                    throw StatisticsError("p must be in the range 0.0 < p < 1.0", span);
                }

                RequirePositiveStdev("inv_cdf()", span);
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
                try
                {
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
                catch { samples.DiscardUnpublishedStorage(); throw; }
            }

            // Wichura AS241, using CPython's binary64 coefficients and regions.
            // Scalar Horner expressions avoid separate retained coefficient arrays.
            private static double InverseStandardNormal(double p)
            {
                var q = p - 0.5;
                double r, numerator, denominator;
                if (Math.Abs(q) <= 0.425)
                {
                    r = 0.180625 - q * q;
                    numerator = (((((((2.5090809287301226727e+3 * r +
                        3.3430575583588128105e+4) * r +
                        6.7265770927008700853e+4) * r +
                        4.5921953931549871457e+4) * r +
                        1.3731693765509461125e+4) * r +
                        1.9715909503065514427e+3) * r +
                        1.3314166789178437745e+2) * r +
                        3.3871328727963666080e+0) * q;
                    denominator = (((((((5.2264952788528545610e+3 * r +
                        2.8729085735721942674e+4) * r +
                        3.9307895800092710610e+4) * r +
                        2.1213794301586595867e+4) * r +
                        5.3941960214247511077e+3) * r +
                        6.8718700749205790830e+2) * r +
                        4.2313330701600911252e+1) * r +
                        1.0);
                    return numerator / denominator;
                }
                r = Math.Sqrt(-Math.Log(q <= 0.0 ? p : 1.0 - p));
                if (r <= 5.0)
                {
                    r -= 1.6;
                    numerator = (((((((7.74545014278341407640e-4 * r +
                        2.27238449892691845833e-2) * r +
                        2.41780725177450611770e-1) * r +
                        1.27045825245236838258e+0) * r +
                        3.64784832476320460504e+0) * r +
                        5.76949722146069140550e+0) * r +
                        4.63033784615654529590e+0) * r +
                        1.42343711074968357734e+0);
                    denominator = (((((((1.05075007164441684324e-9 * r +
                        5.47593808499534494600e-4) * r +
                        1.51986665636164571966e-2) * r +
                        1.48103976427480074590e-1) * r +
                        6.89767334985100004550e-1) * r +
                        1.67638483018380384940e+0) * r +
                        2.05319162663775882187e+0) * r +
                        1.0);
                }
                else
                {
                    r -= 5.0;
                    numerator = (((((((2.01033439929228813265e-7 * r +
                        2.71155556874348757815e-5) * r +
                        1.24266094738807843860e-3) * r +
                        2.65321895265761230930e-2) * r +
                        2.96560571828504891230e-1) * r +
                        1.78482653991729133580e+0) * r +
                        5.46378491116411436990e+0) * r +
                        6.65790464350110377720e+0);
                    denominator = (((((((2.04426310338993978564e-15 * r +
                        1.42151175831644588870e-7) * r +
                        1.84631831751005468180e-5) * r +
                        7.86869131145613259100e-4) * r +
                        1.48753612908506148525e-2) * r +
                        1.36929880922735805310e-1) * r +
                        5.99832206555887937690e-1) * r +
                        1.0);
                }
                var value = numerator / denominator;
                return q < 0.0 ? -value : value;
            }

        }
    }
}
