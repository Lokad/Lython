using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Etlx;
using System.Text.Json;

if (args[0] == "phases")
{
    PhaseDiagnostics.Run(args[1], args[2], args[3]);
    return;
}

if (args[0] == "collect")
{
    // The ready file follows successful session creation, not a guessed delay.
    var providers = new[] {
        new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational),
        new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Verbose,
            0x1 | 0x8 | 0x10 | 0x10000 | 0x20000),
    };
    using var session = new DiagnosticsClient(int.Parse(args[1])).StartEventPipeSession(providers,
        requestRundown: true, circularBufferMB: 64);
    await using var output = File.Create(args[2]);
    var copying = session.EventStream.CopyToAsync(output);
    File.WriteAllText(args[3], DateTimeOffset.UtcNow.ToString("O"));
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    while (!File.Exists(args[4])) await Task.Delay(10, deadline.Token);
    session.Stop();
    await copying;
    return;
}
if (args[0] == "analyze")
{
    using var record = JsonDocument.Parse(File.ReadAllText(args[2]));
    var root = record.RootElement;
    var begin = DateTime.Parse(root.GetProperty("startedUtc").GetString()!).ToUniversalTime();
    var end = DateTime.Parse(root.GetProperty("stoppedUtc").GetString()!).ToUniversalTime();
    var active = root.GetProperty("threadCpuSeconds").EnumerateArray()
        .OrderByDescending(row => row.GetProperty("seconds").GetDouble()).First();
    var tid = int.Parse(active.GetProperty("tid").GetString()!);
    var converted = Path.ChangeExtension(args[1], ".eventpipe.etlx");
    if (!File.Exists(converted)) TraceLog.CreateFromEventPipeDataFile(args[1], converted, new TraceLogOptions());
    using var trace = new TraceLog(converted);
    var counts = new Dictionary<string, int>();
    var samples = new Dictionary<string, int>();
    var leaves = new Dictionary<string, Dictionary<string, int>>();
    var inclusive = new Dictionary<string, Dictionary<string, int>>();
    var allocations = new Dictionary<string, long>();
    var jitNames = new Dictionary<string, int>();
    var suspensions = new Dictionary<string, List<double>>();
    DateTime? suspend = null;
    string? suspendReason = null;
    foreach (var entry in trace.Events)
    {
        var at = entry.TimeStamp.ToUniversalTime();
        if (at < begin || at > end) continue;
        var name = entry.ProviderName + "/" + entry.EventName;
        counts[name] = counts.GetValueOrDefault(name) + 1;
        object? Payload(string key) => entry.PayloadNames.Contains(key) ? entry.PayloadByName(key) : null;
        if (entry.EventName.Contains("SuspendEEStart"))
        { suspend = at; suspendReason = Payload("Reason")?.ToString() ?? "unknown"; }
        if (entry.EventName.Contains("RestartEEStop") && suspend is { } start)
        {
            var reason = suspendReason ?? "unknown";
            if (!suspensions.TryGetValue(reason, out var intervals)) suspensions[reason] = intervals = [];
            intervals.Add((at - start).TotalMilliseconds);
            suspend = null;
        }
        if (entry.EventName.Contains("AllocationTick"))
        {
            var type = Payload("TypeName")?.ToString() ?? "unknown";
            var bytes = Convert.ToInt64(Payload("AllocationAmount64") ?? Payload("AllocationAmount") ?? 0L);
            allocations[type] = allocations.GetValueOrDefault(type) + bytes;
        }
        if (entry.EventName.Contains("JittingStarted"))
        {
            var method = Payload("MethodNamespace") + "." + Payload("MethodName");
            jitNames[method] = jitNames.GetValueOrDefault(method) + 1;
        }
        if (!entry.ProviderName.Contains("SampleProfiler") || entry.ThreadID != tid) continue;
        var sampleType = Payload("Type")?.ToString() ?? "unknown";
        samples[sampleType] = samples.GetValueOrDefault(sampleType) + 1;
        if (!leaves.TryGetValue(sampleType, out var leafCounts)) leaves[sampleType] = leafCounts = [];
        if (!inclusive.TryGetValue(sampleType, out var included)) inclusive[sampleType] = included = [];
        var seen = new HashSet<string>();
        var first = true;
        for (var stack = entry.CallStack(); stack is not null; stack = stack.Caller)
        {
            var method = stack.CodeAddress.FullMethodName;
            if (first) { leafCounts[method] = leafCounts.GetValueOrDefault(method) + 1; first = false; }
            if (seen.Add(method)) included[method] = included.GetValueOrDefault(method) + 1;
        }
    }
    object Top<T>(Dictionary<string, T> values, int count) where T : IComparable<T>
        => values.OrderByDescending(p => p.Value).Take(count).Select(p => new { name = p.Key, value = p.Value }).ToArray();
    var summary = new {
        note = "Only the declared work interval; sampled stack shares are conditional on sample type and active OS thread.",
        activeThread = active, wallMilliseconds = (end - begin).TotalMilliseconds,
        events = Top(counts, 20), sampleTypes = samples,
        leafFramesByType = leaves.ToDictionary(p => p.Key, p => Top(p.Value, 25)),
        inclusiveFramesByType = inclusive.ToDictionary(p => p.Key, p => Top(p.Value, 30)),
        allocationTicksApproximateBytes = Top(allocations, 20),
        compilationStarts = jitNames.Values.Sum(), compilationMethods = Top(jitNames, 20),
        runtimeSuspensionMillisecondsByReason = suspensions,
    };
    File.WriteAllText(args[3], JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    return;
}
throw new ArgumentException("collect <pid> <nettrace> <ready-file> <stop-file>");
