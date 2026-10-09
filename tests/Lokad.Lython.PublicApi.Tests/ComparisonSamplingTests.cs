using System.Text.Json;
using System.Text.Json.Nodes;
using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonSamplingTests
{
    [Fact]
    public void CoreLoopProfileKeepsTheSameReductionAndSmallIndependentOutputsAtThreeSizes()
    {
        var all = WorkloadCatalog.Create().ToDictionary(w => w.Id);
        var loops = ComparisonPolicy.CoreLoopCaseIds.Skip(2).Select(id => all[id]).ToArray();
        Assert.Equal(new[] { 256, 2048, 16384 }, loops.Select(w => w.Size));
        Assert.All(ComparisonPolicy.CoreLoopCaseIds.Take(2), id => Assert.Equal("control", all[id].Category));
        Assert.All(loops, loop =>
        {
            Assert.Contains("for i in range(N):\n    total += i\nprint(total)", loop.Source);
            Assert.Equal(((long)loop.Size * (loop.Size - 1) / 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n", loop.ExpectedOutput);
            Assert.InRange(loop.ExpectedOutputUtf8Bytes, 1, 16);
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("short")]
    [InlineData("frequency")]
    [InlineData("cgroup")]
    [InlineData("future")]
    public async Task PreparationRunsAfterCalibrationAndInvalidPauseEvidenceCannotQualify(string defect)
    {
        var trace = new SamplingSession();
        var machine = new FakeMachine { PreparationHook = () =>
        {
            Assert.NotEmpty(trace.LythonCalibration);
            Assert.NotEmpty(trace.PythonCalibration);
            Assert.Empty(trace.Pairs);
            Assert.Empty(trace.Gates);
        } };
        await PairedSampler.RunAsync(trace, (_, count, _) => Task.FromResult(Response(count, count * .02)), machine, () => { }, default);
        Assert.True(PairedSampler.Evaluate(trace, true).Qualified);
        var pause = trace.PreparationPause!;
        trace.PreparationPause = defect switch
        {
            "missing" => null,
            "short" => pause with { After = pause.After with { Timestamp = pause.Before.Timestamp + 1 } },
            "frequency" => pause with { After = pause.After with { ClockFrequency = 1 } },
            "future" => pause with { After = pause.After with { Timestamp = trace.Gates[0].Windows[0].Before.Timestamp + 1 } },
            _ => pause with { After = pause.After with { Cgroup = "/changed" } },
        };
        Assert.False(PairedSampler.Evaluate(trace, true).Qualified);
        var receipt = await ReceiptAsync();
        receipt.Cases[2].Sessions[0].Sampling.PreparationPause = trace.PreparationPause;
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, receipt.Cases[2]).Status);
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(receipt, "test"));
    }

    [Fact]
    public async Task CancellationDuringPreparationLeavesNoMeasuredPairsOrPauseProof()
    {
        using var cancellation = new CancellationTokenSource();
        var trace = new SamplingSession();
        var machine = new FakeMachine { PreparationHook = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PairedSampler.RunAsync(trace,
            (_, count, _) => Task.FromResult(Response(count, count * .02)), machine, () => { }, cancellation.Token));
        Assert.Equal("Interrupted", trace.State);
        Assert.Null(trace.PreparationPause);
        Assert.Empty(trace.Pairs);
    }

    [Theory]
    [InlineData("warm")]
    [InlineData("compile")]
    [InlineData("compile-run")]
    [InlineData("fresh-process")]
    public async Task WarmupUsesTheRecordedLaneAndCannotSubstituteFreshPreparationForPersistentEvidence(string lane)
    {
        var trace = new SamplingSession { Lane = lane };
        await PairedSampler.RunAsync(trace, (_, count, _) => Task.FromResult(new WorkerResponse(1, 1, "test", "Completed", count,
            count * 10_000_000L, 1_000_000_000, "source", "fixture", "expected", "expected", null)), new FakeMachine(), () => { }, default);
        Assert.Equal("Measured", trace.State);
        var expected = lane == "fresh-process" ? (.1, 8L) : (1d, 32L);
        Assert.All(new[] { trace.LythonWarmup, trace.PythonWarmup }, batches =>
        {
            Assert.True(batches.Sum(b => b.Seconds) >= expected.Item1);
            Assert.True(batches.Sum(b => (long)b.Response.CompletedInvocations) >= expected.Item2);
        });
        if (lane == "fresh-process")
        {
            trace.Lane = "warm";
            Assert.False(PairedSampler.Evaluate(trace, true).Qualified);
        }
        var receipt = await ReceiptAsync(lane);
        receipt.Cases[2].Sessions[0].Sampling.Lane = lane == "fresh-process" ? "warm" : "fresh-process";
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, receipt.Cases[2]).Status);
    }

    [Fact]
    public void QuickCatalogRetainsIndependentGoldensAndTheTwoInvocationControls()
    {
        var full = WorkloadCatalog.Create().ToDictionary(w => w.Id);
        var quick = ComparisonPolicy.QuickCaseIds.Select(id => full[id]).ToArray();
        Assert.Equal(14, quick.Length);
        Assert.Equal(quick.Length, quick.Select(w => w.Id).Distinct().Count());
        Assert.All(quick.Take(2), w => Assert.Equal("control", w.Category));
        Assert.All(quick.Skip(2), w => Assert.NotEqual("control", w.Category));
        Assert.All(quick, w => Assert.Equal(w.ExpectedOutputSha256, ComparisonProtocol.Digest(w.ExpectedOutput)));
        Assert.Contains(quick, w => w.Id == "loops.integer.medium");
        Assert.Contains(quick, w => w.Id == "loops.integer.large");
    }

    [Fact]
    public async Task LaneBudgetCoversRetriesAndOverBudgetEvidenceCannotQualify()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddSeconds(-500);
        Assert.Equal(TimeSpan.FromSeconds(90), ComparisonQualificationCommand.RemainingCollectionTime(start, now));
        Assert.Throws<InvalidDataException>(() => ComparisonQualificationCommand.RemainingCollectionTime(now.AddSeconds(-590), now));
        Assert.Throws<InvalidDataException>(() => ComparisonQualificationCommand.RemainingCollectionTime(now.AddSeconds(1), now));
        var previous = await ReceiptAsync();
        previous.State = "Busy"; previous.Started = now.AddSeconds(-601);
        var next = NextAttempt(previous);
        Assert.Throws<InvalidDataException>(() => ComparisonQualificationCommand.RestoreForResume(previous, next));
        Assert.All(next.Cases, row => Assert.Empty(row.Sessions));
        previous.State = "Completed"; previous.Updated = now;
        Assert.False(QualificationEvidence.CompleteCampaign(previous));
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(previous, "test"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("tiered")]
    [InlineData("gc")]
    [InlineData("worker")]
    public async Task SupervisorIsolationIsRequiredAndCannotJustifyWorkerTuning(string defect)
    {
        var receipt = await ReceiptAsync();
        if (defect == "missing") receipt.SupervisorOverrides = JsonSerializer.SerializeToElement<object?>(null);
        else if (defect == "worker")
        {
            var worker = JsonNode.Parse(receipt.Cases[2].Sessions[0].LythonIdentity.GetRawText())!;
            worker["runtimeOverrides"]!["DOTNET_TieredCompilation"] = "0";
            receipt.Cases[2].Sessions[0].LythonIdentity = QualificationEvidence.Json(worker);
        }
        else
        {
            var supervisor = JsonNode.Parse(receipt.SupervisorOverrides.GetRawText())!;
            supervisor[defect == "tiered" ? "DOTNET_TieredCompilation" : "DOTNET_gcServer"] = "1";
            receipt.SupervisorOverrides = QualificationEvidence.Json(supervisor);
        }
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, receipt.Cases[2]).Status);
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(receipt, "test"));
    }

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
        var result = ComparisonStatistics.Evaluate(Pairs(i => (.001, .001 * (1 + (i - ComparisonPolicy.Pairs / 2) * .002))), true);
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
    public void PairOrderingReversesTheFourThreeSplitAcrossIndependentSessions()
    {
        Assert.Equal(new[] { 4, 3, 4 }, Enumerable.Range(0, 3).Select(s => Enumerable.Range(0, ComparisonPolicy.Pairs).Count(p => ComparisonPolicy.LythonFirst(s, p))));
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
    public void QuietGateRequiresCompleteWindowsAndAcceptsExplicitlyUnavailableOptionalCounters()
    {
        Assert.True(Quiet().Quiet);
        Assert.False(QuietMachineProbe.Evaluate(Quiet().Windows.Take(ComparisonPolicy.IdleWindows - 1)).Quiet);
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
        Assert.Equal(ComparisonPolicy.Pairs, trace.Pairs.Count); Assert.Equal(3, trace.Pairs.Count(p => p.LythonFirst));
        Assert.Equal(trace.LythonWarmup.Count + trace.PythonWarmup.Count + trace.LythonCalibration.Count + trace.PythonCalibration.Count + 2 * ComparisonPolicy.Pairs, trace.Attempts.Count);
        Assert.True(checkpoints >= ComparisonPolicy.Pairs + 7); Assert.True(PairedSampler.Evaluate(trace, true).Qualified);
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
    [InlineData("warm", "Unqualified")]
    [InlineData("compile-run", "Unqualified")]
    [InlineData("compile", "Qualified")]
    [InlineData("fresh-process", "Qualified")]
    public async Task ControlFloorDependsOnTheDeclaredMeasurementBoundary(string lane, string expected)
    {
        var receipt = await ReceiptAsync(lane, dominatedByControl: true);
        Assert.True(QualificationEvidence.CompleteCampaign(receipt));
        Assert.All(receipt.Cases.Take(2), control => Assert.Equal("Control", QualificationEvidence.Assess(receipt, control).Status));
        var assessment = QualificationEvidence.Assess(receipt, receipt.Cases[2]);
        Assert.All(assessment.Sessions, session => Assert.True(session.Qualified));
        Assert.Equal(expected, assessment.Status);
        var report = ComparisonReportCommand.Render(receipt, "test");
        if (expected == "Qualified")
        {
            Assert.Contains("total declared boundary", report);
            Assert.Contains("no ten-times-control floor applies", report);
            Assert.Contains("| Qualified |", report);
        }
        else
        {
            Assert.Contains(assessment.Reasons, reason => reason.Contains("dominated by the invocation control"));
            Assert.Contains("ten times its larger qualified control median", report);
            Assert.DoesNotContain("| Qualified |", report);
        }
    }

    [Theory]
    [InlineData("warm", "missing")]
    [InlineData("compile-run", "missing")]
    [InlineData("compile", "missing")]
    [InlineData("fresh-process", "missing")]
    [InlineData("warm", "noise")]
    [InlineData("compile-run", "noise")]
    [InlineData("compile", "noise")]
    [InlineData("fresh-process", "noise")]
    public async Task EveryLaneStillRequiresBothQualifiedControls(string lane, string defect)
    {
        var receipt = await ReceiptAsync(lane);
        var row = receipt.Cases[2];
        if (defect == "missing")
        {
            receipt.Cases.RemoveAt(0);
            receipt.RequestedCaseIds = receipt.Cases.Select(c => c.Workload.Id).ToArray();
        }
        else receipt.Cases[0].Sessions[0].Sampling.Gates[0].Windows[0].After.NoiseCounters["pgmajfault"] = 1;
        Assert.True(QualificationEvidence.CompleteCampaign(receipt));
        var assessment = QualificationEvidence.Assess(receipt, row);
        Assert.Equal("Unqualified", assessment.Status);
        Assert.Contains(assessment.Reasons, reason => reason.Contains("control", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(receipt, "test"));
    }

    [Fact]
    public async Task MeasuredBatchesBelowTwentyMillisecondsCannotQualify()
    {
        var trace = new SamplingSession();
        await PairedSampler.RunAsync(trace, (_, count, _) => Task.FromResult(Response(count, count * .025)), new FakeMachine(), () => { }, default);
        Assert.True(PairedSampler.Evaluate(trace, true).Qualified);
        var pair = trace.Pairs[0];
        trace.Pairs[0] = pair with { Lython = pair.Lython with { Response = pair.Lython.Response with { ElapsedTicks = 19_000_000 } } };
        RebuildAttempts(trace);
        Assert.False(PairedSampler.Evaluate(trace, true).Qualified);
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("eligibility")]
    [InlineData("manifest")]
    public async Task EarlierPoliciesCannotBeRequalifiedUnderTheNewPolicy(string defect)
    {
        var receipt = await ReceiptAsync("fresh-process");
        if (defect == "policy") receipt.PolicyVersion = 5;
        else if (defect == "eligibility") receipt.EligibilityVersion = 5;
        else
        {
            var policy = JsonNode.Parse(receipt.Policy.GetRawText())!;
            policy["MinimumBatchSeconds"] = .005;
            receipt.Policy = QualificationEvidence.Json(policy);
        }
        Assert.False(QualificationEvidence.CompleteCampaign(receipt));
        Assert.Equal("Unqualified", QualificationEvidence.Assess(receipt, receipt.Cases[2]).Status);
        Assert.DoesNotContain("| Qualified |", ComparisonReportCommand.Render(receipt, "test"));
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
    [InlineData("warm")]
    [InlineData("fresh-process")]
    public async Task ResumeRetainsFinishedMeasurementsAndFailuresAndRestartsPartialRows(string lane)
    {
        var previous = await ReceiptAsync(lane);
        previous.State = "Busy";
        var excluded = previous.Cases[1];
        excluded.State = "Unqualified";
        var failure = new SamplingSession();
        await PairedSampler.RunAsync(failure, (_, count, _) => Task.FromResult(new WorkerResponse(ComparisonProtocol.Version,
            1, excluded.Workload.Id, "BudgetDenied", 0, null, 1_000_000_000, excluded.Workload.SourceSha256,
            excluded.Workload.FixtureSha256, excluded.Workload.ExpectedOutputSha256, null, "Memory budget exceeded.")),
            new FakeMachine(), () => { }, default);
        excluded.Sessions[0].Sampling = failure;
        previous.Cases[2].State = "Running";
        previous.Cases[2].Sessions.RemoveAt(2);
        var next = NextAttempt(previous);
        var restarted = next.Cases[2];

        ComparisonQualificationCommand.RestoreForResume(previous, next);

        Assert.Same(previous.Cases[0], next.Cases[0]);
        Assert.Equal(previous.Started, next.Started);
        Assert.True(ReferenceEquals(excluded, next.Cases[1]), "Finished exclusion proof must be retained.");
        Assert.Equal("BudgetDenied", next.Cases[1].Sessions[0].Sampling.Attempts[0].Batch.Response.Status);
        Assert.Same(excluded.Payload, next.Cases[1].Payload);
        Assert.Same(restarted, Assert.Single(ComparisonQualificationCommand.PendingCases(next)));
        Assert.Empty(restarted.Sessions);
        Assert.Equal(2, previous.Cases[2].Sessions.Count);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("state")]
    [InlineData("schema")]
    [InlineData("protocol")]
    [InlineData("policy-version")]
    [InlineData("eligibility-version")]
    [InlineData("policy-digest")]
    [InlineData("policy-body")]
    [InlineData("catalog-digest")]
    [InlineData("catalog-count")]
    [InlineData("requested-order")]
    [InlineData("case-order")]
    [InlineData("source")]
    [InlineData("files")]
    [InlineData("machine")]
    [InlineData("toolchains")]
    [InlineData("lane")]
    [InlineData("runtime-config")]
    [InlineData("supervisor")]
    public async Task IncompatibleResumeRejectsBeforeCopyingAnyFinishedProof(string defect)
    {
        var previous = await ReceiptAsync();
        previous.State = "Busy";
        var next = NextAttempt(previous);
        var freshRows = next.Cases.ToArray();
        if (defect == "completed") previous.State = "Completed";
        if (defect == "state") previous.State = "unknown";
        if (defect == "schema") previous.SchemaVersion++;
        if (defect == "protocol") previous.ProtocolVersion++;
        if (defect == "policy-version") previous.PolicyVersion++;
        if (defect == "eligibility-version") previous.EligibilityVersion++;
        if (defect == "policy-digest") previous.PolicySha256 = new string('c', 64);
        if (defect == "policy-body")
        {
            var policy = JsonNode.Parse(previous.Policy.GetRawText())!;
            policy["maximumIqrFraction"] = .99;
            previous.Policy = QualificationEvidence.Json(policy);
        }
        if (defect == "catalog-digest") previous.CatalogSha256 = new string('c', 64);
        if (defect == "catalog-count") previous.CatalogCaseCount--;
        if (defect == "requested-order") Array.Reverse(previous.RequestedCaseIds);
        if (defect == "case-order") previous.Cases.Reverse();
        if (defect == "source") previous.Before = previous.Before with { Status = " M runtime.cs" };
        if (defect == "files") previous.Files[0] = previous.Files[0] with { Sha256 = new string('c', 64) };
        if (defect == "machine")
        {
            var machine = JsonNode.Parse(previous.Machine.GetRawText())!;
            machine["cgroup"] = "0::/different-execution-scope";
            previous.Machine = QualificationEvidence.Json(machine);
        }
        if (defect == "toolchains")
        {
            var toolchains = JsonNode.Parse(previous.Toolchains.GetRawText())!;
            toolchains["python"]!["version"] = "3.13.99 test";
            previous.Toolchains = QualificationEvidence.Json(toolchains);
        }
        if (defect == "lane") previous.Lane = "compile";
        if (defect == "runtime-config")
        {
            var config = JsonNode.Parse(previous.RuntimeConfig.GetRawText())!;
            config["runtimeOptions"]!["configProperties"]!["System.Runtime.TieredCompilation"] = false;
            previous.RuntimeConfig = QualificationEvidence.Json(config);
        }
        if (defect == "supervisor") previous.SupervisorOverrides = QualificationEvidence.Json(new { DOTNET_TieredCompilation = "1" });

        Assert.Throws<InvalidDataException>(() => ComparisonQualificationCommand.RestoreForResume(previous, next));
        for (var index = 0; index < freshRows.Length; index++)
        {
            Assert.Same(freshRows[index], next.Cases[index]);
            Assert.Equal("Pending", next.Cases[index].State);
            Assert.Empty(next.Cases[index].Sessions);
        }
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
        return QuietMachineProbe.Evaluate(Enumerable.Range(0, ComparisonPolicy.IdleWindows).Select(i => QuietMachineProbe.Compare(Snapshot(i * 1000, (ulong)i * 100), Snapshot((i + 1) * 1000, (ulong)(i + 1) * 100))));
    }
    internal static MachineSnapshot Snapshot(long timestamp, ulong idle) => new(DateTimeOffset.UnixEpoch, timestamp * 1_000_000, 1_000_000_000,
        [0, 0, 0, idle, 0, 0, 0, 0], new(StringComparer.Ordinal)
        { ["pgmajfault"] = 0, ["pswpin"] = 0, ["pswpout"] = 0, ["memory.some.total_us"] = null,
            ["memory.full.total_us"] = null, ["throttled:/sys/fs/cgroup"] = null }, "/");
    private static WorkerResponse Response(int count, double seconds) => new(1, 1, "test", "Completed", count, (long)Math.Round(seconds * 1_000_000_000),
        1_000_000_000, "source", "fixture", "expected", "expected", null);
    private static List<TimedPair> Pairs(Func<int, (double Left, double Right)> times) => Enumerable.Range(0, ComparisonPolicy.Pairs)
        .Select(i => new TimedPair(i, ComparisonPolicy.LythonFirst(0, i), times(i).Left, times(i).Right)).ToList();
    private sealed class FakeMachine : ISamplingMachine
    {
        private int _read; public bool Busy { get; set; }
        public Action? PreparationHook { get; set; }
        public Task PrepareAsync(CancellationToken cancellationToken)
        {
            PreparationHook?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            _read += 20;
            return Task.CompletedTask;
        }
        public Task SettleAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<QuietEvidence> CheckAsync(CancellationToken cancellationToken)
        {
            var gate = Quiet();
            var offset = _read * 100_000_000L;
            for (var i = 0; i < gate.Windows.Length; i++)
                gate.Windows[i] = gate.Windows[i] with
                {
                    Before = gate.Windows[i].Before with { Timestamp = gate.Windows[i].Before.Timestamp + offset },
                    After = gate.Windows[i].After with { Timestamp = gate.Windows[i].After.Timestamp + offset },
                };
            _read += 10 * ComparisonPolicy.IdleWindows;
            if (Busy) gate.Windows[0].After.Cpu[0] = 100;
            return Task.FromResult(QuietMachineProbe.Evaluate(gate.Windows));
        }
        public MachineSnapshot Read() { _read++; return Snapshot(_read * 100, (ulong)_read * 10); }
    }

    private static QualificationReceipt NextAttempt(QualificationReceipt previous)
    {
        var next = JsonSerializer.Deserialize<QualificationReceipt>(JsonSerializer.Serialize(previous, ComparisonProtocol.JsonOptions),
            ComparisonProtocol.JsonOptions)!;
        next.Id = Guid.NewGuid().ToString("N");
        next.Started = DateTimeOffset.UtcNow;
        next.State = "Running";
        next.Cases = next.Cases.Select(row => new QualificationCase { Workload = row.Workload }).ToList();
        next.After = null;
        next.FilesAfter = null;
        next.MachineAfter = null;
        next.InitialGate = null;
        next.FinalGate = null;
        next.PerformanceQualified = false;
        return next;
    }

    private static async Task<QualificationReceipt> ReceiptAsync(string lane = "warm", bool dominatedByControl = false)
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
        var receipt = new QualificationReceipt { SchemaVersion = 1, ProtocolVersion = 1, PolicyVersion = ComparisonPolicy.Version, EligibilityVersion = ComparisonPolicy.EligibilityVersion,
            Id = Guid.NewGuid().ToString("N"), Started = DateTimeOffset.UtcNow, Updated = DateTimeOffset.UtcNow,
            State = "Completed", Lane = lane, Policy = QualificationEvidence.Json(ComparisonPolicy.Describe()), PolicySha256 = QualificationEvidence.PolicyHash,
            Before = source, After = source, Files = files, FilesAfter = files.ToList(), Machine = machine, MachineAfter = machine,
            Toolchains = tools, CatalogSha256 = hash, InitialGate = Quiet(), FinalGate = Quiet(), CatalogCaseCount = WorkloadCatalog.Create().Count };
        receipt.RuntimeConfig = JsonDocument.Parse("""
        {"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},
        "configProperties":{"System.Reflection.Metadata.MetadataUpdater.IsSupported":false,"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization":false}}}
        """).RootElement.Clone();
        var supervisorOverrides = LythonComparisonWorker.RuntimeOverrideNames.ToDictionary(name => name, _ => (string?)null);
        supervisorOverrides["DOTNET_TieredCompilation"] = "0";
        receipt.SupervisorOverrides = QualificationEvidence.Json(supervisorOverrides);
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
                var session = new QualificationSession { Closed = true, LythonIdentity = left, PythonIdentity = QualificationEvidence.Json(rightNode), Sampling = new() { Index = index, Lane = lane } };
                row.Sessions.Add(session); var request = 0;
                WorkerResponse Make(int count, double? seconds) => new(1, ++request, workload.Id, seconds is null ? "Equivalent" : "Completed", count,
                    seconds is null ? null : (long)Math.Round(seconds.Value * 1e9), 1_000_000_000, workload.SourceSha256, workload.FixtureSha256,
                    workload.ExpectedOutputSha256, lane == "compile" && seconds is not null ? null : workload.ExpectedOutputSha256, null);
                void Verify(string phase) => session.Verification.Add(new(phase, Make(2, null), Make(2, null)));
                Verify("before");
                var controlSeconds = lane == "fresh-process" ? .025 : .00001;
                var perJob = workload.Category == "control" ? controlSeconds
                    : dominatedByControl ? controlSeconds * 2 : lane == "fresh-process" ? .5 : .001;
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
