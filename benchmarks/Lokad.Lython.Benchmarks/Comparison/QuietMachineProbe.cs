using System.Diagnostics;
using System.Globalization;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record MachineSnapshot(DateTimeOffset Utc, long Timestamp, long ClockFrequency, ulong[] Cpu,
    Dictionary<string, ulong?> NoiseCounters, string? Cgroup);
internal sealed record NoiseInterval(double Seconds, double BusyPercent, double StealPercent,
    bool Clean, string[] Reasons, MachineSnapshot Before, MachineSnapshot After);
internal sealed record QuietEvidence(bool Quiet, string[] Reasons, NoiseInterval[] Windows);

internal static class QuietMachineProbe
{
    public static MachineSnapshot Read()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Qualification currently requires Linux CPU/noise accounting.");
        var counters = new Dictionary<string, ulong?>(StringComparer.Ordinal);
        var vm = ParseKeyValues(ReadText("/proc/vmstat"));
        foreach (var name in new[] { "pgmajfault", "pswpin", "pswpout" })
            counters[name] = vm.TryGetValue(name, out var value) ? value : throw new InvalidDataException("Missing Linux paging counter: " + name);
        var pressure = OptionalText("/proc/pressure/memory");
        counters["memory.some.total_us"] = PressureTotal(pressure, "some");
        counters["memory.full.total_us"] = PressureTotal(pressure, "full");
        var cgroup = ReadText("/proc/self/cgroup").Split('\n').FirstOrDefault(line => line.StartsWith("0::", StringComparison.Ordinal))?[3..].Trim();
        if (cgroup is not null)
        {
            const string root = "/sys/fs/cgroup";
            var path = Path.GetFullPath(Path.Combine(root, cgroup.TrimStart('/')));
            if (path != root && !path.StartsWith(root + "/", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid current cgroup path.");
            while (true)
            {
                var text = OptionalText(Path.Combine(path, "cpu.stat"));
                var values = text is null ? null : ParseKeyValues(text);
                counters["throttled:" + path] = values is not null && values.TryGetValue("nr_throttled", out var count) ? count : null;
                if (path == root) break;
                path = Path.GetDirectoryName(path)!;
            }
        }
        else counters["throttled:unavailable"] = null;
        return new(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(), Stopwatch.Frequency, ParseCpu(ReadText("/proc/stat")), counters, cgroup);
    }

    public static ulong[] ParseCpu(string text)
    {
        var line = text.Split('\n').FirstOrDefault(row => row.StartsWith("cpu ", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Missing aggregate CPU counters.");
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 9) throw new InvalidDataException("CPU accounting must include steal time.");
        // guest/guest_nice are already included in user/nice. Sum only these
        // first eight fields, preserving iowait separately from idle.
        return fields.Skip(1).Take(8).Select(ParseUnsigned).ToArray();
    }

    public static Dictionary<string, ulong> ParseKeyValues(string text)
    {
        var result = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || !result.TryAdd(fields[0], ParseUnsigned(fields[1])))
                throw new InvalidDataException("Malformed or duplicate system counter.");
        }
        return result;
    }

