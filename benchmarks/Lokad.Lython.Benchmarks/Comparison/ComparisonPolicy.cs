namespace Lokad.Lython.Benchmarks.Comparison;

// Frozen before qualified timing. Changing any constant requires a policy
// version bump; a failed campaign is not a reason to weaken its eligibility.
internal static class ComparisonPolicy
{
    public const int Version = 3;
    public const int EligibilityVersion = 3;
    public const int Sessions = 3;
    public const int Pairs = 7;
    public const int IdleWindows = 1;
    public const double IdleWindowSeconds = .5;
    public const double SettleSeconds = .05;
    public const double MaximumMedianBusyPercent = 3;
    public const double MaximumBusyPercent = 5;
    public const int MinimumWarmupInvocations = 8;
    public const double MinimumWarmupSeconds = .1;
    public const int MaximumWarmupBatches = 12;
    public const int MaximumCalibrationBatches = 8;
    public const double CalibrationSeconds = .010;
    public const double MinimumBatchSeconds = .005;
    public const double MaximumIqrFraction = .10;
    public const double MaximumOrderFactor = 1.10;
    public const double MaximumIntervalFactor = 1.15;
    public const double MaximumSessionFactor = 1.10;
    public const double MaximumControlFraction = .10;
    public const int BootstrapResamples = 10_000;
    public const uint BootstrapSeed = 1729;
    public const int CaseDeadlineSeconds = 45;
    public const int CampaignDeadlineSeconds = 600;
    public const int CleanupReserveSeconds = 10;

    // Chosen by workload coverage before timing, never by observed ratios.
    // The full 131-case correctness catalog remains available separately.
    public static readonly string[] QuickCaseIds =
    [
        "control.empty.control", "control.tiny.control",
        "loops.integer.medium", "loops.integer.large", "calls.keyword.medium",
        "lists.stable-sort.medium", "dicts.tuple-key-update.medium", "generators.drain.medium",
        "strings.scan-supplementary.medium", "strings.pipeline-ascii.medium",
        "json.transform-roundtrip.medium", "csv.retain.medium", "xml.parse-select.medium",
        "compression.zlib.medium",
    ];

    public static object Describe() => new
    {
        Version, EligibilityVersion, Sessions, Pairs, IdleWindows, IdleWindowSeconds, SettleSeconds,
        MaximumMedianBusyPercent, MaximumBusyPercent, MinimumWarmupInvocations, MinimumWarmupSeconds,
        MaximumWarmupBatches, MaximumCalibrationBatches, CalibrationSeconds, MinimumBatchSeconds,
        MaximumIqrFraction, MaximumOrderFactor, MaximumIntervalFactor, MaximumSessionFactor, MaximumControlFraction,
        BootstrapResamples, BootstrapSeed, CaseDeadlineSeconds, CampaignDeadlineSeconds, CleanupReserveSeconds,
        defaultCaseIds = QuickCaseIds,
        budget = "600 seconds per lane including setup and all retries; ten seconds reserved for cleanup",
        interference = "exclude the affected case without rerunning it; initial/final noise stops the attempt",
        supervisor = "DOTNET_TieredCompilation=0 for the supervisor alone; removed before every persistent/once worker launch",
        maximumBatchIterations = ComparisonProtocol.MaximumBatchIterations,
        maximumReceiptBytes = ComparisonReportCommand.MaximumReceiptBytes,
        requestDeadlineSeconds = ComparisonWorkerClient.RequestDeadline.TotalSeconds,
        prng = "xorshift32/rejection-index-v1", quantile = "linear interpolation of sorted positions (n-1)*p",
        ratio = "exp(median(log(CPython per-job / Lython per-job))) within each session",
        aggregateInterval = "none; session observations are not pooled",
        crossSession = "largest/smallest qualified session median ratio <= 1.10",
        noise = "no observed steal, paging, memory-pressure or observable cgroup-throttle increments",
        controls = "both empty and tiny controls precede cases; each lane's case median must exceed ten times its larger control median",
    };

    public static bool LythonFirst(int session, int pair)
    {
        if (session is < 0 or >= Sessions || pair is < 0 or >= Pairs) throw new ArgumentOutOfRangeException();
        // Odd pair counts differ by one; reverse the starting engine in the
        // next independent session. Record the actual 4/3 order counts.
        return ((session + pair) & 1) == 0;
    }
}

internal sealed record TimedPair(int Index, bool LythonFirst, double LythonSeconds, double PythonSeconds);
internal sealed record SessionStatistics(bool Qualified, string[] Reasons, double LythonMedianSeconds,
    double PythonMedianSeconds, double LythonIqrFraction, double PythonIqrFraction,
    double? Ratio, double? RatioLower, double? RatioUpper, double OrderFactor, string Winner);

