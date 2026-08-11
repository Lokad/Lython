using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        private readonly record struct PairedNumericValues(List<double> X, List<double> Y);

        private readonly record struct CenteredSums(double SumXX, double SumYY, double SumXY);

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
                ? RuntimeArgumentValidation.ExpectReal(arguments[1], "statistics.median_grouped(..., interval=...)", span)
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
                ? RuntimeArgumentValidation.ExpectString(arguments[2], "statistics.quantiles(..., method=...)", span)
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
                RuntimeArgumentValidation.ExpectReal(arguments[0], "statistics.LinearRegression(..., slope=...)", span),
                RuntimeArgumentValidation.ExpectReal(arguments[1], "statistics.LinearRegression(..., intercept=...)", span));
        }

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

        private static PairedNumericValues GetPairedNumericValues(object[] arguments, string owner, LythonSourceSpan span)
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

            return new PairedNumericValues(x, y);
        }

        private static CenteredSums ComputeCenteredSums(IReadOnlyList<double> x, IReadOnlyList<double> y)
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

            return new CenteredSums(sumXX, sumYY, sumXY);
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
    }
}