    public static ulong? PressureTotal(string? text, string kind)
    {
        if (text is null) return null;
        var line = text.Split('\n').FirstOrDefault(row => row.StartsWith(kind + " ", StringComparison.Ordinal));
        if (line is null) return null;
        var total = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).SingleOrDefault(part => part.StartsWith("total=", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Missing pressure total.");
        return ParseUnsigned(total[6..]);
    }

    public static NoiseInterval Compare(MachineSnapshot before, MachineSnapshot after)
    {
        var reasons = new List<string>();
        var seconds = before.ClockFrequency > 0 ? ((double)after.Timestamp - before.Timestamp) / before.ClockFrequency : 0;
        double total = 0, idle = 0, steal = 0;
        if (before.ClockFrequency <= 0 || before.ClockFrequency != after.ClockFrequency
            || before.Cpu.Length != 8 || after.Cpu.Length != 8 || seconds <= 0 || !double.IsFinite(seconds))
            reasons.Add("Invalid CPU accounting or monotonic interval.");
        else
        {
            for (var i = 0; i < 8; i++)
            {
                if (after.Cpu[i] < before.Cpu[i]) { reasons.Add("CPU counters regressed."); continue; }
                var delta = (double)(after.Cpu[i] - before.Cpu[i]);
                total += delta;
                if (i is 3 or 4) idle += delta;
                if (i == 7) steal = delta;
            }
            if (total <= 0) reasons.Add("CPU accounting made no progress.");
            if (steal > 0) reasons.Add("Observed hypervisor steal time.");
        }
        if (before.Cgroup != after.Cgroup || before.NoiseCounters.Count != after.NoiseCounters.Count)
            reasons.Add("Noise-accounting identity changed.");
        foreach (var required in new[] { "pgmajfault", "pswpin", "pswpout" })
            if (!before.NoiseCounters.TryGetValue(required, out var counter) || counter is null)
                reasons.Add("Required paging accounting is missing: " + required);
        if (!before.NoiseCounters.ContainsKey("memory.some.total_us") || !before.NoiseCounters.ContainsKey("memory.full.total_us")
            || !before.NoiseCounters.Keys.Any(k => k.StartsWith("throttled:", StringComparison.Ordinal)))
            reasons.Add("Observable/unavailable pressure and throttling accounting must be explicit.");
        foreach (var pair in before.NoiseCounters)
        {
            if (!after.NoiseCounters.TryGetValue(pair.Key, out var value) || pair.Value.HasValue != value.HasValue)
                reasons.Add("Noise-counter availability changed: " + pair.Key);
            else if (pair.Value is { } old && value is { } current)
            {
                if (current < old) reasons.Add("Noise counter regressed: " + pair.Key);
                else if (current > old) reasons.Add("Observed paging/pressure/throttling: " + pair.Key);
            }
        }
        return new(seconds, total > 0 ? 100 * (total - idle) / total : 0,
            total > 0 ? 100 * steal / total : 0, reasons.Count == 0, [.. reasons], before, after);
    }

    public static QuietEvidence Evaluate(IEnumerable<NoiseInterval> observations)
    {
        // Receipts carry raw counters. Never trust a serialized Clean/Quiet flag
        // or a percentage computed with another machine's timer frequency.
        var windows = observations.Select(w => Compare(w.Before, w.After)).ToArray();
        var reasons = windows.SelectMany(w => w.Reasons).Distinct(StringComparer.Ordinal).ToList();
        if (windows.Length != ComparisonPolicy.IdleWindows || windows.Any(w => w.Seconds < ComparisonPolicy.IdleWindowSeconds
            || !double.IsFinite(w.BusyPercent) || w.BusyPercent is < 0 or > 100 || !w.Clean))
            reasons.Add("Five complete clean one-second idle windows are required.");
        if (windows.Length != 0 && (ComparisonStatistics.Quantile(windows.Select(w => w.BusyPercent), .5) > ComparisonPolicy.MaximumMedianBusyPercent
            || windows.Max(w => w.BusyPercent) > ComparisonPolicy.MaximumBusyPercent))
            reasons.Add("Idle CPU exceeds three-percent median or five-percent maximum.");
        return new(reasons.Count == 0, [.. reasons], windows);
    }

    public static async Task<QuietEvidence> CheckAsync(CancellationToken cancellationToken)
    {
        var windows = new List<NoiseInterval>();
        for (var i = 0; i < ComparisonPolicy.IdleWindows; i++)
        {
            var before = Read();
            do
            {
                var remaining = ComparisonPolicy.IdleWindowSeconds - Stopwatch.GetElapsedTime(before.Timestamp).TotalSeconds;
                if (remaining <= 0) break;
                await Task.Delay(TimeSpan.FromSeconds(remaining) + TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
            } while (true);
            windows.Add(Compare(before, Read()));
        }
        return Evaluate(windows);
    }

    public static object Inventory() => new
    {
        os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        processorCount = Environment.ProcessorCount, parentRuntime = Environment.Version.ToString(),
        clockFrequency = Stopwatch.Frequency, clockHighResolution = Stopwatch.IsHighResolution,
        cpuInfo = OptionalText("/proc/cpuinfo"), memInfo = OptionalText("/proc/meminfo"), swaps = OptionalText("/proc/swaps"),
        osRelease = OptionalText("/etc/os-release"), clockSource = OptionalText("/sys/devices/system/clocksource/clocksource0/current_clocksource")?.Trim(),
        virtualizationVendor = OptionalText("/sys/class/dmi/id/sys_vendor")?.Trim(), product = OptionalText("/sys/class/dmi/id/product_name")?.Trim(),
        governor = OptionalText("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor")?.Trim(),
        cgroup = OptionalText("/proc/self/cgroup")?.Trim(),
        hostname = Environment.MachineName, bootId = OptionalText("/proc/sys/kernel/random/boot_id")?.Trim(),
        allowedCpus = OptionalText("/proc/self/status")?.Split('\n').SingleOrDefault(line => line.StartsWith("Cpus_allowed_list:", StringComparison.Ordinal))?.Trim(),
    };

    private static ulong ParseUnsigned(string value) => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
        ? result : throw new InvalidDataException("Invalid unsigned system counter.");
    private static string ReadText(string path)
    {
        using var input = File.OpenRead(path);
        var buffer = new byte[4096];
        using var bytes = new MemoryStream();
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            if (bytes.Length + count > 1024 * 1024) throw new InvalidDataException("System inventory exceeds its one-MiB bound.");
            bytes.Write(buffer, 0, count);
        }
        return ComparisonProtocol.Utf8.GetString(bytes.ToArray());
    }
    private static string? OptionalText(string path)
    {
        try { return ReadText(path); }
        catch (Exception failure) when (failure is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException) { return null; }
    }
}
