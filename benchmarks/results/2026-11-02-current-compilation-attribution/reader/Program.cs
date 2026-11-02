using Microsoft.Diagnostics.Tracing.Etlx;
using System.Text.Json;

// Offline current-compile attribution; select actual compiler stacks rather
// than the busiest thread. Namespace checks exclude benchmark/type references.
using var record = JsonDocument.Parse(File.ReadAllText(args[1]));
var begin = DateTime.Parse(record.RootElement.GetProperty("startedUtc").GetString()!).ToUniversalTime();
var end = DateTime.Parse(record.RootElement.GetProperty("stoppedUtc").GetString()!).ToUniversalTime();
var converted = Path.ChangeExtension(args[0], ".eventpipe.etlx");
if (!File.Exists(converted)) TraceLog.CreateFromEventPipeDataFile(args[0], converted, new TraceLogOptions());
using var trace = new TraceLog(converted);
bool Compiler(string name) => name.StartsWith("Lokad.Lython.LythonEngine.Compile(", StringComparison.Ordinal)
    || name.StartsWith("Lokad.Lython.LythonEngine.CompileCore(", StringComparison.Ordinal);
bool Core(string name)
{
    var method = name.Split('(', 2)[0];
    return method.StartsWith("Lokad.Lython.", StringComparison.Ordinal)
        && !method.StartsWith("Lokad.Lython.Benchmarks.", StringComparison.Ordinal)
        && !method.StartsWith("Lokad.Lython.Utf8Regex.", StringComparison.Ordinal);
}
if (!Compiler("Lokad.Lython.LythonEngine.Compile(class System.String)")
    || Compiler("Lokad.Lython.Benchmarks.Worker.Invoke(class Lokad.Lython.LythonEngine)")
    || Core("Lokad.Lython.Benchmarks.Worker.Invoke(class Lokad.Lython.LythonCompiledScript)"))
    throw new InvalidDataException("Compiler/core classification controls failed.");

var compilerThreads = new Dictionary<int, int>();
foreach (var entry in trace.Events)
{
    var at = entry.TimeStamp.ToUniversalTime();
    if (at < begin || at > end || !entry.EventName.Contains("AllocationTick")) continue;
    for (var stack = entry.CallStack(); stack is not null; stack = stack.Caller)
    {
        if (!Compiler(stack.CodeAddress.FullMethodName)) continue;
        compilerThreads[entry.ThreadID] = compilerThreads.GetValueOrDefault(entry.ThreadID) + 1;
        break;
    }
}
if (compilerThreads.Count == 0) throw new InvalidDataException("No actual compiler allocation stacks resolved.");
var groups = new Dictionary<string, Dictionary<string, long>>();
var missing = new Dictionary<string, long>();
var ticks = 0;
var rooted = 0;
var excludedTicks = 0;
foreach (var entry in trace.Events)
{
    var at = entry.TimeStamp.ToUniversalTime();
    if (at < begin || at > end || !entry.EventName.Contains("AllocationTick")) continue;
    if (!compilerThreads.ContainsKey(entry.ThreadID)) { excludedTicks++; continue; }
    ticks++;
    object? Payload(string key) => entry.PayloadNames.Contains(key) ? entry.PayloadByName(key) : null;
    var type = Payload("TypeName")?.ToString() ?? "unknown";
    var bytes = Convert.ToInt64(Payload("AllocationAmount64") ?? Payload("AllocationAmount") ?? 0L);
    var frames = new List<string>();
    var hasRoot = false;
    for (var stack = entry.CallStack(); stack is not null; stack = stack.Caller)
    {
        var method = stack.CodeAddress.FullMethodName;
        hasRoot |= Compiler(method);
        if (Core(method) && frames.Count < 10) frames.Add(method);
    }
    if (hasRoot) rooted++;
    if (frames.Count == 0) missing[type] = missing.GetValueOrDefault(type) + bytes;
    var path = frames.Count == 0 ? "<no resolved core allocation stack>" : string.Join(" -> ", frames);
    if (!groups.TryGetValue(type, out var owners)) groups[type] = owners = new();
    owners[path] = owners.GetValueOrDefault(path) + bytes;
}
File.WriteAllText(args[2], JsonSerializer.Serialize(new {
    compilerThreads, ticks, compilerRootedTicks = rooted, excludedOtherThreadTicks = excludedTicks,
    note = "Offline approximate allocation origins on actual compiler threads during the fixed window. Protocol allocations can share those threads; no CPU, latency or retained-memory fractions.",
    missing,
    allocations = groups.Select(x => new { type = x.Key, approximateBytes = x.Value.Values.Sum(),
        origins = x.Value.OrderByDescending(y => y.Value).Take(12).ToDictionary() })
        .OrderByDescending(x => x.approximateBytes)
}, new JsonSerializerOptions { WriteIndented = true }));
