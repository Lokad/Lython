using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class StatisticsModule : PyModule
    {
        public static readonly PyBuiltinRuntimeType NormalDistType = new(
            "statistics.NormalDist",
            CreateNormalDist,
            memberName => memberName switch
            {
                "from_samples" => new TypeMemberCallable("statistics.NormalDist.from_samples", NormalDistFromSamples, ["data"]),
                _ => null
            });

        public static readonly StatisticsModule Instance = new();

        private StatisticsModule() : base("statistics")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "StatisticsError" => new ExceptionTypeValue("StatisticsError"),
                "mean" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMean, Mean),
                "fmean" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsFMean, FMean),
                "median" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMedian, Median),
                "median_low" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMedianLow, MedianLow),
                "median_high" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMedianHigh, MedianHigh),
                "median_grouped" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMedianGrouped, MedianGrouped),
                "mode" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMode, Mode),
                "multimode" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsMultiMode, MultiMode),
                "pstdev" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsPStdev, PopulationStdev),
                "stdev" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsStdev, SampleStdev),
                "pvariance" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsPVariance, PopulationVariance),
                "variance" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsVariance, SampleVariance),
                "harmonic_mean" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsHarmonicMean, HarmonicMean),
                "geometric_mean" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsGeometricMean, GeometricMean),
                "quantiles" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsQuantiles, Quantiles),
                "covariance" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsCovariance, Covariance),
                "correlation" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsCorrelation, Correlation),
                "linear_regression" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsLinearRegression, LinearRegression),
                "LinearRegression" => new BuiltinCallable(LythonKnownCallableSignatures.StatisticsLinearRegressionResult, LinearRegressionResult),
                "NormalDist" => NormalDistType,
                "kde" => UnsupportedStatisticsCallable("statistics.kde"),
                "kde_random" => UnsupportedStatisticsCallable("statistics.kde_random"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Mean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValues(arguments, "statistics.mean", span, context);
            var total = values.Sum(static v => v);
            var mean = total / values.Count;
            return IsWholeInteger(mean) ? new BigInteger(mean) : mean;
        }

        private static object FMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.fmean", span, context);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                return values.Average();
            }

            var weights = GetNumericValuesFromIterable(arguments[1], "statistics.fmean(..., weights=...)", span);
            if (weights.Count != values.Count)
            {
                throw new LythonRuntimeException("StatisticsError", "data and weights must be the same length", span);
            }

            var weightedSum = 0.0;
            var weightSum = 0.0;
            for (var index = 0; index < values.Count; index++)
            {
                weightedSum += values[index] * weights[index];
                weightSum += weights[index];
            }

            if (weightSum == 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "sum of weights must be non-zero", span);
            }

            return weightedSum / weightSum;
        }

        private static object Median(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValues(arguments, "statistics.median", span, context);
            values.Sort();
            var middle = values.Count / 2;
            if (values.Count % 2 == 1)
            {
                return IsWholeInteger(values[middle]) ? new BigInteger(values[middle]) : values[middle];
            }

            var average = (values[middle - 1] + values[middle]) / 2.0;
            return IsWholeInteger(average) ? new BigInteger(average) : average;
        }

        private static object MedianLow(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericObjects(arguments, "statistics.median_low", span, context);
            values.Sort((left, right) => Compare(left, right, span));
            return values[(values.Count - 1) / 2];
        }

        private static object MedianHigh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericObjects(arguments, "statistics.median_high", span, context);
            values.Sort((left, right) => Compare(left, right, span));
            return values[values.Count / 2];
        }

        private static object Mode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetModeValues(arguments, "statistics.mode", allowEmpty: false, span, context);
            return GetModeCounts(values, span).First().Key;
        }

        private static object MultiMode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetModeValues(arguments, "statistics.multimode", allowEmpty: true, span, context);
            var counts = GetModeCounts(values, span);
            var modes = new object[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                modes[i] = counts[i].Key;
            }

            return new PyList(modes, context.MemoryGovernor, span);
        }

        private static object MedianGrouped(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.median_grouped", span, context);
            values.Sort();
            var interval = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectReal(arguments[1], "statistics.median_grouped(..., interval=...)", span)
                : 1.0;
            if (interval <= 0)
            {
                throw new LythonRuntimeException("ValueError", "statistics.median_grouped(..., interval=...) expects a positive interval.", span);
            }

            var target = values[values.Count / 2];
            var below = 0;
            var equal = 0;
            foreach (var value in values)
            {
                if (value < target)
                {
                    below++;
                }
                else if (value == target)
                {
                    equal++;
                }
            }

            var lowerLimit = target - interval / 2.0;
            return lowerLimit + interval * (values.Count / 2.0 - below) / equal;
        }

        private static object HarmonicMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var data = GetNumericValuesFromData(arguments, "statistics.harmonic_mean", span, context);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var reciprocalTotal = 0.0;
                foreach (var value in data)
                {
                    if (value < 0)
                    {
                        throw new LythonRuntimeException("StatisticsError", "harmonic mean does not support negative values", span);
                    }

                    if (value == 0.0)
                    {
                        return 0.0;
                    }

                    reciprocalTotal += 1.0 / value;
                }

                return data.Count / reciprocalTotal;
            }

            var weights = GetNumericValuesFromIterable(arguments[1], "statistics.harmonic_mean(..., weights=...)", span);
            if (weights.Count != data.Count)
            {
                throw new LythonRuntimeException("StatisticsError", "Number of weights does not match data size", span);
            }

            var weightTotal = 0.0;
            var reciprocalTotalWeighted = 0.0;
            for (var i = 0; i < data.Count; i++)
            {
                var value = data[i];
                var weight = weights[i];
                if (value < 0 || weight < 0)
                {
                    throw new LythonRuntimeException("StatisticsError", "harmonic mean does not support negative values", span);
                }

                if (value == 0.0 && weight > 0.0)
                {
                    return 0.0;
                }

                weightTotal += weight;
                if (weight != 0.0)
                {
                    reciprocalTotalWeighted += weight / value;
                }
            }

            if (weightTotal <= 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "Weighted sum must be positive", span);
            }

            return weightTotal / reciprocalTotalWeighted;
        }

        private static object GeometricMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.geometric_mean", span, context);
            var logTotal = 0.0;
            foreach (var value in values)
            {
                if (value < 0)
                {
                    throw new LythonRuntimeException("StatisticsError", "geometric mean does not support negative values", span);
                }

                if (value == 0.0)
                {
                    return 0.0;
                }

                logTotal += Math.Log(value);
            }

            return Math.Exp(logTotal / values.Count);
        }

        private static object PopulationStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(ComputeVariance(arguments, "statistics.pstdev", span, context, sample: false));

        private static object SampleStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(ComputeVariance(arguments, "statistics.stdev", span, context, sample: true));

        private static object PopulationVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ComputeVariance(arguments, "statistics.pvariance", span, context, sample: false);

        private static object SampleVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ComputeVariance(arguments, "statistics.variance", span, context, sample: true);

        private static double ComputeVariance(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, bool sample)
        {
            var values = GetNumericValuesFromData(arguments, owner, span, context);
            if (sample && values.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner}(data) requires at least two data points.", span);
            }

            var mean = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectRealForStatistics(arguments[1], owner, span)
                : values.Average();
            var sum = values.Sum(value => Math.Pow(value - mean, 2));
            return sum / (sample ? values.Count - 1 : values.Count);
        }

        private static object Quantiles(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.quantiles", span, context);
            values.Sort();
            var n = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectPositivePartitionCount(arguments[1], "statistics.quantiles(..., n=...)", span)
                : 4;
            var method = arguments.Length >= 3 && arguments[2] is not PyNone
                ? ExpectText(arguments[2], "statistics.quantiles(..., method=...)", span)
                : "exclusive";
            if (method != "exclusive" && method != "inclusive")
            {
                throw new LythonRuntimeException("ValueError", $"Unknown method: '{method}'", span);
            }

            if (n == 1)
            {
                return new PyList([], context.MemoryGovernor, span);
            }

            var cutPoints = new List<object>(n - 1);
            var count = values.Count;
            for (var i = 1; i < n; i++)
            {
                var value = method == "inclusive"
                    ? InterpolateInclusiveQuantile(values, i, n)
                    : InterpolateExclusiveQuantile(values, i, n);
                cutPoints.Add(BoxStatisticalFloat(value));
            }

            return new PyList(cutPoints, context.MemoryGovernor, span);
        }

        private static object Covariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var (x, y) = GetPairedNumericValues(arguments, "statistics.covariance", span);
            if (x.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", "covariance requires at least two data points", span);
            }

            var (xMean, yMean) = (x.Average(), y.Average());
            var sum = 0.0;
            for (var i = 0; i < x.Count; i++)
            {
                sum += (x[i] - xMean) * (y[i] - yMean);
            }

            return sum / (x.Count - 1);
        }

        private static object Correlation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var (x, y) = GetPairedNumericValues(arguments, "statistics.correlation", span);
            if (x.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", "correlation requires at least two data points", span);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0 || sums.SumYY == 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "at least one of the inputs is constant", span);
            }

            return sums.SumXY / Math.Sqrt(sums.SumXX * sums.SumYY);
        }

        private static object LinearRegression(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 2 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "statistics.linear_regression(x, y) expects two iterable arguments.", span);
            }

            var (x, y) = GetPairedNumericValues([arguments[0], arguments[1]], "statistics.linear_regression", span);
            if (x.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", "linear_regression requires at least two data points", span);
            }

            var proportional = arguments.Length >= 3 && arguments[2] is not PyNone && IsTruthy(arguments[2]);
            if (proportional)
            {
                var sumXX = 0.0;
                var sumXY = 0.0;
                for (var i = 0; i < x.Count; i++)
                {
                    sumXX += x[i] * x[i];
                    sumXY += x[i] * y[i];
                }

                if (sumXX == 0.0)
                {
                    throw new LythonRuntimeException("StatisticsError", "x is constant", span);
                }

                return new StatisticsLinearRegressionResult(sumXY / sumXX, 0.0);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "x is constant", span);
            }

            var slope = sums.SumXY / sums.SumXX;
            var intercept = y.Average() - slope * x.Average();
            return new StatisticsLinearRegressionResult(slope, intercept);
        }

        private static object LinearRegressionResult(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "statistics.LinearRegression(slope, intercept) expects two arguments.", span);
            }

            return new StatisticsLinearRegressionResult(
                ExpectReal(arguments[0], "statistics.LinearRegression(..., slope=...)", span),
                ExpectReal(arguments[1], "statistics.LinearRegression(..., intercept=...)", span));
        }

        private static BuiltinCallable UnsupportedStatisticsCallable(string qualifiedName)
            => new(
                qualifiedName,
                (arguments, span, context) =>
                {
                    _ = arguments;
                    _ = context;
                    throw new LythonRuntimeException("NotImplementedError", qualifiedName + " is unsupported by Lython.", span);
                });

        private static List<KeyValuePair<object, int>> GetModeCounts(IReadOnlyList<object> values, LythonSourceSpan span)
        {
            var counts = new Dictionary<object, int>(PyValueComparer.Instance);
            var order = new List<object>();
            foreach (var value in values)
            {
                try
                {
                    if (counts.TryGetValue(value, out var count))
                    {
                        counts[value] = count + 1;
                        continue;
                    }

                    counts[value] = 1;
                    order.Add(value);
                }
                catch (InvalidOperationException)
                {
                    throw new LythonRuntimeException("TypeError", "unhashable type", span);
                }
            }

            var maxCount = 0;
            foreach (var count in counts.Values)
            {
                maxCount = Math.Max(maxCount, count);
            }

            var result = new List<KeyValuePair<object, int>>();
            foreach (var value in order)
            {
                var count = counts[value];
                if (count == maxCount)
                {
                    result.Add(new KeyValuePair<object, int>(value, count));
                }
            }

            return result;
        }

        private static List<object> GetModeValues(
            object[] arguments,
            string owner,
            bool allowEmpty,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            var values = new List<object>();
            foreach (var value in ToSequence(arguments[0], span, context))
            {
                values.Add(value);
            }

            if (!allowEmpty && values.Count == 0)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner}(data) requires at least one data point.", span);
            }

            return values;
        }

        private static (List<double> X, List<double> Y) GetPairedNumericValues(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two iterable arguments.", span);
            }

            var x = GetNumericValuesFromIterable(arguments[0], owner + "(x, y)", span);
            var y = GetNumericValuesFromIterable(arguments[1], owner + "(x, y)", span);
            if (x.Count != y.Count)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner.Split('.').Last()} requires that both inputs have same number of data points", span);
            }

            return (x, y);
        }

        private static (double SumXX, double SumYY, double SumXY) ComputeCenteredSums(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            var xMean = x.Average();
            var yMean = y.Average();
            var sumXX = 0.0;
            var sumYY = 0.0;
            var sumXY = 0.0;
            for (var i = 0; i < x.Count; i++)
            {
                var dx = x[i] - xMean;
                var dy = y[i] - yMean;
                sumXX += dx * dx;
                sumYY += dy * dy;
                sumXY += dx * dy;
            }

            return (sumXX, sumYY, sumXY);
        }

        private static double InterpolateInclusiveQuantile(IReadOnlyList<double> values, int cut, int partitions)
        {
            if (values.Count == 1)
            {
                return values[0];
            }

            var index = cut * (values.Count - 1.0) / partitions;
            var below = (int)Math.Floor(index);
            var above = Math.Min(below + 1, values.Count - 1);
            return values[below] + (values[above] - values[below]) * (index - below);
        }

        private static double InterpolateExclusiveQuantile(IReadOnlyList<double> values, int cut, int partitions)
        {
            if (values.Count == 1)
            {
                return values[0];
            }

            var position = cut * (values.Count + 1.0) / partitions;
            if (position <= 1.0)
            {
                return values[0] + (values[1] - values[0]) * (position - 1.0);
            }

            if (position >= values.Count)
            {
                var last = values.Count - 1;
                return values[last - 1] + (values[last] - values[last - 1]) * (position - (values.Count - 1));
            }

            var below = (int)Math.Floor(position);
            var fraction = position - below;
            return values[below - 1] + (values[below] - values[below - 1]) * fraction;
        }

        private static int ExpectPositivePartitionCount(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an integer.", span);
            }

            if (integer < BigInteger.One || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects n >= 1.", span);
            }

            return (int)integer;
        }

        private static List<object> GetNumericObjects(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            var values = new List<object>();
            foreach (var value in ToSequence(arguments[0], span))
            {
                values.Add(value);
            }

            if (values.Count == 0)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner}(data) requires at least one data point.", span);
            }

            foreach (var value in values)
            {
                _ = ExpectRealForStatistics(value, owner, span);
            }

            return values;
        }

        private static List<double> GetNumericValuesFromData(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            if (arguments.Length > 1)
            {
                _ = context;
            }

            return GetNumericValuesFromIterable(arguments[0], owner, span);
        }

        private static List<double> GetNumericValuesFromIterable(object data, string owner, LythonSourceSpan span)
        {
            var values = new List<double>();
            foreach (var value in ToSequence(data, span))
            {
                values.Add(ExpectRealForStatistics(value, owner, span));
            }

            if (values.Count == 0)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner} requires at least one data point.", span);
            }

            return values;
        }

        private static List<double> GetNumericValues(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericObjects(arguments, owner, span, context);
            var result = new List<double>(values.Count);
            foreach (var value in values)
            {
                result.Add(ExpectRealForStatistics(value, owner, span));
            }

            return result;
        }

        private static object CreateNormalDist(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.StatisticsNormalDist,
                PythonCallableKind.Builtin);
            var mean = bound.Length >= 1 && bound[0] is not PyNone
                ? ExpectReal(bound[0], "statistics.NormalDist(..., mu=...)", span)
                : 0.0;
            var stdev = bound.Length >= 2 && bound[1] is not PyNone
                ? ExpectReal(bound[1], "statistics.NormalDist(..., sigma=...)", span)
                : 1.0;
            if (stdev < 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "sigma must be non-negative", span);
            }

            _ = context;
            return new PyNormalDist(mean, stdev);
        }

        private static object NormalDistFromSamples(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.NormalDist.from_samples", span, context);
            if (values.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", "statistics.NormalDist.from_samples(data) requires at least two data points.", span);
            }

            var mean = values.Average();
            var sum = values.Sum(value => Math.Pow(value - mean, 2));
            return new PyNormalDist(mean, Math.Sqrt(sum / (values.Count - 1)));
        }

        public static bool TryAddNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => new PyNormalDist(lhs.Mean + rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance)),
                (PyNormalDist lhs, _) when TryAsReal(right, out var amount) => new PyNormalDist(lhs.Mean + amount, lhs.Stdev),
                (_, PyNormalDist rhs) when TryAsReal(left, out var amount) => new PyNormalDist(amount + rhs.Mean, rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TrySubtractNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = span;
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => new PyNormalDist(lhs.Mean - rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance)),
                (PyNormalDist lhs, _) when TryAsReal(right, out var amount) => new PyNormalDist(lhs.Mean - amount, lhs.Stdev),
                (_, PyNormalDist rhs) when TryAsReal(left, out var amount) => new PyNormalDist(amount - rhs.Mean, rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryMultiplyNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = span;
            value = (left, right) switch
            {
                (PyNormalDist lhs, _) when TryAsReal(right, out var factor) => new PyNormalDist(lhs.Mean * factor, lhs.Stdev * Math.Abs(factor)),
                (_, PyNormalDist rhs) when TryAsReal(left, out var factor) => new PyNormalDist(factor * rhs.Mean, Math.Abs(factor) * rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryDivideNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = null;
            if (left is not PyNormalDist lhs || !TryAsReal(right, out var divisor))
            {
                return false;
            }

            if (divisor == 0.0)
            {
                throw new LythonRuntimeException("ValueError", "division by zero", span);
            }

            value = new PyNormalDist(lhs.Mean / divisor, lhs.Stdev / Math.Abs(divisor));
            return true;
        }

        public static bool TryUnaryNormalDist(object operand, bool negative, [MaybeNullWhen(false)] out object value)
        {
            if (operand is not PyNormalDist dist)
            {
                value = null;
                return false;
            }

            value = negative ? new PyNormalDist(-dist.Mean, dist.Stdev) : new PyNormalDist(dist.Mean, dist.Stdev);
            return true;
        }

        private static bool TryAsReal(object value, out double real)
        {
            if (PyNumberOps.TryAsNumber(value, out var number))
            {
                real = number.ToDouble();
                return true;
            }

            if (value is PyDecimal decimalValue)
            {
                real = (double)decimalValue.Value;
                return true;
            }

            real = default;
            return false;
        }

        private static double ExpectReal(object value, string owner, LythonSourceSpan span)
        {
            if (!TryAsReal(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            return real;
        }

        private static string ExpectText(object value, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a string.", span);
            }

            return text.AsString();
        }

        private static object BoxStatisticalFloat(double value)
            => IsWholeInteger(value) ? new BigInteger(value) : value;

        private static bool IsWholeInteger(double value)
            => double.IsFinite(value) && Math.Abs(value % 1.0) < 1e-12;

        private static double ExpectRealForStatistics(object value, string owner, LythonSourceSpan span)
        {
            if (!TryAsReal(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects real numbers.", span);
            }

            return real;
        }

        private sealed class TypeMemberCallable : ICallable
        {
            private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
            private readonly LythonCallableSignature _signature;

            public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string[] parameterNames)
            {
                _signature = new LythonCallableSignature(name, parameterNames);
                _implementation = implementation;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
                return _implementation(positional, span, context);
            }
        }

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
