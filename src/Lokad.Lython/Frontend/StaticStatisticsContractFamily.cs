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
        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.FMean.Name, StringComparison.Ordinal))
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

        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.HarmonicMean.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.harmonic_mean(data, *, weights=None) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeIterableOrNoneArgument(arguments, 1, "weights", "statistics.harmonic_mean(..., weights=...) expects an iterable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.MedianGrouped.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "data", "statistics.median_grouped(data[, interval]) expects iterable data.", diagnostics, bindings);
            emitted |= AnalyzeRealArgument(arguments, 1, "interval", "statistics.median_grouped(..., interval=...) expects a real number.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Quantiles.Name, StringComparison.Ordinal))
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
            if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.LinearRegression.Name, StringComparison.Ordinal))
            {
                emitted |= AnalyzeBooleanArgument(arguments, 2, "proportional", "statistics.linear_regression(..., proportional=...) expects a bool.", diagnostics, bindings);
            }

            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.NormalDistFromSamples.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableArgument(arguments, 0, "data", "statistics.NormalDist.from_samples(data) expects an iterable.", diagnostics, bindings);
        }

        return false;
    }

    private static bool IsSingleDataCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Mean.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.GeometricMean.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Median.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.MedianLow.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.MedianHigh.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Mode.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.MultiMode.Name, StringComparison.Ordinal);

    private static bool IsVarianceCall(string targetName, out string centerName)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.PStdev.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Statistics.PVariance.Name, StringComparison.Ordinal))
        {
            centerName = "mu";
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Stdev.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Variance.Name, StringComparison.Ordinal))
        {
            centerName = "xbar";
            return true;
        }

        centerName = string.Empty;
        return false;
    }

    private static bool IsPairedDataCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Covariance.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.Correlation.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.Statistics.LinearRegression.Name, StringComparison.Ordinal);
}
