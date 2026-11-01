using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Existing complete-compilation and complete-job diagnostics combined for one fixed comparison.
var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var benchmark = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.dll"));
var engine = Activator.CreateInstance(core.GetType("Lokad.Lython.LythonEngine", true)!)!;
var budgetType = core.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
var budget = Activator.CreateInstance(budgetType, [null])!;
var balance = budgetType.GetProperty("ReservedBytes")!;
var compile = engine.GetType().GetMethod("Compile", [typeof(string), core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
var fixtureType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileFixture", true)!;
var hostType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileHost", true)!;
var validateDirectory = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.OwnedWorkerProcess", true)!
    .GetMethod("ValidateFixtureDirectory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().Where(c =>
    c.GetProperty("id").GetString() is "scripts.csv-summary-helpers.large" or "scripts.text-summary-helpers.large" or "scripts.config-edit.large").ToArray();
if (cases.Length != 3) throw new InvalidDataException("Missing frozen cases.");
var observations = new List<object>();
using var process = Process.GetCurrentProcess();
foreach (var item in cases)
{
    var id = item.GetProperty("id").GetString();
    var source = item.GetProperty("source").GetString()!;
    object Compile()
    {
        var script = compile.Invoke(engine, [source, budget])!;
        if (!(bool)script.GetType().GetProperty("IsValid")!.GetValue(script)!)
        { ((IDisposable)script).Dispose(); throw new InvalidDataException("Compilation golden failed: " + id); }
        return script;
    }
    var fixture = fixtureType.GetMethod("Parse")!.Invoke(null, [item.GetProperty("fixtureJson").GetString()])!;
    var owned = Path.Combine(Path.GetTempPath(), "lython-comparison-files-" + Guid.NewGuid().ToString("N"));
    var host = Activator.CreateInstance(hostType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
        null, [fixture, owned], null)!;
    var warm = Compile();
    var run = warm.GetType().GetMethods().Single(m => m.Name == "Run" && m.GetParameters().Length == 1);
    try
    {
        void Invoke(object script)
        {
            hostType.GetMethod("Reset")!.Invoke(host, null);
            using var result = (IDisposable)run.Invoke(script, [host])!;
            if (!(bool)result.GetType().GetProperty("Success")!.GetValue(result)! ||
                (string)result.GetType().GetProperty("StandardOutput")!.GetValue(result)! != item.GetProperty("expectedOutput").GetString() ||
                (string)result.GetType().GetProperty("StandardError")!.GetValue(result)! != "")
                throw new InvalidDataException("Complete stdout golden failed: " + id);
            var files = hostType.GetMethod("ReadOutputs")!.Invoke(host, null)!;
            if ((string)fixtureType.GetMethod("Digest")!.Invoke(null, [files])! != item.GetProperty("expectedFilesSha256").GetString())
                throw new InvalidDataException("Complete file golden failed: " + id);
        }
        void CompileOnce() { using var script = (IDisposable)Compile(); }
        void FreshOnce() { using var script = (IDisposable)Compile(); Invoke(script); }
        for (var i = 0; i < 4; i++) { CompileOnce(); Invoke(warm); FreshOnce(); }
        var baseline = (long)balance.GetValue(budget)!;
        object Measure(string lane, Action once, int batch)
        {
            var times = new List<double>(); var allocations = new List<double>(); var cpu = new List<double>();
            for (var round = 0; round < 7; round++)
            {
                var before = GC.GetTotalAllocatedBytes(true);
                process.Refresh(); var beforeCpu = process.TotalProcessorTime;
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < batch; i++) once();
                timer.Stop(); process.Refresh(); cpu.Add((process.TotalProcessorTime - beforeCpu).TotalMilliseconds);
                times.Add(timer.Elapsed.TotalMilliseconds / batch);
                allocations.Add((GC.GetTotalAllocatedBytes(true) - before) / (double)batch);
                if ((long)balance.GetValue(budget)! != baseline) throw new InvalidDataException("Ownership did not return to baseline: " + lane);
            }
            return new { lane, batch, rounds = 7, milliseconds = times, allocatedBytes = allocations, processCpuMilliseconds = cpu,
                medianMilliseconds = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3),
                totalCpuMillisecondsPerJob = cpu.Sum() / (7 * batch) };
        }
        var compilation = Measure("compile-dispose", CompileOnce, 1024);
        var fresh = Measure("compile-run-dispose", FreshOnce, 5);
        var execution = Measure("warm-run-dispose", () => Invoke(warm), 5);
        observations.Add(new { id, sourceBytes = System.Text.Encoding.UTF8.GetByteCount(source), baseline, compilation, fresh, execution });
    }
    finally
    {
        ((IDisposable)warm).Dispose();
        if ((long)balance.GetValue(budget)! != 0) throw new InvalidDataException("Compiled owner did not end.");
        validateDirectory.Invoke(null, [owned]);
        if (Directory.Exists(owned)) Directory.Delete(owned, recursive: true);
    }
}
var identities = Directory.EnumerateFiles(directory, "Lokad.*.dll").ToDictionary(p => Path.GetFileName(p)!,
    p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
Console.WriteLine(JsonSerializer.Serialize(new { revision = args[1],
    catalogSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))),
    boundary = "Public Compile+disposal separately from fresh complete compile/run and warm Run; existing fixture reset/staging, host I/O, full stdout/file validation and result disposal in execution lanes; startup excluded; common reflection overhead", warmups = 4,
    observations, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    processors = Environment.ProcessorCount, identities, elapsedQualified = false }));
