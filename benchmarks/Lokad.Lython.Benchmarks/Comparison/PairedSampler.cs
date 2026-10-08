namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record BatchEvidence(string Phase, int RequestedIterations, WorkerResponse Response)
{
    public double Seconds => Response.ElapsedTicks is { } ticks && Response.ClockFrequency > 0 ? (double)ticks / Response.ClockFrequency : 0;
    public bool Complete => Response.Status == "Completed" && Response.CompletedInvocations == RequestedIterations
        && RequestedIterations is >= 1 and <= ComparisonProtocol.MaximumBatchIterations
        && Seconds > 0 && Seconds <= ComparisonWorkerClient.RequestDeadline.TotalSeconds && double.IsFinite(Seconds);
}
internal sealed record PairEvidence(int Index, bool LythonFirst, QuietEvidence Quiet,
    NoiseInterval Noise, BatchEvidence Lython, BatchEvidence Python);
internal sealed record BatchAttempt(int Sequence, bool Lython, BatchEvidence Batch);
internal sealed class SamplingSession
{
    public int Index { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public List<BatchEvidence> LythonWarmup { get; set; } = [];
    public List<BatchEvidence> PythonWarmup { get; set; } = [];
    public List<BatchEvidence> LythonCalibration { get; set; } = [];
    public List<BatchEvidence> PythonCalibration { get; set; } = [];
    public List<PairEvidence> Pairs { get; set; } = [];
    public List<BatchAttempt> Attempts { get; set; } = [];
    public List<QuietEvidence> Gates { get; set; } = [];
    public string State { get; set; } = "Running";
    public string? Reason { get; set; }
    public SessionStatistics? Statistics { get; set; }
}

internal interface ISamplingMachine
{
    Task SettleAsync(CancellationToken cancellationToken);
    Task<QuietEvidence> CheckAsync(CancellationToken cancellationToken);
    MachineSnapshot Read();
}
internal sealed class LinuxSamplingMachine : ISamplingMachine
{
    public Task SettleAsync(CancellationToken cancellationToken) => Task.Delay(TimeSpan.FromSeconds(ComparisonPolicy.SettleSeconds), cancellationToken);
    public Task<QuietEvidence> CheckAsync(CancellationToken cancellationToken) => QuietMachineProbe.CheckAsync(cancellationToken);
    public MachineSnapshot Read() => QuietMachineProbe.Read();
}

internal static class PairedSampler
{
    public static async Task RunAsync(SamplingSession trace,
        Func<bool, int, CancellationToken, Task<WorkerResponse>> run, ISamplingMachine machine,
        Action checkpoint, CancellationToken cancellationToken, Func<CancellationToken, Task>? afterWarmup = null)
    {
        try
        {
            await WarmAsync(true, trace.LythonWarmup).ConfigureAwait(false);
            await WarmAsync(false, trace.PythonWarmup).ConfigureAwait(false);
            if (afterWarmup is not null) await afterWarmup(cancellationToken).ConfigureAwait(false);
            var leftCount = await CalibrateAsync(true, trace.LythonWarmup, trace.LythonCalibration).ConfigureAwait(false);
            var rightCount = await CalibrateAsync(false, trace.PythonWarmup, trace.PythonCalibration).ConfigureAwait(false);
            for (var pair = 0; pair < ComparisonPolicy.Pairs; pair++)
            {
                await machine.SettleAsync(cancellationToken).ConfigureAwait(false);
                var quiet = await machine.CheckAsync(cancellationToken).ConfigureAwait(false);
                trace.Gates.Add(quiet);
                if (!quiet.Quiet)
                {
                    trace.State = "Busy"; trace.Reason = string.Join(" | ", quiet.Reasons);
                    checkpoint();
                    // Preserve a failed gate even though no timed pair follows.
                    throw new QuietGateException(quiet);
                }
                var before = machine.Read();
                BatchEvidence left, right;
                var leftFirst = ComparisonPolicy.LythonFirst(trace.Index, pair);
                if (leftFirst)
                {
                    left = await BatchAsync(true, leftCount, "measure").ConfigureAwait(false);
                    right = await BatchAsync(false, rightCount, "measure").ConfigureAwait(false);
                }
                else
                {
                    right = await BatchAsync(false, rightCount, "measure").ConfigureAwait(false);
                    left = await BatchAsync(true, leftCount, "measure").ConfigureAwait(false);
                }
                var noise = QuietMachineProbe.Compare(before, machine.Read());
                trace.Pairs.Add(new(pair, leftFirst, quiet, noise, left, right));
                checkpoint();
                if (!noise.Clean)
                {
                    trace.State = "Busy"; trace.Reason = string.Join(" | ", noise.Reasons); checkpoint();
                    throw new NoiseGateException(noise);
                }
            }
            trace.State = "Measured";
            trace.Statistics = Evaluate(trace, evidenceComplete: true);
            checkpoint();
        }
        catch (SamplingExclusion excluded)
        {
            trace.State = "Unqualified"; trace.Reason = excluded.Message;
            trace.Statistics = Evaluate(trace, evidenceComplete: false);
            checkpoint();
        }
        catch (Exception failure) when (failure is not (QuietGateException or NoiseGateException))
        {
            trace.State = cancellationToken.IsCancellationRequested ? "Interrupted" : "Failed";
            trace.Reason = failure.GetType().Name + ": " + failure.Message; checkpoint();
            throw;
        }

        async Task<BatchEvidence> BatchAsync(bool lython, int count, string phase)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await run(lython, count, cancellationToken).ConfigureAwait(false);
            var evidence = new BatchEvidence(phase, count, response);
            trace.Attempts.Add(new(trace.Attempts.Count, lython, evidence));
            if (!evidence.Complete) throw new SamplingExclusion("Incomplete or failed " + phase + " batch: " + response.Status);
            return evidence;
        }
        async Task WarmAsync(bool lython, List<BatchEvidence> records)
        {
            var count = ComparisonPolicy.MinimumWarmupInvocations;
            for (var batch = 0; batch < ComparisonPolicy.MaximumWarmupBatches; batch++)
            {
                var evidence = await BatchAsync(lython, count, "warmup").ConfigureAwait(false);
                records.Add(evidence); checkpoint();
                var seconds = records.Sum(r => r.Seconds);
                if (records.Sum(r => (long)r.Response.CompletedInvocations) >= ComparisonPolicy.MinimumWarmupInvocations
                    && seconds >= ComparisonPolicy.MinimumWarmupSeconds) return;
                count = NextCount(count, evidence.Seconds, ComparisonPolicy.MinimumWarmupSeconds - seconds,
                    allowClampedWarmup: true);
            }
            throw new SamplingExclusion("Warmup reached its finite batch ceiling.");
        }
        async Task<int> CalibrateAsync(bool lython, List<BatchEvidence> warmup, List<BatchEvidence> records)
        {
            var last = warmup[^1];
            var count = NextCount(last.RequestedIterations, last.Seconds, ComparisonPolicy.CalibrationSeconds);
            var confirmed = 0;
            for (var batch = 0; batch < ComparisonPolicy.MaximumCalibrationBatches; batch++)
            {
                var evidence = await BatchAsync(lython, count, "calibrate").ConfigureAwait(false);
                records.Add(evidence); checkpoint();
                if (evidence.Seconds >= ComparisonPolicy.CalibrationSeconds)
                {
                    if (++confirmed == 2) return count;
                }
                else
                {
                    confirmed = 0;
                    count = NextCount(count, evidence.Seconds, ComparisonPolicy.CalibrationSeconds);
                }
            }
            throw new SamplingExclusion("Calibration reached its finite batch ceiling.");
        }
    }

