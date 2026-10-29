using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonMicroTests
{
    private static readonly ComparisonWorkload Workload = WorkloadCatalog.Create().Single(w => w.Id == "loops.integer.large");

    [Theory]
    [InlineData("Mismatch")]
    [InlineData("BudgetDenied")]
    [InlineData("partial")]
    [InlineData("hash")]
    [InlineData("clock")]
    [InlineData("files")]
    public async Task UnusableWarmupRemainsRecordedAndCannotProduceMeasurements(string defect)
    {
        var attempts = new List<ComparisonMicroCommand.Attempt>();
        var saved = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => ComparisonMicroCommand.SampleAsync(Workload,
            (_, operation, count, _) => Task.FromResult(operation == "verify" ? Reply(count, null) : defect switch
            {
                "partial" => Reply(count - 1, 100),
                "hash" => Reply(count, 100) with { ActualOutputSha256 = new string('0', 64) },
                "clock" => Reply(count, 100) with { ClockFrequency = 0 },
                "files" => Reply(count, 100) with { ActualFilesSha256 = new string('0', 64) },
                _ => Reply(count, null) with { Status = defect },
            }), attempts, () => saved++, default));
        Assert.Equal(2, saved);
        Assert.Equal(new[] { "verify", "warmup" }, attempts.Select(a => a.Phase));
        Assert.Throws<InvalidDataException>(() => ComparisonMicroCommand.Summarize(attempts));
    }

    [Fact]
    public void SummaryUsesPerInvocationClocksAndInterpolatedQuartiles()
    {
        var attempts = ComparisonMicroCommand.Engines.SelectMany(engine => Enumerable.Range(1, 7)
            .Select(n => new ComparisonMicroCommand.Attempt(engine, "measure", 10, Reply(10, n * 10)))).ToArray();
        foreach (var row in ComparisonMicroCommand.Summarize(attempts))
        {
            Assert.Equal(4_000, row.MedianMicroseconds);
            Assert.Equal(.75, row.IqrFraction, precision: 12);
        }
        Assert.Throws<InvalidDataException>(() => ComparisonMicroCommand.Summarize(attempts[..^1]));
        attempts[0] = attempts[0] with { RequestedIterations = 0 };
        Assert.Throws<InvalidDataException>(() => ComparisonMicroCommand.Summarize(attempts));
    }

    [Fact]
    public async Task CancellationDuringWarmupLeavesNoNumericalSummary()
    {
        using var cancelled = new CancellationTokenSource();
        var attempts = new List<ComparisonMicroCommand.Attempt>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ComparisonMicroCommand.SampleAsync(Workload,
            (_, operation, count, token) =>
            {
                if (operation == "verify") return Task.FromResult(Reply(count, null));
                cancelled.Cancel();
                return Task.FromCanceled<WorkerResponse>(token);
            }, attempts, () => { }, cancelled.Token));
        Assert.Single(attempts);
        Assert.Throws<InvalidDataException>(() => ComparisonMicroCommand.Summarize(attempts));
    }

    private static WorkerResponse Reply(int count, long? ticks) => new(ComparisonProtocol.Version, 1, Workload.Id,
        ticks is null ? "Equivalent" : "Completed", count, ticks, 1_000,
        Workload.SourceSha256, Workload.FixtureSha256, Workload.ExpectedOutputSha256, Workload.ExpectedOutputSha256, null);

    [Fact]
    public async Task CompilationTimingRequiresUntimedGoldenExecutionAndNoTimedOutput()
    {
        var workload = WorkloadCatalog.CreateRepresentative().Single(w => w.Id == "scripts.csv-summary.small");
        var attempts = new List<ComparisonMicroCommand.Attempt>();
        await ComparisonMicroCommand.SampleAsync(workload, (_, operation, count, _) => Task.FromResult(
            new WorkerResponse(ComparisonProtocol.Version, 1, workload.Id,
                operation == "verify" ? "Equivalent" : "Completed", count, operation == "verify" ? null : count * 1000L, 1000,
                workload.SourceSha256, workload.FixtureSha256, workload.ExpectedOutputSha256,
                operation == "verify" ? workload.ExpectedOutputSha256 : null, null,
                operation == "verify" ? workload.ExpectedFilesSha256 : null)), attempts, () => { }, default,
            compilationOnly: true);
        Assert.All(ComparisonMicroCommand.Summarize(attempts), row => Assert.Equal(1_000_000, row.MedianMicroseconds));
        Assert.All(attempts.Where(a => a.Phase == "measure"), a =>
        {
            Assert.Null(a.Response.ActualOutputSha256);
            Assert.Null(a.Response.ActualFilesSha256);
        });
    }

    [Theory]
    [InlineData("verify-output")]
    [InlineData("verify-files")]
    [InlineData("timed-output")]
    public async Task CompilationCannotHideFailedVerificationOrExecuteInsideTiming(string defect)
    {
        var attempts = new List<ComparisonMicroCommand.Attempt>();
        await Assert.ThrowsAsync<InvalidDataException>(() => ComparisonMicroCommand.SampleAsync(Workload,
            (_, operation, count, _) => Task.FromResult(operation == "verify" ? defect switch
            {
                "verify-output" => Reply(count, null) with { ActualOutputSha256 = null },
                "verify-files" => Reply(count, null) with { ActualFilesSha256 = new string('0', 64) },
                _ => Reply(count, null),
            } : Reply(count, count * 1000L)), attempts, () => { }, default, compilationOnly: true));
        Assert.DoesNotContain(attempts, a => a.Phase == "measure");
    }
}
