using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
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
    }
}
