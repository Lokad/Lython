using System.Security.Cryptography;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonVerifyCommand
{
    public static int Run(string[] arguments, bool fresh = false)
    {
        var names = new[] { "--catalog", "--dotnet", "--python", "--python-worker", "--out" };
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Length; i += 2)
        {
            if (i + 1 == arguments.Length || !names.Contains(arguments[i]) || !options.TryAdd(arguments[i], arguments[i + 1]))
                return Usage(fresh);
        }
        if (options.Count != names.Length) return Usage(fresh);
        using var campaign = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; campaign.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return (fresh ? ComparisonFreshCommand.RunAsync(options, campaign.Token) : RunAsync(options, campaign.Token)).GetAwaiter().GetResult(); }
        catch (Exception failure)
        {
            Console.Error.WriteLine("Comparison verification failed: " + failure.GetType().Name + ": " + failure.Message);
            return 1;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static int Usage(bool fresh)
    {
        Console.Error.WriteLine("Usage: --compare " + (fresh ? "smoke-fresh" : "verify") + " --catalog <catalog.json> --dotnet <absolute executable> " +
            "--python <absolute executable> --python-worker <cpython-worker.py> --out <receipt.json>");
        return 2;
    }

    private static async Task<int> RunAsync(Dictionary<string, string> options, CancellationToken cancellationToken)
    {
        var catalog = Path.GetFullPath(options["--catalog"]);
        var output = Path.GetFullPath(options["--out"]);
        var pythonWorker = Path.GetFullPath(options["--python-worker"]);
        var dotnet = options["--dotnet"];
        var python = options["--python"];
        if (!Path.IsPathFullyQualified(dotnet) || !Path.IsPathFullyQualified(python))
            throw new ArgumentException("Supply explicit absolute .NET and CPython executables.");
        var assembly = typeof(ComparisonVerifyCommand).Assembly.Location;
        if (new[] { catalog, pythonWorker, assembly, typeof(LythonEngine).Assembly.Location, dotnet, python }
            .Any(path => string.Equals(Path.GetFullPath(path), output,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new ArgumentException("Receipt output must differ from every input and executable.");
        var manifest = ComparisonManifest.Load(catalog);
        var launched = new
        {
            dotnet = FileIdentity(dotnet), python = FileIdentity(python), pythonWorker = FileIdentity(pythonWorker),
            adapter = FileIdentity(assembly), library = FileIdentity(typeof(LythonEngine).Assembly.Location),
        };
        var started = DateTimeOffset.UtcNow;
        JsonElement? lythonIdentity = null, pythonIdentity = null;
        var responses = new List<object>();
        var state = "Running";
        string? error = null;
        var mismatches = 0;
        void Checkpoint() => WriteAtomic(output, new
        {
            schemaVersion = 1, protocolVersion = ComparisonProtocol.Version, performanceQualified = false,
            purpose = "supervised correctness and timer smoke; no sampling qualification or ratios",
            state, started, updated = DateTimeOffset.UtcNow, catalogSha256 = manifest.Sha256,
            caseCount = manifest.Cases.Count, launched, lythonIdentity, pythonIdentity, responses, error,
            deadlinesSeconds = new { startup = 30, request = 65, shutdown = 5, @case = 300, campaign = 600 },
        });
        Checkpoint();
        try
        {
            await using var lython = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(dotnet,
                [assembly, "--compare", "worker", "--catalog", catalog], Environment.CurrentDirectory),
                "Lython", manifest.Sha256, cancellationToken).ConfigureAwait(false);
            lythonIdentity = lython.Identity;
            ValidateLythonFiles(lython.Identity, launched.adapter, launched.library);
            if (FileIdentity(dotnet).Sha256 != launched.dotnet.Sha256)
                throw new InvalidDataException("The selected .NET executable changed during startup.");
            await using var reference = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(python,
                ["-I", "-S", pythonWorker, "--catalog", catalog], Environment.CurrentDirectory),
                "CPython", manifest.Sha256, cancellationToken).ConfigureAwait(false);
            pythonIdentity = reference.Identity;
            if (reference.Identity.GetProperty("executableSha256").GetString() != launched.python.Sha256
                || reference.Identity.GetProperty("adapterSha256").GetString() != launched.pythonWorker.Sha256
                || !reference.Identity.GetProperty("gcEnabled").GetBoolean()
                || !reference.Identity.GetProperty("gilEnabled").GetBoolean()
                || reference.Identity.GetProperty("freeThreaded").GetBoolean()
                || reference.Identity.GetProperty("debug").GetBoolean()
                || !reference.Identity.GetProperty("clockMonotonic").GetBoolean())
                throw new InvalidDataException("CPython executable/adapter identity or ordinary runtime mode differs.");
            Checkpoint();
            foreach (var workload in manifest.Cases.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var caseDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                caseDeadline.CancelAfter(TimeSpan.FromMinutes(5));
                async Task<WorkerResponse> Record(string engine, string operation, string? lane, ComparisonWorkerClient client)
                {
                    WorkerResponse response;
                    try
                    {
                        response = operation == "verify" ? await client.VerifyAsync(workload, caseDeadline.Token).ConfigureAwait(false)
                            : await client.BatchAsync(workload, lane!, 2, caseDeadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && caseDeadline.IsCancellationRequested)
                    { throw new TimeoutException("Case exceeded its five-minute smoke deadline: " + workload.Id); }
                    responses.Add(new { engine, operation, lane, response });
                    return response;
                }
                var beforeLython = await Record("Lython", "verify", null, lython).ConfigureAwait(false);
                var beforePython = await Record("CPython", "verify", null, reference).ConfigureAwait(false);
                if (beforeLython.Status != "Equivalent" || beforePython.Status != "Equivalent") mismatches++;
                else
                {
                    var completed = true;
                    foreach (var lane in new[] { "warm", "compile-run", "compile" })
                    {
                        var lythonBatch = await Record("Lython", "batch", lane, lython).ConfigureAwait(false);
                        var pythonBatch = await Record("CPython", "batch", lane, reference).ConfigureAwait(false);
                        if (lythonBatch.Status != "Completed" || pythonBatch.Status != "Completed")
                        { completed = false; break; }
                    }
                    var afterLython = await Record("Lython", "verify", null, lython).ConfigureAwait(false);
                    var afterPython = await Record("CPython", "verify", null, reference).ConfigureAwait(false);
                    if (!completed || afterLython.Status != "Equivalent" || afterPython.Status != "Equivalent") mismatches++;
                }
                Checkpoint();
            }
            await lython.CloseAsync(cancellationToken).ConfigureAwait(false);
            await reference.CloseAsync(cancellationToken).ConfigureAwait(false);
            state = mismatches == 0 ? "Completed" : "Mismatch";
            Checkpoint();
            Console.WriteLine($"Supervised smoke: {manifest.Cases.Count} cases, {mismatches} failed cases; receipt {output}. No qualified timing ratios.");
            return mismatches == 0 ? 0 : 1;
        }
        catch (Exception failure)
        {
            state = cancellationToken.IsCancellationRequested ? "Interrupted" : "Failed";
            error = failure.GetType().Name + ": " + failure.Message;
            Checkpoint();
            throw;
        }
    }

    internal static void ValidateLythonFiles(JsonElement identity, BinaryIdentity adapter, BinaryIdentity library)
    {
        var rows = identity.GetProperty("libraries").EnumerateArray().ToArray();
        foreach (var expected in new[] { adapter, library })
            if (!rows.Any(row => row.GetProperty("path").GetString() == expected.Path
                && row.GetProperty("sha256").GetString() == expected.Sha256))
                throw new InvalidDataException("Lython worker loaded a different adapter or library.");
        foreach (var row in rows)
            if (FileIdentity(row.GetProperty("path").GetString()!).Sha256 != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Loaded Lython runtime file digest differs.");
    }

    internal sealed record BinaryIdentity(string Path, string Sha256);
    internal static BinaryIdentity FileIdentity(string path)
    {
        using var file = File.OpenRead(path);
        return new BinaryIdentity(Path.GetFullPath(path), Convert.ToHexStringLower(SHA256.HashData(file)));
    }

    internal static void WriteAtomic(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n", ComparisonProtocol.Utf8);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
