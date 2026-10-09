using System.Diagnostics;
using System.Text.Json;
using static Lokad.Lython.Benchmarks.Comparison.ComparisonVerifyCommand;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonQualificationCommand
{
    public static int Run(string[] arguments)
    {
        var required = new[] { "--catalog", "--dotnet", "--python", "--python-worker", "--toolchains", "--out" };
        var allowed = required.Concat(["--case", "--lane", "--resume"]).ToArray();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Length; i += 2)
            if (i + 1 == arguments.Length || !allowed.Contains(arguments[i]) || !options.TryAdd(arguments[i], arguments[i + 1])) return Usage();
        if (required.Any(n => !options.ContainsKey(n))) return Usage();
        options.TryAdd("--case", "quick"); options.TryAdd("--lane", "warm");
        if (options["--lane"] is not ("warm" or "compile-run" or "compile" or "fresh-process")
            || options.TryGetValue("--resume", out var resume) && resume != "true") return Usage();
        var started = DateTimeOffset.UtcNow;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ComparisonPolicy.CampaignDeadlineSeconds - ComparisonPolicy.CleanupReserveSeconds));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; deadline.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return RunAsync(options, started, deadline).GetAwaiter().GetResult(); }
        catch (Exception failure) { Console.Error.WriteLine("Qualification failed: " + failure.GetType().Name + ": " + failure.Message); return 1; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: --compare qualify --catalog <catalog.json> --dotnet <absolute-dotnet> --python <absolute-python> " +
            "--python-worker <cpython-worker.py> --toolchains <toolchains.json> --out <receipt.json> " +
            "[--case <quick|core-loop|all|comma-separated-ids>] [--lane <warm|compile-run|compile|fresh-process>] [--resume true]");
        return 2;
    }

    private static async Task<int> RunAsync(Dictionary<string, string> options, DateTimeOffset started, CancellationTokenSource deadline)
    {
        var cancellationToken = deadline.Token;
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("The frozen qualification profile requires Linux x86_64.");
        var supervisorOverrides = QualificationEvidence.Json(LythonComparisonWorker.RuntimeOverrides());
        QualificationEvidence.ValidateSupervisorOverrides(supervisorOverrides);
        var catalog = Path.GetFullPath(options["--catalog"]); var output = Path.GetFullPath(options["--out"]);
        var helper = Path.GetFullPath(options["--python-worker"]); var toolchainPath = Path.GetFullPath(options["--toolchains"]);
        var dotnet = options["--dotnet"]; var python = options["--python"];
        if (!Path.IsPathFullyQualified(dotnet) || !Path.IsPathFullyQualified(python)) throw new ArgumentException("Supply absolute executables.");
        var assembly = typeof(ComparisonQualificationCommand).Assembly.Location;
        var adapterDirectory = Path.GetDirectoryName(assembly)!;
        var root = await QualificationEvidence.CaptureAsync("/usr/bin/git", ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (Path.GetFullPath(root) != Environment.CurrentDirectory) throw new ArgumentException("Run qualification from the clean repository root.");
        var manifest = ComparisonManifest.Load(catalog);
        var requested = options["--case"] switch
        {
            "all" => manifest.Cases.Keys.ToArray(),
            "quick" => ComparisonPolicy.QuickCaseIds,
            "core-loop" => ComparisonPolicy.CoreLoopCaseIds,
            _ => options["--case"].Split(','),
        };
        if (requested.Any(id => !manifest.Cases.ContainsKey(id))) throw new ArgumentException("An explicitly selected case is absent from the catalog.");
        var ids = new[] { "control.empty.control", "control.tiny.control" }.Concat(requested).Distinct(StringComparer.Ordinal).ToArray();
        var cases = ids.Select(id => manifest.Cases.TryGetValue(id, out var value) ? value : throw new ArgumentException("Both controls must be present in the catalog.")).ToArray();
        var canonical = WorkloadCatalog.Create().ToDictionary(c => c.Id, StringComparer.Ordinal);
        if (cases.Any(c => !canonical.TryGetValue(c.Id, out var expected) || expected != c)) throw new InvalidDataException("Catalog differs from the frozen source/golden catalog.");
        var toolchains = ComparisonReportCommand.ReadJson(toolchainPath).Deserialize<JsonElement>();
        if (FileIdentity(dotnet) != new BinaryIdentity(toolchains.GetProperty("dotnet").GetProperty("executable").GetString()!,
            toolchains.GetProperty("dotnet").GetProperty("sha256").GetString()!))
            throw new InvalidDataException("Selected .NET executable differs from the toolchain receipt.");
        var filePaths = new[] { catalog, dotnet, python, helper, toolchainPath, typeof(object).Assembly.Location }
            .Concat(Directory.GetFiles(adapterDirectory, "*.dll"))
            .Concat(Directory.GetFiles(adapterDirectory, "*.deps.json"))
            .Concat(Directory.GetFiles(adapterDirectory, "*.runtimeconfig.json"))
            .Concat(new[] { "global.json", "Directory.Build.props", "Directory.Packages.props",
                "src/Lokad.Lython/packages.lock.json", "benchmarks/Lokad.Lython.Benchmarks/packages.lock.json" }.Select(p => Path.Combine(root, p)))
            .Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (filePaths.Contains(output, StringComparer.Ordinal)) throw new ArgumentException("Output aliases an input or binary.");
        var files = filePaths.Select(FileIdentity).ToList();
        var before = await ReadSourceAsync().ConfigureAwait(false);
        if (before.Status.Length != 0 || before.Sdk != "10.0.401") throw new InvalidDataException("Qualification requires a clean checkout and the pinned effective SDK.");
        var machine = QualificationEvidence.Json(QuietMachineProbe.Inventory());
        var receipt = new QualificationReceipt
        {
            SchemaVersion = 1, ProtocolVersion = ComparisonProtocol.Version, PolicyVersion = ComparisonPolicy.Version,
            EligibilityVersion = ComparisonPolicy.EligibilityVersion, Policy = QualificationEvidence.Json(ComparisonPolicy.Describe()),
            PolicySha256 = QualificationEvidence.PolicyHash, Id = Guid.NewGuid().ToString("N"), Started = started,
            Lane = options["--lane"], CatalogSha256 = manifest.Sha256, Before = before, Machine = machine, Toolchains = toolchains,
            RuntimeConfig = ComparisonReportCommand.ReadJson(Path.ChangeExtension(assembly, ".runtimeconfig.json")),
            SupervisorOverrides = supervisorOverrides,
            Files = files, Cases = cases.Select(w => new QualificationCase { Workload = w }).ToList(),
            RequestedCaseIds = ids, CatalogCaseCount = manifest.Cases.Count,
        };
        QualificationEvidence.ValidateRuntimeConfig(receipt.RuntimeConfig);
        if (options.ContainsKey("--resume"))
        {
            var previous = ComparisonReportCommand.ReadReceipt(output);
            RestoreForResume(previous, receipt);
            deadline.CancelAfter(RemainingCollectionTime(receipt.Started, DateTimeOffset.UtcNow));
            // Preserve the complete previous attempt beside the receipt. Reuse
            // finished measurements and exclusions; partial sessions restart.
            var archive = output + ".attempt-" + previous.Updated.UtcTicks + ".json";
            if (File.Exists(archive)) throw new IOException("Previous-attempt archive already exists.");
            WriteAtomic(archive, previous);
        }
        else if (File.Exists(output)) throw new IOException("Receipt already exists; choose a new path or explicitly resume.");
        void Checkpoint() { receipt.Updated = DateTimeOffset.UtcNow; WriteAtomic(output, receipt); }
        Checkpoint();
        try
        {
            receipt.InitialGate = await QuietMachineProbe.CheckAsync(cancellationToken).ConfigureAwait(false); Checkpoint();
            if (!receipt.InitialGate.Quiet) throw new PairedSampler.QuietGateException(receipt.InitialGate);
            foreach (var row in PendingCases(receipt))
            {
                using var caseDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                caseDeadline.CancelAfter(TimeSpan.FromSeconds(ComparisonPolicy.CaseDeadlineSeconds));
                row.State = "Running"; Checkpoint();
                string? payloadPath = null; ComparisonManifest? payload = null;
                if (receipt.Lane == "fresh-process")
                {
                    payloadPath = Path.Combine(output + ".inputs-" + receipt.Id, row.Workload.Id + ".json");
                    WriteAtomic(payloadPath, new { schemaVersion = 1, catalogVersion = WorkloadCatalog.Version, cases = new[] { row.Workload } });
                    payload = ComparisonManifest.Load(payloadPath);
                    row.Payload = FileIdentity(payloadPath);
                }
                try
                {
                    for (var index = 0; index < ComparisonPolicy.Sessions; index++)
                    {
                        var session = new QualificationSession { Sampling = new SamplingSession { Index = index, Lane = receipt.Lane } };
                        row.Sessions.Add(session); Checkpoint();
                        ComparisonWorkerClient? left = null, right = null;
                        try
                        {
                            await OpenAsync().ConfigureAwait(false);
                            session.LythonIdentity = left!.Identity; session.PythonIdentity = right!.Identity;
                            await VerifyAsync("before", caseDeadline.Token).ConfigureAwait(false);
                            if (receipt.Lane == "fresh-process") await CloseAsync().ConfigureAwait(false);
                            var nextRequest = 0;
                            await PairedSampler.RunAsync(session.Sampling, BatchAsync, new LinuxSamplingMachine(), Checkpoint,
                                caseDeadline.Token, ct => VerifyAsync("after-warmup", ct)).ConfigureAwait(false);
                            if (session.Sampling.State == "Measured") await VerifyAsync("after", caseDeadline.Token).ConfigureAwait(false);
                            await CloseAsync().ConfigureAwait(false); session.Closed = true;
                            CheckFiles(); Checkpoint();

                            async Task<WorkerResponse> BatchAsync(bool lython, int count, CancellationToken ct)
                            {
                                if (receipt.Lane != "fresh-process") return await (lython ? left! : right!).BatchAsync(row.Workload, receipt.Lane, count, ct).ConfigureAwait(false);
                                var request = ++nextRequest; long ticks = 0;
                                using var batchDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                batchDeadline.CancelAfter(ComparisonWorkerClient.RequestDeadline);
                                for (var job = 0; job < count; job++)
                                {
                                    if (FileIdentity(payloadPath!).Sha256 != payload!.Sha256) throw new InvalidDataException("Prepared one-case payload changed.");
                                    var observation = await FreshProcessRunner.RunAsync(lython
                                        ? new(dotnet, [assembly, "--compare", "once", "--catalog", payloadPath!], root, RemoveSupervisorTieringOverride: true)
                                        : new(python, ["-I", "-S", helper, "--once", "--catalog", payloadPath!], root, RemoveSupervisorTieringOverride: true), payload!,
                                        lython ? session.LythonIdentity : session.PythonIdentity, batchDeadline.Token).ConfigureAwait(false);
                                    session.Fresh.Add(new(lython, request, observation));
                                    if (observation.Status != "Equivalent") return observation.Response with { RequestId = request, ElapsedTicks = null };
                                    ticks = checked(ticks + observation.ElapsedTicks!.Value);
                                }
                                return new(ComparisonProtocol.Version, request, row.Workload.Id, "Completed", count, ticks, Stopwatch.Frequency,
                                    row.Workload.SourceSha256, row.Workload.FixtureSha256, row.Workload.ExpectedOutputSha256, row.Workload.ExpectedOutputSha256, null);
                            }
                        }
                        finally
                        {
                            if (left is not null) await left.DisposeAsync().ConfigureAwait(false);
                            if (right is not null) await right.DisposeAsync().ConfigureAwait(false);
                        }

                        async Task OpenAsync()
                        {
                            left = await ComparisonWorkerClient.StartAsync(new(dotnet, [assembly, "--compare", "worker", "--catalog", catalog], root, RemoveSupervisorTieringOverride: true), "Lython", manifest.Sha256, caseDeadline.Token).ConfigureAwait(false);
                            right = await ComparisonWorkerClient.StartAsync(new(python, ["-I", "-S", helper, "--catalog", catalog], root, RemoveSupervisorTieringOverride: true), "CPython", manifest.Sha256, caseDeadline.Token).ConfigureAwait(false);
                            QualificationEvidence.ValidateProfile(left.Identity, right.Identity, before, toolchains);
                            ValidateLythonFiles(left.Identity, FileIdentity(assembly), FileIdentity(typeof(LythonEngine).Assembly.Location));
                            CheckFiles();
                        }
                        async Task VerifyAsync(string phase, CancellationToken ct)
                        {
                            if (left is null) await OpenAsync().ConfigureAwait(false);
                            var l = await left!.VerifyAsync(row.Workload, ct).ConfigureAwait(false);
                            var r = await right!.VerifyAsync(row.Workload, ct).ConfigureAwait(false);
                            session.Verification.Add(new(phase, l, r)); Checkpoint();
                            if (l.Status != "Equivalent" || r.Status != "Equivalent") throw new InvalidDataException("Semantic verification failed: " + row.Workload.Id + " / " + phase);
                            if (receipt.Lane == "fresh-process") await CloseAsync().ConfigureAwait(false);
                        }
                        async Task CloseAsync()
                        {
                            if (left is not null) { await left.CloseAsync(caseDeadline.Token).ConfigureAwait(false); await left.DisposeAsync().ConfigureAwait(false); left = null; }
                            if (right is not null) { await right.CloseAsync(caseDeadline.Token).ConfigureAwait(false); await right.DisposeAsync().ConfigureAwait(false); right = null; }
                        }
                    }
                }
                catch (Exception noise) when (noise is PairedSampler.QuietGateException or PairedSampler.NoiseGateException)
                {
                    row.State = "Unqualified";
                    row.Reason = noise.Message;
                    Checkpoint();
                    Console.WriteLine($"Excluded {row.Workload.Id}: {noise.Message}; continuing without retrying this case.");
                    continue;
                }
                row.State = row.Sessions.All(s => s.Sampling.State == "Measured") ? "Measured" : "Unqualified";
                row.Reason = row.State == "Unqualified" ? string.Join(" | ", row.Sessions.Select(s => s.Sampling.Reason).Where(r => r is not null)) : null;
                Checkpoint();
                Console.WriteLine($"Collected {row.Workload.Id}: {row.State}, {row.Sessions.Count} sessions; qualification is recomputed after final checks.");
            }
            CheckFiles(); receipt.After = await ReadSourceAsync().ConfigureAwait(false);
            receipt.FilesAfter = filePaths.Select(FileIdentity).ToList(); receipt.MachineAfter = QualificationEvidence.Json(QuietMachineProbe.Inventory());
            receipt.FinalGate = await QuietMachineProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (!receipt.FinalGate.Quiet) throw new PairedSampler.QuietGateException(receipt.FinalGate);
            receipt.State = "Completed";
            if (!QualificationEvidence.CompleteCampaign(receipt)) throw new InvalidDataException("Final source/environment/policy evidence differs.");
            receipt.PerformanceQualified = receipt.Cases.Any(row => QualificationEvidence.Assess(receipt, row).Status == "Qualified");
            Checkpoint(); Console.WriteLine("Completed collection: " + output + "; use render-report for all exclusions and eligible session ratios.");
            return 0;
        }
        catch (Exception failure)
        {
            receipt.State = failure is PairedSampler.QuietGateException or PairedSampler.NoiseGateException ? "Busy"
                : cancellationToken.IsCancellationRequested ? "Interrupted" : "Failed";
            receipt.Reason = failure.GetType().Name + ": " + failure.Message; receipt.PerformanceQualified = false; Checkpoint();
            if (receipt.State == "Busy") { Console.Error.WriteLine("Machine noise stopped collection; checkpoint: " + output); return 3; }
            throw;
        }

        void CheckFiles()
        {
            foreach (var file in files) if (FileIdentity(file.Path).Sha256 != file.Sha256) throw new InvalidDataException("Frozen input/build changed: " + file.Path);
        }
        async Task<SourceEvidence> ReadSourceAsync() => new(
            await QualificationEvidence.CaptureAsync("/usr/bin/git", ["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false),
            await QualificationEvidence.CaptureAsync("/usr/bin/git", ["status", "--porcelain", "--untracked-files=normal"], cancellationToken).ConfigureAwait(false),
            await QualificationEvidence.CaptureAsync(dotnet, ["--version"], cancellationToken).ConfigureAwait(false));
    }

    internal static void RestoreForResume(QualificationReceipt previous, QualificationReceipt receipt)
    {
        if (previous.State is not ("Running" or "Busy" or "Failed" or "Interrupted"))
            throw new InvalidDataException("Only an unfinished campaign can be resumed.");
        if (previous.SchemaVersion != receipt.SchemaVersion || previous.ProtocolVersion != receipt.ProtocolVersion
            || previous.PolicyVersion != receipt.PolicyVersion || previous.EligibilityVersion != receipt.EligibilityVersion
            || previous.PolicySha256 != receipt.PolicySha256 || QualificationEvidence.CanonicalJson(previous.Policy) != QualificationEvidence.CanonicalJson(receipt.Policy)
            || previous.CatalogSha256 != receipt.CatalogSha256 || previous.CatalogCaseCount != receipt.CatalogCaseCount
            || !previous.RequestedCaseIds.SequenceEqual(receipt.RequestedCaseIds) || previous.Before != receipt.Before || previous.Lane != receipt.Lane
            || !JsonElement.DeepEquals(previous.RuntimeConfig, receipt.RuntimeConfig)
            || !JsonElement.DeepEquals(previous.SupervisorOverrides, receipt.SupervisorOverrides)
            || QualificationEvidence.CanonicalJson(previous.Toolchains) != QualificationEvidence.CanonicalJson(receipt.Toolchains)
            || !previous.Files.SequenceEqual(receipt.Files)
            || QualificationEvidence.MachineFingerprint(previous.Machine) != QualificationEvidence.MachineFingerprint(receipt.Machine)
            || !previous.Cases.Select(c => c.Workload).SequenceEqual(receipt.Cases.Select(c => c.Workload)))
            throw new InvalidDataException("Resume requires identical versions, source, policy, case order, machine, toolchains and every file digest.");
        _ = RemainingCollectionTime(previous.Started, DateTimeOffset.UtcNow);
        receipt.Started = previous.Started;
        for (var i = 0; i < receipt.Cases.Count; i++)
            if (Finished(previous.Cases[i])) receipt.Cases[i] = previous.Cases[i];
    }

    internal static IEnumerable<QualificationCase> PendingCases(QualificationReceipt receipt)
        => receipt.Cases.Where(row => !Finished(row));

    private static bool Finished(QualificationCase row) => row.State is "Measured" or "Unqualified";

    internal static TimeSpan RemainingCollectionTime(DateTimeOffset started, DateTimeOffset now)
    {
        if (started == default || started > now) throw new InvalidDataException("Invalid campaign start time.");
        var remaining = started.AddSeconds(ComparisonPolicy.CampaignDeadlineSeconds - ComparisonPolicy.CleanupReserveSeconds) - now;
        if (remaining <= TimeSpan.Zero) throw new InvalidDataException("The original ten-minute lane budget is exhausted; resumption cannot reset it.");
        return remaining;
    }
}
