using System.Diagnostics;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class LythonOnceWorker
{
    public static async Task RunAsync(ComparisonManifest manifest, Stream output, CancellationToken cancellationToken = default,
        ILythonHost? host = null)
    {
        if (manifest.Cases.Count != 1) throw new InvalidDataException("A fresh-process payload must contain exactly one case.");
        var workload = manifest.Cases.Values.Single();
        var status = "Unsupported";
        string? actual = null, reason = null;
        var completed = 0;
        var script = new LythonEngine().Compile(workload.Source);
        if (!script.IsValid) reason = string.Join(" | ", script.Diagnostics.Select(d => d.Message));
        else
        {
            // Exactly one public invocation, with ordinary limits and a fresh
            // context. No warmup or verification run precedes it in this process.
            var result = script.Run(host ?? new ComparisonHost());
            actual = result.StandardOutput;
            if (!result.Success)
            {
                status = result.DeniedReservationBytes > 0 || result.Failure?.ExceptionType == "MemoryError" ? "BudgetDenied" : "Failure";
                reason = result.Failure?.Message ?? "Lython execution failed.";
            }
            else if (result.ExitCode is not null || result.ReturnValue is not null || result.StandardError.Length != 0
                || !string.Equals(actual, workload.ExpectedOutput, StringComparison.Ordinal))
            {
                status = "Mismatch";
                reason = "Complete public result differs from the independent golden.";
            }
            else { status = "Equivalent"; completed = 1; }
        }
        await ComparisonProtocol.WriteAsync(output, new
        {
            identity = LythonComparisonWorker.CreateIdentity(manifest, includeFileDigests: false),
            response = new WorkerResponse(ComparisonProtocol.Version, 1, workload.Id, status, completed, null,
                Stopwatch.Frequency, workload.SourceSha256, workload.FixtureSha256, workload.ExpectedOutputSha256,
                actual is null ? null : ComparisonProtocol.Digest(actual), reason),
        }, cancellationToken).ConfigureAwait(false);
    }
}