internal static class ComparisonStatistics
{
    public static SessionStatistics Evaluate(IReadOnlyList<TimedPair> pairs, bool evidenceComplete)
    {
        var reasons = new List<string>();
        if (!evidenceComplete) reasons.Add("Incomplete semantic/provenance/warmup/calibration/quietness evidence.");
        if (pairs.Count != ComparisonPolicy.Pairs || pairs.Select(p => p.Index).Distinct().Count() != pairs.Count
            || pairs.Any(p => p.Index < 0 || p.Index >= ComparisonPolicy.Pairs))
            reasons.Add($"Expected {ComparisonPolicy.Pairs} distinct indexed pairs.");
        if (pairs.Count == 0 || pairs.Any(p => !Positive(p.LythonSeconds) || !Positive(p.PythonSeconds)))
            return new(false, [.. reasons, "Invalid or missing per-invocation times."], 0, 0, 0, 0, null, null, null, 0, "Unqualified");
        var left = pairs.Select(p => p.LythonSeconds).ToArray();
        var right = pairs.Select(p => p.PythonSeconds).ToArray();
        var leftMedian = Quantile(left, .5);
        var rightMedian = Quantile(right, .5);
        var leftIqr = (Quantile(left, .75) - Quantile(left, .25)) / leftMedian;
        var rightIqr = (Quantile(right, .75) - Quantile(right, .25)) / rightMedian;
        if (leftIqr > ComparisonPolicy.MaximumIqrFraction || rightIqr > ComparisonPolicy.MaximumIqrFraction)
            reasons.Add("Lane IQR/median exceeds ten percent.");
        var logs = pairs.Select(p => Math.Log(p.PythonSeconds) - Math.Log(p.LythonSeconds)).ToArray();
        var ab = pairs.Where(p => p.LythonFirst).Select(p => Math.Log(p.PythonSeconds) - Math.Log(p.LythonSeconds)).ToArray();
        var ba = pairs.Where(p => !p.LythonFirst).Select(p => Math.Log(p.PythonSeconds) - Math.Log(p.LythonSeconds)).ToArray();
        var order = ab.Length == 0 || ba.Length == 0 ? double.PositiveInfinity
            : Math.Exp(Math.Abs(Quantile(ab, .5) - Quantile(ba, .5)));
        if (Math.Abs(ab.Length - ba.Length) != 1) reasons.Add("Pair order is not alternating near-balanced AB/BA.");
        if (pairs.OrderBy(p => p.Index).Zip(pairs.OrderBy(p => p.Index).Skip(1)).Any(p => p.First.LythonFirst == p.Second.LythonFirst))
            reasons.Add("Pair order does not alternate.");
        if (order > ComparisonPolicy.MaximumOrderFactor) reasons.Add("AB/BA order changes the ratio by more than ten percent.");
        var bootstrap = new double[ComparisonPolicy.BootstrapResamples];
        var sample = new double[logs.Length];
        var generator = new BootstrapGenerator(ComparisonPolicy.BootstrapSeed);
        for (var draw = 0; draw < bootstrap.Length; draw++)
        {
            for (var i = 0; i < sample.Length; i++) sample[i] = logs[generator.Index(logs.Length)];
            Array.Sort(sample);
            bootstrap[draw] = SortedQuantile(sample, .5);
        }
        var lowLog = Quantile(bootstrap, .025);
        var highLog = Quantile(bootstrap, .975);
        if (Math.Exp(highLog - lowLog) > ComparisonPolicy.MaximumIntervalFactor)
            reasons.Add("Paired ratio interval spans more than fifteen percent.");
        var qualified = reasons.Count == 0;
        var low = Math.Exp(lowLog);
        var high = Math.Exp(highLog);
        var ratio = Math.Exp(Quantile(logs, .5));
        if (!Positive(ratio) || !Positive(low) || !Positive(high))
        { qualified = false; reasons.Add("Ratio or interval is not finite and positive."); }
        return new(qualified, [.. reasons], leftMedian, rightMedian, leftIqr, rightIqr,
            qualified ? ratio : null, qualified ? low : null, qualified ? high : null, double.IsFinite(order) ? order : 0,
            !qualified ? "Unqualified" : low > 1 ? "Lython" : high < 1 ? "CPython" : "No winner");
    }

    public static double Quantile(IEnumerable<double> values, double probability)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0 || probability is < 0 or > 1 || !double.IsFinite(probability)) throw new ArgumentException("Invalid quantile.");
        return SortedQuantile(sorted, probability);
    }

    private static double SortedQuantile(double[] values, double probability)
    {
        var position = (values.Length - 1) * probability;
        var lower = (int)position;
        return values[lower] + (values[(int)Math.Ceiling(position)] - values[lower]) * (position - lower);
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;

    // Explicit xorshift32 and rejection sampling, not System.Random. The
    // algorithm/seed, intact-pair resampling and quantiles are versioned policy.
    private struct BootstrapGenerator(uint state)
    {
        public int Index(int count)
        {
            var bound = (uint)count;
            var threshold = unchecked(0u - bound) % bound;
            uint value;
            do
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                value = state;
            } while (value < threshold);
            return (int)(value % bound);
        }
    }
}
