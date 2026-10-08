using System.Text.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Lokad.Lython.Benchmarks.Comparison.ComparisonVerifyCommand;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonFreshCommand
{
    internal static async Task<int> RunAsync(Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        var catalog = Path.GetFullPath(options["--catalog"]);
        var output = Path.GetFullPath(options["--out"]);
        var pythonWorker = Path.GetFullPath(options["--python-worker"]);
        var dotnet = options["--dotnet"];
        var python = options["--python"];
        if (!Path.IsPathFullyQualified(dotnet) || !Path.IsPathFullyQualified(python))
            throw new ArgumentException("Supply explicit absolute .NET and CPython executables.");
        var assembly = typeof(ComparisonFreshCommand).Assembly.Location;
        var library = typeof(LythonEngine).Assembly.Location;
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (new[] { catalog, pythonWorker, assembly, library, dotnet, python }.Any(path =>
            string.Equals(Path.GetFullPath(path), output, pathComparison)))
            throw new ArgumentException("Receipt output must differ from every input and executable.");
        var manifest = ComparisonManifest.Load(catalog);
        var launched = new
        {
            dotnet = FileIdentity(dotnet), python = FileIdentity(python), pythonWorker = FileIdentity(pythonWorker),
            adapter = FileIdentity(assembly), library = FileIdentity(library),
        };
        var payloadDirectory = output + ".inputs-" + Guid.NewGuid().ToString("N");
        var payloads = new List<(string Path, ComparisonManifest Manifest)>();
        // Freeze identical one-case bytes before starting any parent timer.
        foreach (var workload in manifest.Cases.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(payloadDirectory, payloads.Count.ToString("D4") + ".json");
            WriteAtomic(path, new { schemaVersion = 1, catalogVersion = WorkloadCatalog.Version, cases = new[] { workload } });
            payloads.Add((path, ComparisonManifest.Load(path)));
        }
        var started = DateTimeOffset.UtcNow;
        JsonElement? lythonIdentity = null, pythonIdentity = null;
        var validation = new List<object>();
        var rows = new List<object>();
        var state = "Running";
        string? error = null;
        var failures = 0;
        void Checkpoint() => WriteAtomic(output, new
        {
            schemaVersion = 1, protocolVersion = ComparisonProtocol.Version, performanceQualified = false,
            purpose = "fresh-process correctness/timer smoke; one job, OS caches retained, no qualified ratios",
            timerBoundary = "parent before owned launch through complete stdout/stderr drain and root exit; post-drain decoding/validation excluded",
            parentClock = new { frequency = Stopwatch.Frequency, highResolution = Stopwatch.IsHighResolution,
                runtimeVersion = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString() },
            state, started, updated = DateTimeOffset.UtcNow, catalogSha256 = manifest.Sha256,
            caseCount = payloads.Count, launched, lythonIdentity, pythonIdentity, payloadDirectory, validation, rows, error,
            deadlinesSeconds = new { freshProcess = 65, @case = 300, campaign = 600 },
        });
        Checkpoint();
        try
        {
            // Full provenance and two-run semantic admission are outside fresh
            // samples. Shut both persistent workers down before launching once.
            await using (var lython = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(dotnet,
                [assembly, "--compare", "worker", "--catalog", catalog], Environment.CurrentDirectory),
                "Lython", manifest.Sha256, cancellationToken).ConfigureAwait(false))
            await using (var reference = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(python,
                ["-I", "-S", pythonWorker, "--catalog", catalog], Environment.CurrentDirectory),
                "CPython", manifest.Sha256, cancellationToken).ConfigureAwait(false))
            {
                lythonIdentity = lython.Identity;
                pythonIdentity = reference.Identity;
                CheckPreparedFiles();
                if (reference.Identity.GetProperty("executableSha256").GetString() != launched.python.Sha256
                    || reference.Identity.GetProperty("adapterSha256").GetString() != launched.pythonWorker.Sha256
                    || !reference.Identity.GetProperty("gcEnabled").GetBoolean()
                    || !reference.Identity.GetProperty("gilEnabled").GetBoolean()
                    || reference.Identity.GetProperty("freeThreaded").GetBoolean()
                    || reference.Identity.GetProperty("debug").GetBoolean())
                    throw new InvalidDataException("CPython prepared binary, helper or normal mode differs.");
                foreach (var workload in manifest.Cases.Values)
                {
                    var left = await lython.VerifyAsync(workload, cancellationToken).ConfigureAwait(false);
                    var right = await reference.VerifyAsync(workload, cancellationToken).ConfigureAwait(false);
                    validation.Add(new { caseId = workload.Id, lython = left, python = right });
                    if (left.Status != "Equivalent" || right.Status != "Equivalent") failures++;
                    Checkpoint();
                }
                await lython.CloseAsync(cancellationToken).ConfigureAwait(false);
                await reference.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            if (failures == 0)
            {
                foreach (var payload in payloads)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var caseDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    caseDeadline.CancelAfter(TimeSpan.FromMinutes(5));
                    var left = await FreshProcessRunner.RunAsync(new WorkerLaunch(dotnet,
                        [assembly, "--compare", "once", "--catalog", payload.Path], Environment.CurrentDirectory),
                        payload.Manifest, lythonIdentity!.Value, caseDeadline.Token).ConfigureAwait(false);
                    rows.Add(new { engine = "Lython", payload.Path, payloadSha256 = payload.Manifest.Sha256, observation = left });
                    Checkpoint();
                    var right = await FreshProcessRunner.RunAsync(new WorkerLaunch(python,
                        ["-I", "-S", pythonWorker, "--once", "--catalog", payload.Path], Environment.CurrentDirectory),
                        payload.Manifest, pythonIdentity!.Value, caseDeadline.Token).ConfigureAwait(false);
                    rows.Add(new { engine = "CPython", payload.Path, payloadSha256 = payload.Manifest.Sha256, observation = right });
                    if (left.Status != "Equivalent" || right.Status != "Equivalent") failures++;
                    Checkpoint();
                }
            }
            CheckPreparedFiles();
            state = failures == 0 ? "Completed" : "Mismatch";
            Checkpoint();
            Console.WriteLine($"Fresh-process smoke: {payloads.Count} cases, {failures} failed cases; receipt {output}. No qualified timing ratios.");
            return failures == 0 ? 0 : 1;
        }
        catch (Exception failure)
        {
            state = cancellationToken.IsCancellationRequested ? "Interrupted" : "Failed";
            error = failure.GetType().Name + ": " + failure.Message;
            Checkpoint();
            throw;
        }

        void CheckPreparedFiles()
        {
            foreach (var file in new[] { launched.dotnet, launched.python, launched.pythonWorker, launched.adapter, launched.library })
                if (FileIdentity(file.Path).Sha256 != file.Sha256)
                    throw new InvalidDataException("Prepared executable/helper/library changed: " + file.Path);
            if (lythonIdentity is { } identity) ValidateLythonFiles(identity, launched.adapter, launched.library);
        }
    }
}
