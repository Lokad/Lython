using Microsoft.Diagnostics.Tracing.Etlx;
using System.Text.Json;
using var record = JsonDocument.Parse(File.ReadAllText(args[1]));
var begin = DateTime.Parse(record.RootElement.GetProperty("startedUtc").GetString()!).ToUniversalTime();
var end = DateTime.Parse(record.RootElement.GetProperty("stoppedUtc").GetString()!).ToUniversalTime();
using var trace = new TraceLog(args[0]);
var events = trace.Events.Where(e => e.TimeStamp.ToUniversalTime() >= begin && e.TimeStamp.ToUniversalTime() <= end)
    .GroupBy(e => e.ProviderName + "/" + e.EventName)
    .OrderByDescending(g => g.Count()).Select(g => new { name = g.Key, count = g.Count() }).ToArray();
File.WriteAllText(args[2], JsonSerializer.Serialize(new { source = "Original B80 trace; no new guest run", begin, end, events }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(events.Take(15)));

var types = new Dictionary<string, int>();
var rootedTypes = new Dictionary<string, int>();
var stages = new Dictionary<string, int>();
var self = new Dictionary<string, int>();
var threads = new Dictionary<int, int>();
var examples = new List<object>();
var unrooted = 0;
foreach (var entry in trace.Events)
{
    if (entry.TimeStamp.ToUniversalTime() < begin || entry.TimeStamp.ToUniversalTime() > end ||
        entry.ProviderName != "Microsoft-DotNETCore-SampleProfiler" || entry.EventName != "Thread/Sample") continue;
    var payload = string.Join(";", entry.PayloadNames.Select((name, i) => name + "=" + entry.PayloadValue(i)));
    types[payload] = types.GetValueOrDefault(payload) + 1;
    var frames = new List<string>();
    for (var stack = entry.CallStack(); stack is not null; stack = stack.Caller) frames.Add(stack.CodeAddress.FullMethodName);
    if (!frames.Any(n => n.StartsWith("Lokad.Lython.LythonEngine.CompileCore(", StringComparison.Ordinal))) { unrooted++; continue; }
    rootedTypes[payload] = rootedTypes.GetValueOrDefault(payload) + 1;
    threads[entry.ThreadID] = threads.GetValueOrDefault(entry.ThreadID) + 1;
    var leaf = frames.FirstOrDefault() ?? "<missing>";
    self[leaf] = self.GetValueOrDefault(leaf) + 1;
    var stage = new[] { "Parser.Parse(", "PythonTokenization.Read(", "SyntaxValidityDiagnostics.Analyze(", "PostponedAnnotationText.Attach(",
        "ImplicitSuperSyntaxFacts.Prepare(", "AnnotationDiagnostics.Analyze(", "StaticAbstractInterpreter.Analyze(",
        "StaticScopeDirectiveDiagnostics.Analyze(", "StaticNameBindingDiagnostics.Analyze(", "LoweredScript.Lower(", "ExecutableScript.Compile(" }
        .FirstOrDefault(key => frames.Any(n => n.StartsWith("Lokad.Lython.Frontend." + key, StringComparison.Ordinal))) ?? "other compiler work";
    stages[stage] = stages.GetValueOrDefault(stage) + 1;
    if (examples.Count < 3) examples.Add(new { payload, frames });
}
File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[2])!, "stack-inventory.json"), JsonSerializer.Serialize(new { types, compileCoreRootedTypes = rootedTypes,
    compileCoreRootedThreads = threads, unrootedSamples = unrooted, allRootedStages = stages,
    allRootedLeafSamples = self.OrderByDescending(x => x.Value).ToDictionary(), examples,
    note = "Original intrusive allocation trace: sample payload/state not yet classified; stack counts are NOT CPU/elapsed fractions." }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { types, rootedTypes, threads, stages, top = self.OrderByDescending(x => x.Value).Take(8) }));
