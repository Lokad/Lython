using Microsoft.Diagnostics.Tracing.Etlx;
using System.Text.Json;

using var record = JsonDocument.Parse(File.ReadAllText(args[1]));
var root = record.RootElement;
var begin = DateTime.Parse(root.GetProperty("startedUtc").GetString()!).ToUniversalTime();
var end = DateTime.Parse(root.GetProperty("stoppedUtc").GetString()!).ToUniversalTime();
var tid = int.Parse(root.GetProperty("guestTid").GetString()!);
var converted = Path.ChangeExtension(args[0], ".eventpipe.etlx");
if (!File.Exists(converted)) TraceLog.CreateFromEventPipeDataFile(args[0], converted, new TraceLogOptions());
using var trace = new TraceLog(converted);
var groups = new Dictionary<string, Dictionary<string, long>>();
var missing = new Dictionary<string, long>();
var ticks = 0;
var threadTotals = new Dictionary<int, long>();
foreach (var entry in trace.Events)
{
    var at = entry.TimeStamp.ToUniversalTime();
    if (at < begin || at > end || !entry.EventName.Contains("AllocationTick")) continue;
    object? Payload(string key) => entry.PayloadNames.Contains(key) ? entry.PayloadByName(key) : null;
    var type = Payload("TypeName")?.ToString() ?? "unknown";
    var bytes = Convert.ToInt64(Payload("AllocationAmount64") ?? Payload("AllocationAmount") ?? 0L);
    threadTotals[entry.ThreadID] = threadTotals.GetValueOrDefault(entry.ThreadID) + bytes;
    if (entry.ThreadID != tid) continue;
    ticks++;
    var frames = new List<string>();
    for (var stack = entry.CallStack(); stack is not null; stack = stack.Caller)
    {
        var method = stack.CodeAddress.FullMethodName;
        if (method.Contains("Lokad.Lython")) frames.Add(method);
    }
    if (frames.Count == 0) missing[type] = missing.GetValueOrDefault(type) + bytes;
    var path = frames.Count == 0 ? "<no resolved Lython allocation stack>" : string.Join(" -> ", frames);
    if (!groups.TryGetValue(type, out var owners)) groups[type] = owners = new();
    owners[path] = owners.GetValueOrDefault(path) + bytes;
}
File.WriteAllText(args[2], JsonSerializer.Serialize(new { thread = tid, ticks, threadTotals, note = "Offline sampled allocation origins, approximate bytes, not retained storage or CPU shares; missing stacks remain visible.", missing, allocations = groups.Select(x => new { type = x.Key, approximateBytes = x.Value.Values.Sum(), origins = x.Value.OrderByDescending(y => y.Value).ToDictionary() }).OrderByDescending(x => x.approximateBytes) }, new JsonSerializerOptions { WriteIndented = true }));
