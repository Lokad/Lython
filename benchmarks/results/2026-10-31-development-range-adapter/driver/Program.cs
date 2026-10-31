using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Adaptation of the existing per-invocation allocation diagnostic. Each source
// runs in its own process so assembly identity and shared caches cannot cross.
var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var textType = assembly.GetType("Lokad.Lython.Benchmarks.TextScanBenchmarks", true)!;
var textBenchmark = Activator.CreateInstance(textType)!;
textType.GetProperty("RowCount")!.SetValue(textBenchmark, 1000);
textType.GetMethod("Seed")!.Invoke(textBenchmark, null);
const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
var host = textType.GetField("_host", fields)!.GetValue(textBenchmark)!;
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.dll"));
var adapter = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.Utf8Regex.dll"));
var provider = adapter.GetType("Lokad.Lython.Utf8Regex.Utf8RegexProvider", true)!.GetProperty("Instance")!.GetValue(null)!;
var engineType = core.GetType("Lokad.Lython.LythonEngine", true)!;
var engine = Activator.CreateInstance(engineType, [provider])!;
var compile = engineType.GetMethod("Compile", [typeof(string)])!;
var cases = JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(args[2]))!;
var observations = new List<object>();
try
{
    foreach (var test in cases.Append(new Case("text-control", "", "12000")))
    {
        var control = test.Name == "text-control";
        var script = control ? textType.GetField("_discardLines", fields)!.GetValue(textBenchmark)! : compile.Invoke(engine, [test.Source])!;
        try
        {
            if (!(bool)script.GetType().GetProperty("IsValid")!.GetValue(script)!)
                throw new InvalidDataException("Invalid diagnostic source: " + test.Name);
            var run = script.GetType().GetMethods().Single(m => m.Name == "RunAsync" && m.GetParameters().Length == 1);
            async Task Once()
            {
                var pending = (Task)run.Invoke(script, [host])!;
                await pending;
                var result = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                using ((IDisposable)result)
                    if (!(bool)result.GetType().GetProperty("Success")!.GetValue(result)! ||
                        result.GetType().GetProperty("ReturnValue")!.GetValue(result)?.ToString() != test.Expected ||
                        result.GetType().GetProperty("StandardOutput")!.GetValue(result)?.ToString() != "")
                        throw new InvalidDataException("Diagnostic golden failed: " + test.Name + ": " + result.GetType().GetProperty("Failure")!.GetValue(result));
            }
            for (var i = 0; i < 4; i++) await Once();
            var times = new List<double>(); var allocations = new List<double>();
            for (var round = 0; round < 7; round++)
            {
                var before = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) await Once();
                timer.Stop(); times.Add(timer.Elapsed.TotalMilliseconds / 5);
                allocations.Add((GC.GetTotalAllocatedBytes(true) - before) / 5.0);
            }
            observations.Add(new { test.Name, test.Expected, milliseconds = times, allocatedBytes = allocations,
                medianMilliseconds = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3) });
        }
        finally { if (!control) ((IDisposable)script).Dispose(); }
    }
    var identities = Directory.EnumerateFiles(directory, "Lokad.*.dll").ToDictionary(p => Path.GetFileName(p)!,
        p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    Console.WriteLine(JsonSerializer.Serialize(new { revision = args[1],
        boundary = "Warm public RunAsync, return/stdout golden and result disposal; compilation/startup excluded; common reflection overhead", warmups = 4, rounds = 7, batch = 5,
        observations, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        processors = Environment.ProcessorCount, identities, performanceQualified = false }));
}
finally { textType.GetMethod("Cleanup")!.Invoke(textBenchmark, null); }
record Case(string Name, string Source, string Expected);
