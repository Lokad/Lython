using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class StatisticsModule : PyModule
    {
        public static readonly StatisticsModule Instance = new();

        private StatisticsModule() : base("statistics")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "StatisticsError" => new ExceptionTypeValue("StatisticsError"),
                "mean" => new BuiltinCallable("statistics.mean", Mean, ["data"]),
                "fmean" => new BuiltinCallable("statistics.fmean", FMean, ["data"]),
                "median" => new BuiltinCallable("statistics.median", Median, ["data"]),
                "median_low" => new BuiltinCallable("statistics.median_low", MedianLow, ["data"]),
                "median_high" => new BuiltinCallable("statistics.median_high", MedianHigh, ["data"]),
                "mode" => new BuiltinCallable("statistics.mode", Mode, ["data"]),
                "multimode" => new BuiltinCallable("statistics.multimode", MultiMode, ["data"]),
                "pstdev" => new BuiltinCallable("statistics.pstdev", PopulationStdev, ["data"]),
                "stdev" => new BuiltinCallable("statistics.stdev", SampleStdev, ["data"]),
                "pvariance" => new BuiltinCallable("statistics.pvariance", PopulationVariance, ["data"]),
                "variance" => new BuiltinCallable("statistics.variance", SampleVariance, ["data"]),
                _ => null!,
            };

            return value is not null;
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
            var values = GetNumericValues(arguments, "statistics.fmean", span, context);
            return values.Average();
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
            var values = GetNumericObjects(arguments, "statistics.mode", span, context);
            return GetModeCounts(values).First().Key;
        }

        private static object MultiMode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericObjects(arguments, "statistics.multimode", span, context);
            var counts = GetModeCounts(values);
            var modes = new object[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                modes[i] = counts[i].Key;
            }

            return new PyList(modes, context.MemoryGovernor, span);
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
            var values = GetNumericValues(arguments, owner, span, context);
            if (sample && values.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", $"{owner}(data) requires at least two data points.", span);
            }

            var mean = values.Average();
            var sum = values.Sum(value => Math.Pow(value - mean, 2));
            return sum / (sample ? values.Count - 1 : values.Count);
        }

        private static List<KeyValuePair<object, int>> GetModeCounts(IReadOnlyList<object> values)
        {
            var counts = new Dictionary<object, int>(PyValueComparer.Instance);
            var order = new List<object>();
            foreach (var value in values)
            {
                if (counts.TryGetValue(value, out var count))
                {
                    counts[value] = count + 1;
                    continue;
                }

                counts[value] = 1;
                order.Add(value);
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

        private static bool IsWholeInteger(double value)
            => double.IsFinite(value) && Math.Abs(value % 1.0) < 1e-12;

        private static double ExpectRealForStatistics(object value, string owner, LythonSourceSpan span)
        {
            if (!Numbers.PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects real numbers.", span);
            }

            return number.ToDouble();
        }
    }
}
