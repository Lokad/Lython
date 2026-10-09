using System.Text.Json;
using System.Text.Json.Nodes;
using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonSamplingTests
{
    [Fact]
    public void StablePairsHaveExactKnownRatioAndDeterministicInterval()
    {
        var pairs = Pairs(i => (.001, .002));
        var first = ComparisonStatistics.Evaluate(pairs, true);
        Assert.True(first.Qualified); Assert.Equal(2, first.Ratio!.Value, 12);
        Assert.Equal(2, first.RatioLower!.Value, 12); Assert.Equal(2, first.RatioUpper!.Value, 12);
        Assert.Equal("Lython", first.Winner); Assert.Equal(first, ComparisonStatistics.Evaluate(pairs, true) with { Reasons = first.Reasons });
    }

    [Fact]
    public void IntervalCrossingParityHasNoWinner()
    {
        var result = ComparisonStatistics.Evaluate(Pairs(i => (.001, .001 * (1 + (i - 5) * .002))), true);
        Assert.True(result.Qualified); Assert.Equal("No winner", result.Winner);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("nan")]
    [InlineData("negative")]
    [InlineData("order")]
    [InlineData("noise")]
    [InlineData("incomplete")]
    public void InvalidUnstableOrIncompletePairsCannotProduceARatio(string defect)
    {
        var pairs = Pairs(i => (.001, .002));
        if (defect == "missing") pairs.RemoveAt(0);
        if (defect == "duplicate") pairs[1] = pairs[1] with { Index = 0 };
        if (defect == "nan") pairs[0] = pairs[0] with { LythonSeconds = double.NaN };
        if (defect == "negative") pairs[0] = pairs[0] with { PythonSeconds = -1 };
        if (defect == "order") for (var i = 0; i < pairs.Count; i++) pairs[i] = pairs[i] with { PythonSeconds = pairs[i].LythonFirst ? .0023 : .002 };
        if (defect == "noise") for (var i = 0; i < pairs.Count; i++) pairs[i] = pairs[i] with { LythonSeconds = .001 + i * .0002 };
        var result = ComparisonStatistics.Evaluate(pairs, defect != "incomplete");
        Assert.False(result.Qualified); Assert.Null(result.Ratio); Assert.Null(result.RatioLower); Assert.Null(result.RatioUpper);
    }

    [Fact]
    public void PairOrderingReversesTheSixFiveSplitAcrossIndependentSessions()
    {
        Assert.Equal(new[] { 6, 5, 6 }, Enumerable.Range(0, 3).Select(s => Enumerable.Range(0, 11).Count(p => ComparisonPolicy.LythonFirst(s, p))));
        Assert.Equal(2.5, ComparisonStatistics.Quantile(new[] { 4d, 1d, 3d, 2d }, .5));
        Assert.Equal(1.75, ComparisonStatistics.Quantile(new[] { 4d, 1d, 3d, 2d }, .25));
    }

    [Fact]
    public void LinuxCpuParserDoesNotDoubleCountGuestTime()
    {
        Assert.Equal(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 }, QuietMachineProbe.ParseCpu("cpu 1 2 3 4 5 6 7 8 999 888\ncpu0 1 2 3\n"));
        Assert.Throws<InvalidDataException>(() => QuietMachineProbe.ParseCpu("cpu 1 2 3 4"));
        Assert.Throws<InvalidDataException>(() => QuietMachineProbe.ParseKeyValues("pgmajfault 1\npgmajfault 2\n"));
        Assert.Equal(1234UL, QuietMachineProbe.PressureTotal("some avg10=0.00 avg60=0.00 avg300=0.00 total=1234\n", "some"));
        Assert.Null(QuietMachineProbe.PressureTotal(null, "full"));
    }

    [Theory]
    [InlineData("busy")]
    [InlineData("steal")]
    [InlineData("pgmajfault")]
    [InlineData("pswpin")]
    [InlineData("memory.some.total_us")]
    [InlineData("throttled:/sys/fs/cgroup")]
    [InlineData("regression")]
    [InlineData("frequency")]
    [InlineData("missing")]
    public void RawAccountingRejectsNoiseAndForgedQuietFlags(string defect)
    {
        var windows = Quiet().Windows;
        var before = windows[0].Before; var after = windows[0].After;
        if (defect == "busy") { after.Cpu[0] = 10; after.Cpu[3] = before.Cpu[3] + 90; }
        else if (defect == "steal") after.Cpu[7] = 1;
        else if (defect == "regression") after.Cpu[3] = 0;
        else if (defect == "frequency") after = after with { ClockFrequency = 999 };
        else if (defect == "missing") before.NoiseCounters.Remove("pgmajfault");
        else after.NoiseCounters[defect] = 1;
        // All serialized derived flags lie; only raw counters decide.
        windows[0] = new(1, 0, 0, true, [], before, after);
        Assert.False(QuietMachineProbe.Evaluate(windows).Quiet);
    }

    [Fact]
    public void QuietGateRequiresFiveFullWindowsAndAcceptsExplicitlyUnavailableOptionalCounters()
    {
        Assert.True(Quiet().Quiet);
        Assert.False(QuietMachineProbe.Evaluate(Quiet().Windows.Take(4)).Quiet);
        var windows = Quiet().Windows;
        windows[0] = windows[0] with { After = windows[0].After with { Timestamp = windows[0].Before.Timestamp + 999 } };
        Assert.False(QuietMachineProbe.Evaluate(windows).Quiet);
    }

    [Fact]
    public async Task SamplerWarmsCalibratesRetainsEveryAttemptAndAlternates()
    {
        var trace = new SamplingSession { Index = 1 }; var checkpoints = 0; var hookCalled = false;
        await PairedSampler.RunAsync(trace, (left, count, _) => Task.FromResult(Response(count, count * (left ? .01 : .02))),
            new FakeMachine(), () => checkpoints++, default, _ => { hookCalled = true; Assert.Empty(trace.LythonCalibration); return Task.CompletedTask; });
        Assert.True(hookCalled); Assert.Equal("Measured", trace.State); Assert.True(trace.Statistics!.Qualified);
        Assert.Equal(11, trace.Pairs.Count); Assert.Equal(5, trace.Pairs.Count(p => p.LythonFirst));
        Assert.Equal(trace.LythonWarmup.Count + trace.PythonWarmup.Count + trace.LythonCalibration.Count + trace.PythonCalibration.Count + 22, trace.Attempts.Count);
        Assert.True(checkpoints >= 16); Assert.True(PairedSampler.Evaluate(trace, true).Qualified);
    }

    [Fact]
    public async Task FailedWarmupReplyRemainsInCheckpointEvidence()
    {
        var trace = new SamplingSession();
        await PairedSampler.RunAsync(trace, (_, count, _) => Task.FromResult(Response(count, 1) with { Status = "BudgetDenied", ElapsedTicks = null }),
            new FakeMachine(), () => { }, default);
        Assert.Equal("Unqualified", trace.State); Assert.Single(trace.Attempts);
        Assert.Equal("BudgetDenied", trace.Attempts[0].Batch.Response.Status); Assert.Null(trace.Statistics!.Ratio);
    }

    [Fact]
    public async Task BusyGateStopsBeforeTimingAndPreservesFailedGate()
    {
        var trace = new SamplingSession(); var machine = new FakeMachine { Busy = true };
        await Assert.ThrowsAsync<PairedSampler.QuietGateException>(() => PairedSampler.RunAsync(trace,
            (_, count, _) => Task.FromResult(Response(count, count * .02)), machine, () => { }, default));
        Assert.Equal("Busy", trace.State); Assert.Single(trace.Gates); Assert.Empty(trace.Pairs);
    }

    [Fact]
    public void CalibrationCeilingNeverRelaxesTheTimerFloor()
    {
        Assert.Throws<PairedSampler.SamplingExclusion>(() => PairedSampler.NextCount(1_000_000, .001, .040));
        Assert.Equal(1_000_000, PairedSampler.NextCount(1_000_000, .001, .040, true));
    }

    [Theory]
    [InlineData("warm")]
    [InlineData("compile-run")]
    [InlineData("compile")]
    [InlineData("fresh-process")]
    public async Task ReportRecomputesQualificationAndSurvivesIndentedReceiptRoundTrip(string lane)
    {
        var receipt = await ReceiptAsync(lane);
        Assert.True(QualificationEvidence.CompleteCampaign(receipt));
        Assert.Equal("Qualified", QualificationEvidence.Assess(receipt, receipt.Cases[2]).Status);
        receipt.Cases[2].Sessions[0].Sampling.Statistics = receipt.Cases[2].Sessions[0].Sampling.Statistics! with
        { Ratio = 99, RatioLower = 98, RatioUpper = 100 };
        var roundtrip = JsonSerializer.Deserialize<QualificationReceipt>(JsonSerializer.Serialize(receipt,
            new JsonSerializerOptions(ComparisonProtocol.JsonOptions) { WriteIndented = true }), ComparisonProtocol.JsonOptions)!;
        var assessment = QualificationEvidence.Assess(roundtrip, roundtrip.Cases[2]);
        Assert.True(assessment.Status == "Qualified", string.Join(" | ", assessment.Reasons));
        var report = ComparisonReportCommand.Render(roundtrip, new string('b', 64));
        Assert.Contains("2.000 [2.000, 2.000]", report); Assert.Contains("Control", report);
        Assert.DoesNotContain("overall speedup: 2", report);
    }

    [Theory]
    [InlineData("parent-clock")]
    [InlineData("once-request")]
    [InlineData("repeated-process")]
    [InlineData("process-id")]
    [InlineData("missing-job")]
    [InlineData("extra-job")]
    [InlineData("elapsed-total")]
    [InlineData("payload")]
    [InlineData("runtime")]
    [InlineData("output")]
    [InlineData("job-count")]
    public async Task InvalidFreshProcessEvidenceCannotPublishARatio(string defect)
    {
        var receipt = await ReceiptAsync("fresh-process");
        var row = receipt.Cases[2];
        var session = row.Sessions[0];
        var first = session.Fresh[0];
        var observation = first.Observation;
        if (defect == "parent-clock")
            session.Fresh[0] = first with { Observation = observation with { ClockFrequency = 1 } };
        if (defect == "once-request")
            session.Fresh[0] = first with { Observation = observation with { Response = observation.Response with { RequestId = 2 } } };
        if (defect == "repeated-process")
        {
            var identity = JsonNode.Parse(session.Fresh[1].Observation.Identity.GetRawText())!;
            identity["processId"] = observation.Identity.GetProperty("processId").GetInt32();
            session.Fresh[1] = session.Fresh[1] with
            { Observation = session.Fresh[1].Observation with { Identity = QualificationEvidence.Json(identity) } };
        }
        if (defect == "process-id")
        {
            var identity = JsonNode.Parse(observation.Identity.GetRawText())!;
            identity["processId"] = 0;
            session.Fresh[0] = first with { Observation = observation with { Identity = QualificationEvidence.Json(identity) } };
        }
        if (defect == "missing-job") session.Fresh.RemoveAt(0);
        if (defect == "extra-job") session.Fresh.Add(first);
        if (defect == "elapsed-total")
            session.Fresh[0] = first with { Observation = observation with { ElapsedTicks = observation.ElapsedTicks + 1 } };
        if (defect == "payload") row.Payload = row.Payload! with { Sha256 = new string('c', 64) };
        if (defect == "runtime")
        {
            var identity = JsonNode.Parse(observation.Identity.GetRawText())!;
            identity["runtimeVersion"] = "0.0.0";
            session.Fresh[0] = first with { Observation = observation with { Identity = QualificationEvidence.Json(identity) } };
        }
        if (defect == "output")
            session.Fresh[0] = first with { Observation = observation with { Response = observation.Response with { ActualOutputSha256 = new string('c', 64) } } };
        if (defect == "job-count")
            session.Fresh[0] = first with { Observation = observation with { Response = observation.Response with { CompletedInvocations = 2 } } };
        receipt.PerformanceQualified = true;
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, row).Status);
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(receipt, "test"));
    }

    [Fact]
    public async Task CheckpointBeforeWorkerStartupSerializesAndRendersAsIncomplete()
    {
        var receipt = await ReceiptAsync(); receipt.State = "Running";
        receipt.Cases[0].State = "Running"; receipt.Cases[0].Sessions = [new QualificationSession()];
        var path = Path.Combine(Path.GetTempPath(), "lython-pending-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ComparisonVerifyCommand.WriteAtomic(path, receipt);
            var restored = ComparisonReportCommand.ReadReceipt(path);
            Assert.Equal(JsonValueKind.Null, restored.Cases[0].Sessions[0].LythonIdentity.ValueKind);
            Assert.Equal(JsonValueKind.Null, restored.Cases[0].Sessions[0].PythonIdentity.ValueKind);
            var report = ComparisonReportCommand.Render(restored, "test");
            Assert.Contains("Partial or invalid evidence", report); Assert.DoesNotContain("| Qualified |", report);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("dirty")]
    [InlineData("policy")]
    [InlineData("files")]
    [InlineData("verification")]
    [InlineData("count")]
    [InlineData("short")]
    [InlineData("binary")]
    [InlineData("order")]
    [InlineData("warmup")]
    [InlineData("control")]
    [InlineData("cross-session")]
    [InlineData("scope")]
    [InlineData("replay")]
    [InlineData("gate")]
    [InlineData("runtime-config")]
    public async Task IneligibleReceiptCannotPublishARatioEvenWhenItsQualifiedFlagsLie(string defect)
    {
        var receipt = await ReceiptAsync(); var row = receipt.Cases[2]; var trace = row.Sessions[0].Sampling;
        receipt.PerformanceQualified = true;
        if (defect == "partial") receipt.State = "Busy";
        if (defect == "dirty") receipt.After = receipt.Before with { Status = " M runtime.cs" };
        if (defect == "policy") receipt.PolicyVersion++;
        if (defect == "files") receipt.FilesAfter!.RemoveAt(0);
        if (defect == "verification") row.Sessions[0].Verification.RemoveAt(2);
        if (defect == "count") trace.Pairs[0] = trace.Pairs[0] with { Lython = trace.Pairs[0].Lython with { RequestedIterations = 0 } };
        if (defect == "short") trace.Pairs[0] = trace.Pairs[0] with { Lython = trace.Pairs[0].Lython with { Response = trace.Pairs[0].Lython.Response with { ElapsedTicks = 1 } } };
        if (defect == "binary") receipt.Files = receipt.Files.Select(f => f with { Sha256 = new string('c', 64) }).ToList();
        if (defect == "order") trace.Pairs[0] = trace.Pairs[0] with { LythonFirst = false };
        if (defect == "warmup") trace.LythonWarmup.Clear();
        if (defect == "control") receipt.Cases[0].Sessions.Clear();
        if (defect == "scope") receipt.RequestedCaseIds = receipt.RequestedCaseIds[..2];
        if (defect == "replay")
        {
            trace.Pairs[1] = trace.Pairs[1] with { Lython = trace.Pairs[1].Lython with { Response = trace.Pairs[1].Lython.Response with { RequestId = trace.Pairs[0].Lython.Response.RequestId } } };
            RebuildAttempts(trace);
        }
        if (defect == "gate") trace.Gates[0].Windows[0].After.NoiseCounters["pgmajfault"] = 1;
        if (defect == "runtime-config")
        {
            var config = JsonNode.Parse(receipt.RuntimeConfig.GetRawText())!;
            config["runtimeOptions"]!["configProperties"]!["System.Runtime.TieredCompilation"] = false;
            receipt.RuntimeConfig = QualificationEvidence.Json(config);
        }
        if (defect == "cross-session")
        {
            var other = row.Sessions[1].Sampling;
            // Internally consistent complete evidence, but a different stable
            // ratio in another process session cannot be hidden by pooling.
            foreach (var list in new[] { other.PythonWarmup, other.PythonCalibration })
                for (var i = 0; i < list.Count; i++) list[i] = list[i] with { Response = list[i].Response with { ElapsedTicks = list[i].Response.ElapsedTicks * 2 } };
            for (var i = 0; i < other.Pairs.Count; i++) other.Pairs[i] = other.Pairs[i] with { Python = other.Pairs[i].Python with { Response = other.Pairs[i].Python.Response with { ElapsedTicks = other.Pairs[i].Python.Response.ElapsedTicks * 2 } } };
            RebuildAttempts(other);
        }
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, row).Status);
        var report = ComparisonReportCommand.Render(receipt, "test");
        Assert.DoesNotContain("| Qualified |", report);
    }

    internal static QuietEvidence Quiet()
    {
        return QuietMachineProbe.Evaluate(Enumerable.Range(0, 5).Select(i => QuietMachineProbe.Compare(Snapshot(i * 1000, (ulong)i * 100), Snapshot((i + 1) * 1000, (ulong)(i + 1) * 100))));
    }
    internal static MachineSnapshot Snapshot(long timestamp, ulong idle) => new(DateTimeOffset.UnixEpoch, timestamp * 1_000_000, 1_000_000_000,
        [0, 0, 0, idle, 0, 0, 0, 0], new(StringComparer.Ordinal)
        { ["pgmajfault"] = 0, ["pswpin"] = 0, ["pswpout"] = 0, ["memory.some.total_us"] = null,
            ["memory.full.total_us"] = null, ["throttled:/sys/fs/cgroup"] = null }, "/");
    private static WorkerResponse Response(int count, double seconds) => new(1, 1, "test", "Completed", count, (long)Math.Round(seconds * 1_000_000_000),
        1_000_000_000, "source", "fixture", "expected", "expected", null);
    private static List<TimedPair> Pairs(Func<int, (double Left, double Right)> times) => Enumerable.Range(0, 11)
        .Select(i => new TimedPair(i, ComparisonPolicy.LythonFirst(0, i), times(i).Left, times(i).Right)).ToList();
    private sealed class FakeMachine : ISamplingMachine
    {
        private int _read; public bool Busy { get; set; }
        public Task SettleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<QuietEvidence> CheckAsync(CancellationToken cancellationToken)
        {
            var gate = Quiet();
            if (Busy) gate.Windows[0].After.Cpu[0] = 100;
            return Task.FromResult(QuietMachineProbe.Evaluate(gate.Windows));
        }
        public MachineSnapshot Read() { _read++; return Snapshot(_read * 100, (ulong)_read * 10); }
    }

    private static async Task<QualificationReceipt> ReceiptAsync(string lane = "warm")
    {
        var source = new SourceEvidence(new string('a', 40), "", "10.0.401"); var hash = new string('b', 64);
        var py = QualificationEvidence.Json(new { version = "3.13.16 test", executable = "/python", sha256 = hash,
            configArgs = "--enable-optimizations --with-lto", flags = "isolated=1 no_site=1 ignore_environment=1", jit = "disabled at build time" });
        var tools = QualificationEvidence.Json(new { python = py, dotnet = new { sdk = "10.0.401", runtime = "10.0.12" } });
        var machine = QualificationEvidence.Json(new { os = "Linux", architecture = "X64", processorCount = 4, parentRuntime = "10.0.12",
            clockFrequency = 1_000_000_000L, clockHighResolution = true, cpuInfo = "processor\t: 0\nmodel name\t: TEST\n",
            memInfo = "MemTotal: 16384000 kB\n", clockSource = "tsc", product = "VM", virtualizationVendor = "Microsoft",
            governor = (string?)null, osRelease = "Ubuntu", swaps = "Filename Type Size Used Priority", cgroup = "0::/", allowedCpus = "0-3", hostname = "test", bootId = "test" });
        var files = new[] { "/library", "/adapter", "/core", "/python", "/helper" }.Select(p => new ComparisonVerifyCommand.BinaryIdentity(p, hash)).ToList();
        var receipt = new QualificationReceipt { SchemaVersion = 1, ProtocolVersion = 1, PolicyVersion = 1, EligibilityVersion = 1,
            Id = Guid.NewGuid().ToString("N"), Started = DateTimeOffset.UtcNow, Updated = DateTimeOffset.UtcNow,
            State = "Completed", Lane = lane, Policy = QualificationEvidence.Json(ComparisonPolicy.Describe()), PolicySha256 = QualificationEvidence.PolicyHash,
            Before = source, After = source, Files = files, FilesAfter = files.ToList(), Machine = machine, MachineAfter = machine,
            Toolchains = tools, CatalogSha256 = hash, InitialGate = Quiet(), FinalGate = Quiet(), CatalogCaseCount = WorkloadCatalog.Create().Count };
        receipt.RuntimeConfig = JsonDocument.Parse("""
        {"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},
        "configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
        """).RootElement.Clone();
        var freshProcessId = 1000;
        foreach (var workload in WorkloadCatalog.Create().Where(w => w.Id is "control.empty.control" or "control.tiny.control" or "loops.integer.large"))
        {
            var row = new QualificationCase { Workload = workload, State = "Measured" }; receipt.Cases.Add(row);
            if (lane == "fresh-process")
                row.Payload = new("/inputs/" + workload.Id + ".json", ComparisonProtocol.Digest(ComparisonVerifyCommand.SerializeAtomic(new
                { schemaVersion = 1, catalogVersion = WorkloadCatalog.Version, cases = new[] { workload } })));
            for (var index = 0; index < 3; index++)
            {
                var left = QualificationEvidence.Json(new { protocolVersion = 1, status = "Ready", engine = "Lython", catalogVersion = 1, catalogSha256 = hash,
                    processId = 100 + 2 * index,
                    runtimeVersion = "10.0.12", architecture = "X64", processorCount = 4, benchmarkDotNetLoaded = false,
                    serverGc = false, gcLatencyMode = "Interactive", clockHighResolution = true, runtimeOverrides = new { DOTNET_PROCESSOR_COUNT = (string?)null },
                    publicLimits = "ordinary defaults; instruction fuel unset; no forced GC",
                    clockFrequency = 1_000_000_000L, maximumBatchIterations = 1_000_000, maximumBatchSeconds = 60, maximumFrameBytes = 4194304,
                    libraries = files.Take(3).Select(f => new { path = f.Path, sha256 = f.Sha256, moduleId = "11111111-1111-1111-1111-111111111111",
                        configuration = "Release", buildSdk = "10.0.401", version = f.Path == "/core" ? "10.0.12+core" : "1+" + source.Revision }) });
                var rightNode = JsonNode.Parse(left.GetRawText())!.AsObject();
                rightNode["engine"] = "CPython"; rightNode["architecture"] = "x86_64"; rightNode["clockMonotonic"] = true;
                rightNode["processId"] = 101 + 2 * index;
                rightNode["gcEnabled"] = true; rightNode["gilEnabled"] = true; rightNode["debug"] = false; rightNode["freeThreaded"] = false;
                rightNode["version"] = "3.13.16 test"; rightNode["executable"] = "/python"; rightNode["executableSha256"] = hash;
                rightNode["adapterSha256"] = hash; rightNode["configArgs"] = "--enable-optimizations --with-lto"; rightNode["flags"] = "isolated=1 no_site=1 ignore_environment=1";
                rightNode["captureByteLimit"] = 16 * 1024 * 1024;
                var session = new QualificationSession { Closed = true, LythonIdentity = left, PythonIdentity = QualificationEvidence.Json(rightNode), Sampling = new() { Index = index } };
                row.Sessions.Add(session); var request = 0;
                WorkerResponse Make(int count, double? seconds) => new(1, ++request, workload.Id, seconds is null ? "Equivalent" : "Completed", count,
                    seconds is null ? null : (long)Math.Round(seconds.Value * 1e9), 1_000_000_000, workload.SourceSha256, workload.FixtureSha256,
                    workload.ExpectedOutputSha256, lane == "compile" && seconds is not null ? null : workload.ExpectedOutputSha256, null);
                void Verify(string phase) => session.Verification.Add(new(phase, Make(2, null), Make(2, null)));
                Verify("before");
                var perJob = lane == "fresh-process" ? workload.Category == "control" ? .025 : .5
                    : workload.Category == "control" ? .00001 : .001;
                await PairedSampler.RunAsync(session.Sampling, (l, count, _) =>
                {
                    var response = Make(count, perJob * count * (l ? 1 : 2));
                    if (lane == "fresh-process")
                    {
                        for (var job = 0; job < count; job++)
                        {
                            var identity = JsonNode.Parse((l ? session.LythonIdentity : session.PythonIdentity).GetRawText())!;
                            identity["catalogSha256"] = row.Payload!.Sha256;
                            identity["processId"] = ++freshProcessId;
                            if (l)
                                foreach (var library in identity["libraries"]!.AsArray()) library!["sha256"] = null;
                            else
                            {
                                identity.AsObject().Remove("executableSha256");
                                identity.AsObject().Remove("adapterSha256");
                                identity["isolated"] = true;
                                identity["noSite"] = true;
                            }
                            var once = response with { RequestId = 1, Status = "Equivalent", CompletedInvocations = 1, ElapsedTicks = null };
                            session.Fresh.Add(new(l, response.RequestId, new("Equivalent", (long)Math.Round(perJob * (l ? 1 : 2) * 1e9),
                                1_000_000_000, once, QualificationEvidence.Json(identity))));
                        }
                    }
                    return Task.FromResult(response);
                }, new FakeMachine(), () => { }, default,
                    _ => { Verify("after-warmup"); return Task.CompletedTask; });
                Verify("after");
            }
        }
        receipt.RequestedCaseIds = receipt.Cases.Select(c => c.Workload.Id).ToArray();
        Assert.Equal(3, receipt.Cases.Count); return receipt;
    }
    private static void RebuildAttempts(SamplingSession trace)
    {
        trace.Attempts.Clear();
        void Add(bool left, BatchEvidence batch) => trace.Attempts.Add(new(trace.Attempts.Count, left, batch));
        trace.LythonWarmup.ForEach(b => Add(true, b)); trace.PythonWarmup.ForEach(b => Add(false, b));
        trace.LythonCalibration.ForEach(b => Add(true, b)); trace.PythonCalibration.ForEach(b => Add(false, b));
        foreach (var pair in trace.Pairs) { if (pair.LythonFirst) { Add(true, pair.Lython); Add(false, pair.Python); } else { Add(false, pair.Python); Add(true, pair.Lython); } }
    }
}