    internal static int NextCount(int previousCount, double seconds, double target, bool allowClampedWarmup = false)
    {
        if (previousCount <= 0 || seconds <= 0 || !double.IsFinite(seconds) || target <= 0 || !double.IsFinite(target))
            throw new SamplingExclusion("Invalid calibration count or duration.");
        var proposed = Math.Ceiling(previousCount * target / seconds * 1.25);
        if (!double.IsFinite(proposed) || proposed > ComparisonProtocol.MaximumBatchIterations)
        {
            if (!allowClampedWarmup) throw new SamplingExclusion("The required batch exceeds its iteration ceiling.");
            return ComparisonProtocol.MaximumBatchIterations;
        }
        return (int)Math.Max(1, proposed);
    }

    public static SessionStatistics Evaluate(SamplingSession trace, bool evidenceComplete)
    {
        var warm = new[] { trace.LythonWarmup, trace.PythonWarmup }.All(records => records.Count <= ComparisonPolicy.MaximumWarmupBatches
            && records.All(r => r.Complete && r.Phase == "warmup" && r.RequestedIterations <= ComparisonProtocol.MaximumBatchIterations)
            && records.Sum(r => (long)r.Response.CompletedInvocations) >= ComparisonPolicy.MinimumWarmupInvocations
            && records.Sum(r => r.Seconds) >= ComparisonPolicy.MinimumWarmupSeconds);
        var calibration = new[] { trace.LythonCalibration, trace.PythonCalibration }.All(records => records.Count >= 2
            && records.Count <= ComparisonPolicy.MaximumCalibrationBatches
            && records.All(r => r.Complete && r.Phase == "calibrate" && r.RequestedIterations <= ComparisonProtocol.MaximumBatchIterations)
            && records[^1].RequestedIterations == records[^2].RequestedIterations
            && records[^1].Seconds >= ComparisonPolicy.CalibrationSeconds && records[^2].Seconds >= ComparisonPolicy.CalibrationSeconds);
        var pairs = trace.Pairs.Select(p => new TimedPair(p.Index, p.LythonFirst,
            p.Lython.Seconds / Math.Max(1, p.Lython.Response.CompletedInvocations),
            p.Python.Seconds / Math.Max(1, p.Python.Response.CompletedInvocations))).ToArray();
        var complete = evidenceComplete && trace.Index is >= 0 and < ComparisonPolicy.Sessions && warm && calibration
            && trace.Gates.Count == ComparisonPolicy.Pairs && trace.Gates.All(g => QuietMachineProbe.Evaluate(g.Windows).Quiet)
            && trace.Pairs.All(p => p.Index is >= 0 and < ComparisonPolicy.Pairs
            && QuietMachineProbe.Evaluate(p.Quiet.Windows).Quiet && QuietMachineProbe.Compare(p.Noise.Before, p.Noise.After).Clean
            && p.Lython.Complete && p.Python.Complete && p.Lython.Seconds >= ComparisonPolicy.MinimumBatchSeconds
            && p.Python.Seconds >= ComparisonPolicy.MinimumBatchSeconds
            && p.Lython.Phase == "measure" && p.Python.Phase == "measure"
            && p.Lython.RequestedIterations == trace.LythonCalibration[^1].RequestedIterations
            && p.Python.RequestedIterations == trace.PythonCalibration[^1].RequestedIterations
            && p.LythonFirst == ComparisonPolicy.LythonFirst(trace.Index, p.Index));
        return ComparisonStatistics.Evaluate(pairs, complete);
    }

    internal sealed class SamplingExclusion(string reason) : Exception(reason);
    internal sealed class QuietGateException(QuietEvidence gate) : Exception("Machine is busy or its accounting cannot qualify.")
    { public QuietEvidence Gate { get; } = gate; }
    internal sealed class NoiseGateException(NoiseInterval interval) : Exception("Observed interference during a measured pair.")
    { public NoiseInterval Interval { get; } = interval; }
}
