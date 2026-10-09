using System.Diagnostics;
using System.Text.Json;
using static Lokad.Lython.Benchmarks.Comparison.ComparisonVerifyCommand;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record SourceEvidence(string Revision, string Status, string Sdk);
internal sealed record VerificationEvidence(string Phase, WorkerResponse Lython, WorkerResponse Python);
internal sealed record FreshEvidence(bool Lython, int BatchRequestId, FreshProcessObservation Observation);
internal sealed class QualificationSession
{
    public SamplingSession Sampling { get; set; } = new();
    public JsonElement LythonIdentity { get; set; } = JsonSerializer.SerializeToElement<object?>(null);
    public JsonElement PythonIdentity { get; set; } = JsonSerializer.SerializeToElement<object?>(null);
    public List<VerificationEvidence> Verification { get; set; } = [];
    public List<FreshEvidence> Fresh { get; set; } = [];
    public bool Closed { get; set; }
}
internal sealed class QualificationCase
{
    public ComparisonWorkload Workload { get; set; } = null!;
    public List<QualificationSession> Sessions { get; set; } = [];
    public string State { get; set; } = "Pending";
    public string? Reason { get; set; }
    public BinaryIdentity? Payload { get; set; }
}
internal sealed class QualificationReceipt
{
    public int SchemaVersion { get; set; }
    public int ProtocolVersion { get; set; }
    public int PolicyVersion { get; set; }
    public int EligibilityVersion { get; set; }
    public JsonElement Policy { get; set; }
    public string PolicySha256 { get; set; } = "";
    public string Id { get; set; } = "";
    public string State { get; set; } = "Running";
    public string? Reason { get; set; }
    public string Lane { get; set; } = "";
    public DateTimeOffset Started { get; set; }
    public DateTimeOffset Updated { get; set; }
    public string CatalogSha256 { get; set; } = "";
    public SourceEvidence Before { get; set; } = null!;
    public SourceEvidence? After { get; set; }
    public JsonElement Machine { get; set; }
    public JsonElement? MachineAfter { get; set; }
    public JsonElement Toolchains { get; set; }
    public JsonElement RuntimeConfig { get; set; }
    public List<BinaryIdentity> Files { get; set; } = [];
    public List<BinaryIdentity>? FilesAfter { get; set; }
    public List<QualificationCase> Cases { get; set; } = [];
    public string[] RequestedCaseIds { get; set; } = [];
    public int CatalogCaseCount { get; set; }
    public QuietEvidence? InitialGate { get; set; }
    public QuietEvidence? FinalGate { get; set; }
    // Raw data is authoritative. The renderer recomputes qualification.
    public bool PerformanceQualified { get; set; }
}
internal sealed record CaseAssessment(string Status, string[] Reasons, SessionStatistics[] Sessions);

