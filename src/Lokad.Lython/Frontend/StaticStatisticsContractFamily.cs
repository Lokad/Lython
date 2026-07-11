using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticStatisticsContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsFMean.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.fmean(data, weights=None) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeIterableOrNoneArgument(arguments, 1, "weights", "statistics.fmean(..., weights=...) expects an iterable or None.", diagnostics, bindings);
            return emitted;
        }

        if (IsVarianceCall(targetName, out var centerName))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", $"{targetName}(data) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeRealOrNoneArgument(arguments, 1, centerName, $"{targetName}(..., {centerName}=...) expects a real number or None.", diagnostics, bindings);
            return emitted;
        }

        if (IsSingleDataCall(targetName))
        {
            return AnalyzeIterableArgument(arguments, 0, "data", $"{targetName}(data) expects an iterable.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsHarmonicMean.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.harmonic_mean(data, *, weights=None) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeIterableOrNoneArgument(arguments, 1, "weights", "statistics.harmonic_mean(..., weights=...) expects an iterable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMedianGrouped.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.median_grouped(data[, interval]) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeRealArgument(arguments, 1, "interval", "statistics.median_grouped(..., interval=...) expects a real number.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsQuantiles.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.quantiles(data, *, n=4, method='exclusive') expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 1, "n", "statistics.quantiles(..., n=...) expects an integer.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 2, "method", "statistics.quantiles(..., method=...) expects a string.", diagnostics, bindings);
            return emitted;
        }

        if (IsPairedDataCall(targetName))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "x", $"{targetName}(x, y) expects iterable inputs.", diagnostics, bindings);
            emitted |= AnalyzeIterableArgument(arguments, 1, "y", $"{targetName}(x, y) expects iterable inputs.", diagnostics, bindings);
            if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsLinearRegression.Name, StringComparison.Ordinal))
            {
                emitted |= AnalyzeBooleanArgument(arguments, 2, "proportional", "statistics.linear_regression(..., proportional=...) expects a bool.", diagnostics, bindings);
            }

            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsLinearRegressionResult.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRealArgument(arguments, 0, "slope", "statistics.LinearRegression(slope, intercept) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeRealArgument(arguments, 1, "intercept", "statistics.LinearRegression(slope, intercept) expects real numbers.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsNormalDist.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRealArgument(arguments, 0, "mu", "statistics.NormalDist([mu][, sigma]) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeRealArgument(arguments, 1, "sigma", "statistics.NormalDist([mu][, sigma]) expects real numbers.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsNormalDistFromSamples.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableArgument(arguments, 0, "data", "statistics.NormalDist.from_samples(data) expects an iterable.", diagnostics, bindings);
        }

        return false;
    }

    private static bool AnalyzeIterableArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeIterableOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeRealArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, StaticAbstractFacts.IsNumericLike);

    private static bool AnalyzeRealOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsNumericLike(value));

    private static bool IsSingleDataCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMean.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsGeometricMean.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMedian.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMedianLow.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMedianHigh.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMode.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsMultiMode.Name, StringComparison.Ordinal);

    private static bool IsVarianceCall(string targetName, out string centerName)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsPStdev.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.StatisticsPVariance.Name, StringComparison.Ordinal))
        {
            centerName = "mu";
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.StatisticsStdev.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.StatisticsVariance.Name, StringComparison.Ordinal))
        {
            centerName = "xbar";
            return true;
        }

        centerName = string.Empty;
        return false;
    }

    private static bool IsPairedDataCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.StatisticsCovariance.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsCorrelation.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.StatisticsLinearRegression.Name, StringComparison.Ordinal);
}
