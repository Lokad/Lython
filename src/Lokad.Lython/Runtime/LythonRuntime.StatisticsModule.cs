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
            }, CreateNormalDistAsync);

        private readonly PyNamedTupleType _regressionType;

        internal StatisticsModule(ExecutionContext context) : base("statistics")
        {
            _regressionType = context.State.StatisticsRegressionType ??= new PyNamedTupleType(
                "LinearRegression", ["slope", "intercept"], null, context.MemoryGovernor, null)
            { ModuleName = ExceptionTypeValue.SharedModuleLabel("statistics") };
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "StatisticsError" => new ExceptionTypeValue(ModuleException("statistics", "StatisticsError")),
                "mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Mean, Mean, MeanAsync),
                "fmean" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.FMean, FMean, FMeanAsync),
                "median" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Median, Median, MedianAsync),
                "median_low" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.MedianLow, MedianLow, MedianLowAsync),
                "median_high" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.MedianHigh, MedianHigh, MedianHighAsync),
                "median_grouped" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.MedianGrouped, MedianGrouped, MedianGroupedAsync),
                "mode" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Mode, Mode, ModeAsync),
                "multimode" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.MultiMode, MultiMode, MultiModeAsync),
                "pstdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.PStdev, PopulationStdev, PopulationStdevAsync),
                "stdev" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Stdev, SampleStdev, SampleStdevAsync),
                "pvariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.PVariance, PopulationVariance, PopulationVarianceAsync),
                "variance" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Variance, SampleVariance, SampleVarianceAsync),
                "harmonic_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.HarmonicMean, HarmonicMean, HarmonicMeanAsync),
                "geometric_mean" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.GeometricMean, GeometricMean, GeometricMeanAsync),
                "quantiles" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Quantiles, Quantiles, QuantilesAsync),
                "covariance" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Covariance, Covariance, CovarianceAsync),
                "correlation" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.Correlation, Correlation, CorrelationAsync),
                "linear_regression" => BuiltinCallable.Create(LythonKnownCallableSignatures.Statistics.LinearRegression, LinearRegression, LinearRegressionAsync),
                "LinearRegression" => _regressionType,
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
