using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Existing allocation diagnostic adapted to the fixed complete dictionary-heavy scripts.
var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var benchmark = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.dll"));
var engineType = core.GetType("Lokad.Lython.LythonEngine", true)!;
var engine = Activator.CreateInstance(engineType)!;
var budgetType = core.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
var budget = Activator.CreateInstance(budgetType, [null])!;
var balance = budgetType.GetProperty("ReservedBytes")!;
var compile = engineType.GetMethod("Compile", [typeof(string), core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
var fixtureType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileFixture", true)!;
var hostType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileHost", true)!;
var validateDirectory = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.OwnedWorkerProcess", true)!
    .GetMethod("ValidateFixtureDirectory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().Where(c =>
    c.GetProperty("id").GetString() is "scripts.csv-summary-helpers.large" or "scripts.text-summary-helpers.large" or "scripts.config-edit.large").ToArray();
if (cases.Length != 3) throw new InvalidDataException("Missing frozen CSV/text/config cases.");
var observations = new List<object>();
foreach (var item in cases)
{
    var id = item.GetProperty("id").GetString();
    var fixture = fixtureType.GetMethod("Parse")!.Invoke(null, [item.GetProperty("fixtureJson").GetString()])!;
    var owned = Path.Combine(Path.GetTempPath(), "lython-comparison-files-" + Guid.NewGuid().ToString("N"));
    var host = Activator.CreateInstance(hostType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
        null, [fixture, owned], null)!;
    var script = compile.Invoke(engine, [item.GetProperty("source").GetString(), budget])!;
    try
    {
        if (!(bool)script.GetType().GetProperty("IsValid")!.GetValue(script)!) throw new InvalidDataException("Invalid script: " + id);
        var run = script.GetType().GetMethods().Single(m => m.Name == "Run" && m.GetParameters().Length == 1);
        var compiledBaseline = (long)balance.GetValue(budget)!;
        long expectedBalance = -1;
        void Once()
        {
            hostType.GetMethod("Reset")!.Invoke(host, null);
            using ((IDisposable)(run.Invoke(script, [host])! is { } result ? result : throw new Exception("Missing result")))
            {
                if (!(bool)result.GetType().GetProperty("Success")!.GetValue(result)! ||
                    (string)result.GetType().GetProperty("StandardOutput")!.GetValue(result)! != item.GetProperty("expectedOutput").GetString() ||
                    (string)result.GetType().GetProperty("StandardError")!.GetValue(result)! != "")
                    throw new InvalidDataException("Complete JSON golden failed: " + id);
                var files = hostType.GetMethod("ReadOutputs")!.Invoke(host, null)!;
                if ((string)fixtureType.GetMethod("Digest")!.Invoke(null, [files])! != item.GetProperty("expectedFilesSha256").GetString())
                    throw new InvalidDataException("Complete file golden failed: " + id);
            }
            if (expectedBalance >= 0 && (long)balance.GetValue(budget)! != expectedBalance)
                throw new InvalidDataException("Execution ownership did not end.");
        }
        for (var i = 0; i < 4; i++) Once();
        expectedBalance = (long)balance.GetValue(budget)!;
        var times = new List<double>(); var allocations = new List<double>();
        for (var round = 0; round < 7; round++)
        {
            var before = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
            for (var i = 0; i < 5; i++) Once();
            timer.Stop(); times.Add(timer.Elapsed.TotalMilliseconds / 5);
            allocations.Add((GC.GetTotalAllocatedBytes(true) - before) / 5.0);
        }
        observations.Add(new { id, compiledBaseline, steadyBaseline = expectedBalance, milliseconds = times, allocatedBytes = allocations,
            medianMilliseconds = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3) });
    }
    finally
    {
        ((IDisposable)script).Dispose();
        if ((long)balance.GetValue(budget)! != 0) throw new InvalidDataException("Compiled owner did not end.");
        // The existing supervisor validates this generated, owned temporary
        // target before the one native-shell-free cleanup operation.
        validateDirectory.Invoke(null, [owned]);
        if (Directory.Exists(owned)) Directory.Delete(owned, recursive: true);
    }
}
var identities = Directory.EnumerateFiles(directory, "Lokad.*.dll").ToDictionary(p => Path.GetFileName(p)!,
    p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
Console.WriteLine(JsonSerializer.Serialize(new { revision = args[1],
    catalogSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))),
    boundary = "Warm public Run with existing file fixture reset/staging, stdout/file goldens and result disposal; compilation/startup excluded; common reflection overhead", warmups = 4, rounds = 7, batch = 5,
    observations, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    processors = Environment.ProcessorCount, identities, performanceQualified = false }));
