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
                "mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMean, Mean),
                "fmean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsFMean, FMean),
                "median" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedian, Median),
                "median_low" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianLow, MedianLow),
                "median_high" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianHigh, MedianHigh),
                "median_grouped" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianGrouped, MedianGrouped),
                "mode" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMode, Mode),
                "multimode" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMultiMode, MultiMode),
                "pstdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsPStdev, PopulationStdev),
                "stdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsStdev, SampleStdev),
                "pvariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsPVariance, PopulationVariance),
                "variance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsVariance, SampleVariance),
                "harmonic_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsHarmonicMean, HarmonicMean),
                "geometric_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsGeometricMean, GeometricMean),
                "quantiles" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsQuantiles, Quantiles),
                "covariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsCovariance, Covariance),
                "correlation" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsCorrelation, Correlation),
                "linear_regression" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsLinearRegression, LinearRegression),
                "LinearRegression" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsLinearRegressionResult, LinearRegressionResult),
                "NormalDist" => NormalDistType,
                "kde" => UnsupportedStatisticsCallable("statistics.kde"),
                "kde_random" => UnsupportedStatisticsCallable("statistics.kde_random"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }
}
