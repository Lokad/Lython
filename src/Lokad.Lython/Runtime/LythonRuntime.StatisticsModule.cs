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
                "from_samples" => BuiltinCallable.Create("statistics.NormalDist.from_samples", NormalDistFromSamples, NormalDistFromSamplesAsync, ["data"]),
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
                "StatisticsError" => new ExceptionTypeValue(ModuleException("statistics", "StatisticsError")),
                "mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMean, Mean, MeanAsync),
                "fmean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsFMean, FMean, FMeanAsync),
                "median" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedian, Median, MedianAsync),
                "median_low" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianLow, MedianLow, MedianLowAsync),
                "median_high" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianHigh, MedianHigh, MedianHighAsync),
                "median_grouped" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMedianGrouped, MedianGrouped, MedianGroupedAsync),
                "mode" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMode, Mode, ModeAsync),
                "multimode" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsMultiMode, MultiMode, MultiModeAsync),
                "pstdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsPStdev, PopulationStdev, PopulationStdevAsync),
                "stdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsStdev, SampleStdev, SampleStdevAsync),
                "pvariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsPVariance, PopulationVariance, PopulationVarianceAsync),
                "variance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsVariance, SampleVariance, SampleVarianceAsync),
                "harmonic_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsHarmonicMean, HarmonicMean, HarmonicMeanAsync),
                "geometric_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsGeometricMean, GeometricMean, GeometricMeanAsync),
                "quantiles" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsQuantiles, Quantiles, QuantilesAsync),
                "covariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsCovariance, Covariance, CovarianceAsync),
                "correlation" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsCorrelation, Correlation, CorrelationAsync),
                "linear_regression" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsLinearRegression, LinearRegression, LinearRegressionAsync),
                "LinearRegression" => BuiltinCallable.Create(LythonKnownCallableSignatures.StatisticsLinearRegressionResult, LinearRegressionResult),
                "NormalDist" => NormalDistType,
                "kde" => BuiltinCallable.CreateUnsupported("statistics.kde"),
                "kde_random" => BuiltinCallable.CreateUnsupported("statistics.kde_random"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static LythonRuntimeException StatisticsError(string message, LythonSourceSpan span)
        => new(ModuleException("statistics", "StatisticsError"), message, span);
}
