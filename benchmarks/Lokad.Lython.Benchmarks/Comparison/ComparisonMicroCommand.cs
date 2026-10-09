using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

// Short diagnostic feedback, deliberately separate from milestone qualification.
internal static class ComparisonMicroCommand
{
    internal const int CollectionSeconds = 15;
    internal const int Rounds = 7;
    internal const double WarmupSeconds = 1;
    internal const double BatchSeconds = .025;
    internal sealed record Attempt(string Engine, string Phase, int RequestedIterations, WorkerResponse Response);
    internal sealed record Summary(string Engine, double MedianMicroseconds, double IqrFraction);

    public static int Run(string[] arguments)
    {
        var required = new[] { "--catalog", "--dotnet", "--baseline-worker", "--python", "--python-worker", "--out" };
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Length; i += 2)
            if (i + 1 == arguments.Length || (!required.Contains(arguments[i]) && arguments[i] != "--case")
                || !options.TryAdd(arguments[i], arguments[i + 1])) return Usage();
        if (required.Any(name => !options.ContainsKey(name))) return Usage();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CollectionSeconds));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; deadline.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return RunAsync(options, deadline.Token).GetAwaiter().GetResult(); }
        catch (Exception failure)
        {
            Console.Error.WriteLine("Microbenchmark failed: " + failure.GetType().Name + ": " + failure.Message);
            return 1;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: --compare micro --catalog <catalog.json> --dotnet <absolute executable> " +
            "--baseline-worker <old benchmark.dll> --python <absolute executable> --python-worker <cpython-worker.py> " +
            "--out <new receipt.json> [--case <id; default loops.integer.large>]. Diagnostic only; run in a 30-second owned service.");
        return 2;
    }

    private static async Task<int> RunAsync(Dictionary<string, string> options, CancellationToken token)
    {
        var catalog = Path.GetFullPath(options["--catalog"]);
        var output = Path.GetFullPath(options["--out"]);
        var oldAdapter = Path.GetFullPath(options["--baseline-worker"]);
        var newAdapter = typeof(ComparisonMicroCommand).Assembly.Location;
        var oldLibrary = Path.Combine(Path.GetDirectoryName(oldAdapter)!, "Lokad.Lython.dll");
        var newLibrary = typeof(LythonEngine).Assembly.Location;
        var pythonWorker = Path.GetFullPath(options["--python-worker"]);
        var dotnet = options["--dotnet"];
        var python = options["--python"];
        if (!Path.IsPathFullyQualified(dotnet) || !Path.IsPathFullyQualified(python))
            throw new ArgumentException("Use explicit absolute executable paths.");
        if (File.Exists(output)) throw new IOException("Preserve existing microbenchmark receipts; choose a new output.");
        var inputPaths = new[] { catalog, oldAdapter, newAdapter, oldLibrary, newLibrary, dotnet, python, pythonWorker,
            Path.ChangeExtension(oldAdapter, ".deps.json"), Path.ChangeExtension(oldAdapter, ".runtimeconfig.json"),
            Path.ChangeExtension(newAdapter, ".deps.json"), Path.ChangeExtension(newAdapter, ".runtimeconfig.json") };
        var files = inputPaths.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(ComparisonVerifyCommand.FileIdentity).ToArray();
        if (files.Any(file => string.Equals(file.Path, output,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new ArgumentException("Receipt must differ from every input.");
        var manifest = ComparisonManifest.Load(catalog);
        var id = options.GetValueOrDefault("--case", "loops.integer.large");
        if (!manifest.Cases.TryGetValue(id, out var workload)) throw new ArgumentException("Case is absent from the manifest: " + id);
        var attempts = new List<Attempt>();
        var identities = new Dictionary<string, JsonElement>();
        var started = DateTimeOffset.UtcNow;
        var state = "Running";
        string? error = null;
        Summary[] summaries = [];
        void Save() => ComparisonVerifyCommand.WriteAtomic(output, new
        {
            schemaVersion = 1, protocolVersion = ComparisonProtocol.Version, performanceQualified = false,
            purpose = "short old/new Lython microbenchmark with CPython context; no noise or milestone qualification",
            state, started, updated = DateTimeOffset.UtcNow, collectionSeconds = CollectionSeconds,
            requiredExternalHardDeadlineSeconds = 30, rounds = Rounds, warmupSeconds = WarmupSeconds, batchSeconds = BatchSeconds,
            catalogSha256 = manifest.Sha256, workload, files, identities, attempts, summaries, error,
        });
        Save();
        try
        {
            var removeOverride = OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") == "0";
            await using var baseline = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(dotnet,
                [oldAdapter, "--compare", "worker", "--catalog", catalog], Environment.CurrentDirectory, removeOverride),
                "Lython", manifest.Sha256, token).ConfigureAwait(false);
            identities.Add("baseline", baseline.Identity);
            ComparisonVerifyCommand.ValidateLythonFiles(baseline.Identity,
                ComparisonVerifyCommand.FileIdentity(oldAdapter), ComparisonVerifyCommand.FileIdentity(oldLibrary));
            await using var candidate = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(dotnet,
                [newAdapter, "--compare", "worker", "--catalog", catalog], Environment.CurrentDirectory, removeOverride),
                "Lython", manifest.Sha256, token).ConfigureAwait(false);
            identities.Add("candidate", candidate.Identity);
            ComparisonVerifyCommand.ValidateLythonFiles(candidate.Identity,
                ComparisonVerifyCommand.FileIdentity(newAdapter), ComparisonVerifyCommand.FileIdentity(newLibrary));
            await using var reference = await ComparisonWorkerClient.StartAsync(new WorkerLaunch(python,
                ["-I", "-S", pythonWorker, "--catalog", catalog], Environment.CurrentDirectory, removeOverride),
                "CPython", manifest.Sha256, token).ConfigureAwait(false);
            identities.Add("CPython", reference.Identity);
            if (reference.Identity.GetProperty("executableSha256").GetString() != ComparisonVerifyCommand.FileIdentity(python).Sha256
                || reference.Identity.GetProperty("adapterSha256").GetString() != ComparisonVerifyCommand.FileIdentity(pythonWorker).Sha256
                || !reference.Identity.GetProperty("gcEnabled").GetBoolean()
                || !reference.Identity.GetProperty("gilEnabled").GetBoolean()
                || reference.Identity.GetProperty("freeThreaded").GetBoolean()
                || reference.Identity.GetProperty("debug").GetBoolean())
                throw new InvalidDataException("CPython input identity differs.");
            var clients = new[] { baseline, candidate, reference };
            await SampleAsync(workload, async (engine, operation, count, ct) => operation == "verify"
                ? await clients[engine].VerifyAsync(workload, ct).ConfigureAwait(false)
                : await clients[engine].BatchAsync(workload, "warm", count, ct).ConfigureAwait(false), attempts, Save, token).ConfigureAwait(false);
            foreach (var client in clients) await client.CloseAsync(token).ConfigureAwait(false);
            if (!files.SequenceEqual(files.Select(file => ComparisonVerifyCommand.FileIdentity(file.Path))))
                throw new InvalidDataException("An input changed during the microbenchmark.");
            summaries = Summarize(attempts);
            state = "Completed";
            Save();
            foreach (var row in summaries)
                Console.WriteLine($"{row.Engine}: {row.MedianMicroseconds:F3} us/job, IQR/median {row.IqrFraction:P1}");
            Console.WriteLine("Diagnostic microbenchmark completed; no qualified multiplier or confidence interval. Receipt: " + output);
            return 0;
        }
        catch (Exception failure)
        {
            state = token.IsCancellationRequested ? "Interrupted" : "Failed";
            error = failure.GetType().Name + ": " + failure.Message;
            Save();
            throw;
        }
    }

    internal static readonly string[] Engines = ["baseline", "candidate", "CPython"];

    internal static async Task SampleAsync(ComparisonWorkload workload,
        Func<int, string, int, CancellationToken, Task<WorkerResponse>> request,
        List<Attempt> attempts, Action save, CancellationToken token)
    {
        async Task<double> Record(int engine, string phase, int count)
        {
            token.ThrowIfCancellationRequested();
            var response = await request(engine, phase == "verify" ? "verify" : "batch", count, token).ConfigureAwait(false);
            attempts.Add(new(Engines[engine], phase, count, response));
            save();
            if (response.Status != (phase == "verify" ? "Equivalent" : "Completed")
                || response.CompletedInvocations != count || response.ActualOutputSha256 != workload.ExpectedOutputSha256
                || response.SourceSha256 != workload.SourceSha256 || response.FixtureSha256 != workload.FixtureSha256
                || response.ExpectedOutputSha256 != workload.ExpectedOutputSha256 || response.ClockFrequency <= 0
                || (phase == "verify" ? response.ElapsedTicks is not null : response.ElapsedTicks is null or <= 0))
                throw new InvalidDataException("Unsuccessful, incomplete or mismatched microbenchmark response.");
            return response.ElapsedTicks is { } ticks ? (double)ticks / response.ClockFrequency : 0;
        }
        var counts = new int[3];
        for (var engine = 0; engine < 3; engine++)
        {
            await Record(engine, "verify", 2).ConfigureAwait(false);
            var count = 32;
            var elapsed = 0.0;
            for (var batch = 0; elapsed < WarmupSeconds; batch++)
            {
                if (batch >= 8) throw new InvalidDataException("Microbenchmark warmup count ceiling reached.");
                var seconds = await Record(engine, "warmup", count).ConfigureAwait(false);
                elapsed += seconds;
                if (elapsed < WarmupSeconds) count = PairedSampler.NextCount(count, seconds, WarmupSeconds - elapsed, true);
            }
            counts[engine] = 1;
            for (var batch = 0; ; batch++)
            {
                if (batch >= 8) throw new InvalidDataException("Microbenchmark calibration ceiling reached.");
                var seconds = await Record(engine, "calibrate", counts[engine]).ConfigureAwait(false);
                if (seconds >= BatchSeconds) break;
                counts[engine] = PairedSampler.NextCount(counts[engine], seconds, BatchSeconds);
            }
        }
        // Let ordinary background compilation settle without changing worker defaults.
        await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        for (var round = 0; round < Rounds; round++)
            for (var offset = 0; offset < 3; offset++)
            {
                var engine = round % 2 == 0 ? (round + offset) % 3 : (round + 3 - offset) % 3;
                await Record(engine, "measure", counts[engine]).ConfigureAwait(false);
            }
        for (var engine = 0; engine < 3; engine++) await Record(engine, "verify", 2).ConfigureAwait(false);
    }

    internal static Summary[] Summarize(IReadOnlyList<Attempt> attempts)
        => Engines.Select(engine =>
        {
            var rows = attempts.Where(a => a.Engine == engine && a.Phase == "measure").ToArray();
            if (rows.Length != Rounds || rows.Any(a => a.Response.Status != "Completed"
                || a.RequestedIterations <= 0
                || a.Response.CompletedInvocations != a.RequestedIterations || a.Response.ElapsedTicks is null or <= 0
                || a.Response.ClockFrequency <= 0)) throw new InvalidDataException("Incomplete microbenchmark series.");
            var seconds = rows.Select(a => (double)a.Response.ElapsedTicks!.Value / a.Response.ClockFrequency / a.RequestedIterations)
                .Order().ToArray();
            var median = seconds[3];
            return new Summary(engine, median * 1_000_000, (seconds[4] + seconds[5] - seconds[1] - seconds[2]) / (2 * median));
        }).ToArray();
}