internal static class QualificationEvidence
{
    internal static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, ComparisonProtocol.JsonOptions);
    internal static string CanonicalJson(JsonElement value) => JsonSerializer.Serialize(value, ComparisonProtocol.JsonOptions);
    internal static string PolicyHash => ComparisonProtocol.Digest(CanonicalJson(Json(ComparisonPolicy.Describe())));

    public static void ValidateProfile(JsonElement left, JsonElement right, SourceEvidence source, JsonElement toolchains)
    {
        if (source.Status.Length != 0 || source.Revision.Length != 40 || source.Sdk != "10.0.401")
            throw new InvalidDataException("Qualification requires a clean committed checkout and SDK 10.0.401.");
        var python = toolchains.GetProperty("python");
        var dotnet = toolchains.GetProperty("dotnet");
        if (dotnet.GetProperty("sdk").GetString() != source.Sdk || dotnet.GetProperty("runtime").GetString() != "10.0.12"
            || left.GetProperty("engine").GetString() != "Lython" || right.GetProperty("engine").GetString() != "CPython"
            || left.GetProperty("runtimeVersion").GetString() != "10.0.12"
            || left.GetProperty("architecture").GetString() != "X64" || right.GetProperty("architecture").GetString() != "x86_64"
            || left.GetProperty("processorCount").GetInt32() != 4 || left.GetProperty("benchmarkDotNetLoaded").GetBoolean()
            || left.GetProperty("serverGc").GetBoolean() || left.GetProperty("gcLatencyMode").GetString() != "Interactive"
            || left.GetProperty("publicLimits").GetString() != "ordinary defaults; instruction fuel unset; no forced GC"
            || !left.GetProperty("clockHighResolution").GetBoolean() || !right.GetProperty("clockMonotonic").GetBoolean()
            || left.GetProperty("runtimeOverrides").EnumerateObject().Any(p => p.Value.ValueKind != JsonValueKind.Null)
            || !right.GetProperty("gcEnabled").GetBoolean() || !right.GetProperty("gilEnabled").GetBoolean()
            || right.GetProperty("debug").GetBoolean() || right.GetProperty("freeThreaded").GetBoolean()
            || right.GetProperty("version").GetString() != python.GetProperty("version").GetString()
            || !right.GetProperty("version").GetString()!.StartsWith("3.13.16 ", StringComparison.Ordinal)
            || right.GetProperty("executable").GetString() != python.GetProperty("executable").GetString()
            || right.GetProperty("executableSha256").GetString() != python.GetProperty("sha256").GetString()
            || right.GetProperty("configArgs").GetString() != python.GetProperty("configArgs").GetString()
            || right.GetProperty("flags").GetString() != python.GetProperty("flags").GetString()
            || !right.GetProperty("flags").GetString()!.Contains("isolated=1", StringComparison.Ordinal)
            || !right.GetProperty("flags").GetString()!.Contains("no_site=1", StringComparison.Ordinal)
            || !right.GetProperty("flags").GetString()!.Contains("ignore_environment=1", StringComparison.Ordinal)
            || !python.GetProperty("configArgs").GetString()!.Contains("--enable-optimizations", StringComparison.Ordinal)
            || !python.GetProperty("configArgs").GetString()!.Contains("--with-lto", StringComparison.Ordinal)
            || python.GetProperty("jit").GetString() != "disabled at build time")
            throw new InvalidDataException("Actual worker profile differs from the frozen ordinary four-core Linux reference.");
        foreach (var identity in new[] { left, right })
            if (identity.GetProperty("protocolVersion").GetInt32() != ComparisonProtocol.Version
                || identity.GetProperty("catalogVersion").GetInt32() != WorkloadCatalog.Version
                || identity.GetProperty("clockFrequency").GetInt64() <= 0
                || identity.GetProperty("maximumBatchIterations").GetInt32() != ComparisonProtocol.MaximumBatchIterations
                || identity.GetProperty("maximumBatchSeconds").GetInt32() != ComparisonProtocol.MaximumBatchSeconds
                || identity.GetProperty("maximumFrameBytes").GetInt32() != ComparisonProtocol.MaximumFrameBytes)
                throw new InvalidDataException("Actual protocol/clock/caps differ from the frozen profile.");
        var assemblies = left.GetProperty("libraries").EnumerateArray().ToArray();
        if (assemblies.Length != 3 || assemblies.Any(a => a.GetProperty("configuration").GetString() != "Release")
            || assemblies.Count(a => a.GetProperty("version").GetString()!.EndsWith("+" + source.Revision, StringComparison.Ordinal)) != 2)
            throw new InvalidDataException("Both Release producer assemblies must match the clean committed revision.");
        if (assemblies.Where(a => a.GetProperty("version").GetString()!.EndsWith("+" + source.Revision, StringComparison.Ordinal))
            .Any(a => a.GetProperty("buildSdk").GetString() != source.Sdk))
            throw new InvalidDataException("Actual producer build SDK differs from the pinned SDK.");
    }

    public static bool Semantic(WorkerResponse response, ComparisonWorkload workload, string status, int count, long frequency)
        => response.ProtocolVersion == ComparisonProtocol.Version && response.CaseId == workload.Id
        && response.RequestId > 0 && response.Status == status && response.CompletedInvocations == count
        && response.ClockFrequency == frequency && response.SourceSha256 == workload.SourceSha256
        && response.FixtureSha256 == workload.FixtureSha256 && response.ExpectedOutputSha256 == workload.ExpectedOutputSha256
        && response.Reason is null && (status == "Equivalent" ? response.ElapsedTicks is null : response.ElapsedTicks > 0)
        && (status != "Completed" || response.ActualOutputSha256 is not null)
        && response.ActualOutputSha256 == workload.ExpectedOutputSha256;

    public static CaseAssessment Assess(QualificationReceipt receipt, QualificationCase row)
    {
        var reasons = new List<string>();
        if (!CompleteCampaign(receipt)) reasons.Add("Campaign is incomplete or its policy/provenance/quietness changed.");
        if (row.State != "Measured" || row.Sessions.Count != ComparisonPolicy.Sessions
            || !row.Sessions.Select(s => s.Sampling.Index).SequenceEqual(Enumerable.Range(0, ComparisonPolicy.Sessions))
            || row.Sessions.Select(s => s.Sampling.Id).Distinct(StringComparer.Ordinal).Count() != ComparisonPolicy.Sessions)
            reasons.Add("Three distinct complete independent sessions are required.");
        foreach (var engine in new[] { "LythonIdentity", "PythonIdentity" })
            if (row.Sessions.Select(s => ProcessId(engine == "LythonIdentity" ? s.LythonIdentity : s.PythonIdentity))
                .Where(pid => pid > 0).Distinct().Count() != ComparisonPolicy.Sessions)
                reasons.Add("Independent worker process identities are missing or repeated.");
        var statistics = new List<SessionStatistics>();
        foreach (var session in row.Sessions)
        {
            var complete = reasons.Count == 0 && SessionComplete(receipt, row, session);
            var stats = PairedSampler.Evaluate(session.Sampling, complete);
            statistics.Add(stats);
        }
        if (statistics.Any(s => !s.Qualified)) reasons.Add("At least one session fails semantic, timing, stability or evidence eligibility.");
        if (statistics.Count == ComparisonPolicy.Sessions && statistics.All(s => s.Qualified)
            && statistics.Max(s => s.Ratio!.Value) / statistics.Min(s => s.Ratio!.Value) > ComparisonPolicy.MaximumSessionFactor)
            reasons.Add("Independent-session median ratios differ by more than ten percent.");
        if (row.Workload.Category == "control")
            return new(reasons.Count == 0 ? "Control" : "Unqualified control", [.. reasons], [.. statistics]);
        foreach (var controlId in new[] { "control.empty.control", "control.tiny.control" })
        {
            var control = receipt.Cases.SingleOrDefault(c => c.Workload.Id == controlId);
            if (control is null) { reasons.Add("Required invocation control is missing: " + controlId); continue; }
            var assessment = Assess(receipt, control);
            if (assessment.Status != "Control") { reasons.Add("Invocation control did not qualify: " + controlId); continue; }
            for (var i = 0; i < statistics.Count && i < assessment.Sessions.Length; i++)
                if (statistics[i].LythonMedianSeconds * ComparisonPolicy.MaximumControlFraction <= assessment.Sessions[i].LythonMedianSeconds
                    || statistics[i].PythonMedianSeconds * ComparisonPolicy.MaximumControlFraction <= assessment.Sessions[i].PythonMedianSeconds)
                    reasons.Add("Job is dominated by the invocation control in session " + (i + 1) + ".");
        }
        return new(reasons.Count == 0 ? "Qualified" : "Unqualified", reasons.Distinct(StringComparer.Ordinal).ToArray(), [.. statistics]);
        static int ProcessId(JsonElement identity) => identity.ValueKind == JsonValueKind.Object
            && identity.TryGetProperty("processId", out var pid) && pid.TryGetInt32(out var value) ? value : 0;
    }

    public static bool CompleteCampaign(QualificationReceipt receipt)
    {
        try
        {
            if (receipt.SchemaVersion != 1 || receipt.ProtocolVersion != ComparisonProtocol.Version
                || receipt.PolicyVersion != ComparisonPolicy.Version || receipt.EligibilityVersion != ComparisonPolicy.EligibilityVersion
                || receipt.PolicySha256 != PolicyHash || ComparisonProtocol.Digest(CanonicalJson(receipt.Policy)) != PolicyHash
                || receipt.State != "Completed" || receipt.Before != receipt.After || receipt.Before.Status.Length != 0
                || !Guid.TryParseExact(receipt.Id, "N", out _) || receipt.Started == default || receipt.Updated < receipt.Started
                || receipt.Files.Count == 0 || receipt.FilesAfter is null || !receipt.Files.SequenceEqual(receipt.FilesAfter)
                || receipt.Files.Any(f => !f.Path.StartsWith("/", StringComparison.Ordinal) || f.Sha256.Length != 64
                    || f.Sha256.Any(c => !char.IsAsciiHexDigitLower(c)))
                || receipt.MachineAfter is null || MachineFingerprint(receipt.Machine) != MachineFingerprint(receipt.MachineAfter.Value)
                || receipt.InitialGate is null || receipt.FinalGate is null
                || !QuietMachineProbe.Evaluate(receipt.InitialGate.Windows).Quiet || !QuietMachineProbe.Evaluate(receipt.FinalGate.Windows).Quiet
                || receipt.Lane is not ("warm" or "compile-run" or "compile" or "fresh-process")
                || receipt.Cases.Count < 2 || receipt.Cases.Any(c => c.State is not ("Measured" or "Unqualified"))
                || receipt.CatalogCaseCount < receipt.Cases.Count || receipt.CatalogCaseCount > 256
                || !receipt.RequestedCaseIds.SequenceEqual(receipt.Cases.Select(c => c.Workload.Id))
                || receipt.Cases.Select(c => c.Workload.Id).Distinct(StringComparer.Ordinal).Count() != receipt.Cases.Count)
                return false;
            var canonical = WorkloadCatalog.Create().ToDictionary(c => c.Id, StringComparer.Ordinal);
            return receipt.Cases.All(row => canonical.TryGetValue(row.Workload.Id, out var workload) && workload == row.Workload);
        }
        catch (Exception failure) when (failure is InvalidOperationException or KeyNotFoundException or NullReferenceException or InvalidDataException or ArgumentException)
        { return false; }
    }

    private static bool SessionComplete(QualificationReceipt receipt, QualificationCase row, QualificationSession session)
    {
        try
        {
            var workload = row.Workload;
            ValidateProfile(session.LythonIdentity, session.PythonIdentity, receipt.Before, receipt.Toolchains);
            ValidateRuntimeConfig(receipt.RuntimeConfig);
            var left = session.LythonIdentity; var right = session.PythonIdentity;
            if (!session.Closed || session.Sampling.State != "Measured"
                || left.GetProperty("catalogSha256").GetString() != receipt.CatalogSha256
                || right.GetProperty("catalogSha256").GetString() != receipt.CatalogSha256
                || session.Verification.Count != 3 || !session.Verification.Select(v => v.Phase).SequenceEqual(new[] { "before", "after-warmup", "after" }))
                return false;
            if (receipt.Lane != "fresh-process")
            {
                foreach (var lython in new[] { true, false })
                {
                    var requests = new List<int> { (lython ? session.Verification[0].Lython : session.Verification[0].Python).RequestId };
                    requests.AddRange((lython ? session.Sampling.LythonWarmup : session.Sampling.PythonWarmup).Select(b => b.Response.RequestId));
                    requests.Add((lython ? session.Verification[1].Lython : session.Verification[1].Python).RequestId);
                    requests.AddRange((lython ? session.Sampling.LythonCalibration : session.Sampling.PythonCalibration).Select(b => b.Response.RequestId));
                    requests.AddRange(session.Sampling.Pairs.Select(p => (lython ? p.Lython : p.Python).Response.RequestId));
                    requests.Add((lython ? session.Verification[2].Lython : session.Verification[2].Python).RequestId);
                    if (requests.Zip(requests.Skip(1)).Any(p => p.First >= p.Second)) return false;
                }
            }
            foreach (var check in session.Verification)
                if (!Semantic(check.Lython, workload, "Equivalent", 2, left.GetProperty("clockFrequency").GetInt64())
                    || !Semantic(check.Python, workload, "Equivalent", 2, right.GetProperty("clockFrequency").GetInt64())) return false;
            var parentFrequency = receipt.Machine.GetProperty("clockFrequency").GetInt64();
            if (session.Sampling.Gates.SelectMany(g => g.Windows).Concat(session.Sampling.Pairs.Select(p => p.Noise))
                .Any(w => w.Before.ClockFrequency != parentFrequency || w.After.ClockFrequency != parentFrequency)) return false;
            var attempts = session.Sampling.Attempts;
            if (!attempts.Select(a => a.Sequence).SequenceEqual(Enumerable.Range(0, attempts.Count))) return false;
            var expected = new List<(bool Left, BatchEvidence Batch)>();
            expected.AddRange(session.Sampling.LythonWarmup.Select(b => (true, b)));
            expected.AddRange(session.Sampling.PythonWarmup.Select(b => (false, b)));
            expected.AddRange(session.Sampling.LythonCalibration.Select(b => (true, b)));
            expected.AddRange(session.Sampling.PythonCalibration.Select(b => (false, b)));
            foreach (var pair in session.Sampling.Pairs)
            {
                expected.Add(pair.LythonFirst ? (true, pair.Lython) : (false, pair.Python));
                expected.Add(pair.LythonFirst ? (false, pair.Python) : (true, pair.Lython));
            }
            if (attempts.Count != expected.Count) return false;
            for (var i = 0; i < attempts.Count; i++)
            {
                var attempt = attempts[i];
                if (attempt.Lython != expected[i].Left || attempt.Batch != expected[i].Batch) return false;
                var frequency = receipt.Lane == "fresh-process" ? receipt.Machine.GetProperty("clockFrequency").GetInt64()
                    : (attempt.Lython ? left : right).GetProperty("clockFrequency").GetInt64();
                var response = attempt.Batch.Response;
                if (!Semantic(response, workload, "Completed", attempt.Batch.RequestedIterations, frequency)
                    && !(receipt.Lane == "compile" && response.ActualOutputSha256 is null
                        && Semantic(response with { ActualOutputSha256 = workload.ExpectedOutputSha256 }, workload, "Completed", attempt.Batch.RequestedIterations, frequency)))
                    return false;
            }
            foreach (var identity in left.GetProperty("libraries").EnumerateArray())
                if (!receipt.Files.Any(f => f.Path == identity.GetProperty("path").GetString() && f.Sha256 == identity.GetProperty("sha256").GetString())) return false;
            if (!receipt.Files.Any(f => f.Path == right.GetProperty("executable").GetString() && f.Sha256 == right.GetProperty("executableSha256").GetString())
                || !receipt.Files.Any(f => f.Sha256 == right.GetProperty("adapterSha256").GetString())) return false;
            if (receipt.Lane == "fresh-process")
            {
                var expectedPayload = ComparisonProtocol.Digest(SerializeAtomic(new
                { schemaVersion = 1, catalogVersion = WorkloadCatalog.Version, cases = new[] { workload } }));
                if (row.Payload is null || row.Payload.Sha256 != expectedPayload) return false;
                if (session.Fresh.Count != attempts.Sum(a => a.Batch.RequestedIterations)) return false;
                foreach (var attempt in attempts)
                {
                    var observations = session.Fresh.Where(f => f.Lython == attempt.Lython && f.BatchRequestId == attempt.Batch.Response.RequestId).ToArray();
                    if (observations.Length != attempt.Batch.RequestedIterations
                        || observations.Any(f => f.Observation.Status != "Equivalent" || f.Observation.ElapsedTicks <= 0
                            || f.Observation.ClockFrequency != parentFrequency || f.Observation.Response.RequestId != 1
                            || !Semantic(f.Observation.Response, workload, "Equivalent", 1,
                                (f.Lython ? left : right).GetProperty("clockFrequency").GetInt64()))
                        || observations.Sum(f => f.Observation.ElapsedTicks!.Value) != attempt.Batch.Response.ElapsedTicks) return false;
                    var processIds = observations.Select(f => f.Observation.Identity.GetProperty("processId").GetInt32()).ToArray();
                    if (processIds.Any(id => id <= 0) || processIds.Distinct().Count() != processIds.Length) return false;
                    foreach (var fresh in observations)
                        FreshProcessRunner.ValidateIdentity(fresh.Observation.Identity, fresh.Lython ? left : right, expectedPayload,
                            fresh.Observation.Identity.GetProperty("processId").GetInt32());
                }
            }
            else if (session.Fresh.Count != 0) return false;
            return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException or KeyNotFoundException or NullReferenceException or InvalidDataException or ArgumentException)
        { return false; }
    }

    public static string MachineFingerprint(JsonElement inventory)
    {
        var cpu = inventory.GetProperty("cpuInfo").GetString()!;
        var fixedCpu = string.Join('\n', cpu.Split('\n').Where(line => new[] { "processor", "vendor_id", "model name", "physical id", "core id", "siblings", "cpu cores" }
            .Any(prefix => line.StartsWith(prefix + "\t", StringComparison.Ordinal))));
        return ComparisonProtocol.Digest(Json(new
        {
            os = inventory.GetProperty("os"), architecture = inventory.GetProperty("architecture"),
            processorCount = inventory.GetProperty("processorCount"), parentRuntime = inventory.GetProperty("parentRuntime"),
            clockFrequency = inventory.GetProperty("clockFrequency"), clockHighResolution = inventory.GetProperty("clockHighResolution"),
            clockSource = inventory.GetProperty("clockSource"), product = inventory.GetProperty("product"),
            cgroup = inventory.GetProperty("cgroup"), allowedCpus = inventory.GetProperty("allowedCpus"),
            hostname = inventory.GetProperty("hostname"), bootId = inventory.GetProperty("bootId"),
            virtualizationVendor = inventory.GetProperty("virtualizationVendor"), governor = inventory.GetProperty("governor"),
            cpu = fixedCpu, memoryTotal = inventory.GetProperty("memInfo").GetString()!.Split('\n').Single(l => l.StartsWith("MemTotal:", StringComparison.Ordinal)),
            osRelease = inventory.GetProperty("osRelease"), swaps = inventory.GetProperty("swaps"),
        }).GetRawText());
    }

    public static void ValidateRuntimeConfig(JsonElement config)
    {
        var options = config.GetProperty("runtimeOptions");
        var framework = options.GetProperty("framework");
        if (options.GetProperty("tfm").GetString() != "net10.0"
            || framework.GetProperty("name").GetString() != "Microsoft.NETCore.App"
            || framework.GetProperty("version").GetString() != "10.0.0"
            || options.EnumerateObject().Any(p => p.Name is not ("tfm" or "framework" or "configProperties")))
            throw new InvalidDataException("Runtime configuration differs from the default SDK-produced profile.");
        var properties = options.GetProperty("configProperties").EnumerateObject().ToArray();
        if (properties.Length != 2 || properties.Any(p => p.Name is not
            ("System.Reflection.Metadata.MetadataUpdater.IsSupported" or "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization")
            || p.Value.ValueKind != JsonValueKind.False))
            throw new InvalidDataException("Primary qualification cannot use runtimeconfig GC/tiering/PGO or other tuning overrides.");
    }

    // Read-only provenance commands use the same bounded owned process envelope
    // as workers. Nothing invokes a shell or resolves a different runtime.
    public static async Task<string> CaptureAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await using var process = await OwnedWorkerProcess.StartAsync(new(executable, arguments, Environment.CurrentDirectory), deadline.Token).ConfigureAwait(false);
        process.Input.Dispose();
        var stdout = DrainAsync(process.Output); var stderr = DrainAsync(process.Error);
        try
        {
            await Task.WhenAll(stdout, stderr, process.Exit).WaitAsync(deadline.Token).ConfigureAwait(false);
            if (await process.Exit.ConfigureAwait(false) != 0 || (await stderr.ConfigureAwait(false)).Length != 0)
                throw new InvalidDataException("Provenance command failed: " + executable);
            return ComparisonProtocol.Utf8.GetString(await stdout.ConfigureAwait(false)).TrimEnd('\r', '\n');
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
            try { await Task.WhenAll(stdout, stderr).WaitAsync(ComparisonWorkerClient.ShutdownDeadline).ConfigureAwait(false); }
            catch when (stdout.IsCompleted && stderr.IsCompleted) { }
        }
        async Task<byte[]> DrainAsync(Stream stream)
        {
            using var bytes = new MemoryStream(); var buffer = new byte[4096];
            try
            {
                int count;
                while ((count = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
                {
                    if (bytes.Length + count > 1024 * 1024) throw new InvalidDataException("Provenance output exceeds one MiB.");
                    bytes.Write(buffer, 0, count);
                }
                return bytes.ToArray();
            }
            catch { deadline.Cancel(); throw; }
        }
    }
}
