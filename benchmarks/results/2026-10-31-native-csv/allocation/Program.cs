using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var type = assembly.GetType("Lokad.Lython.Benchmarks.CsvReaderBenchmarks", true)!;
var benchmark = Activator.CreateInstance(type)!;
type.GetProperty("RowCount")!.SetValue(benchmark, 1000);
type.GetMethod("Seed")!.Invoke(benchmark, null);
const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
var host = type.GetField("_host", fields)!.GetValue(benchmark)!;
var observations = new List<object>();
try
{
    foreach (var field in new[] { "_retainAll", "_discardAll" })
    {
        var script = type.GetField(field, fields)!.GetValue(benchmark)!;
        var run = script.GetType().GetMethods().Single(m => m.Name == "RunAsync" && m.GetParameters().Length == 1);
        async Task Once()
        {
            var pending = (Task)run.Invoke(script, [host])!;
            await pending;
            var result = pending.GetType().GetProperty("Result")!.GetValue(pending)!;
            using ((IDisposable)result)
                if (!(bool)result.GetType().GetProperty("Success")!.GetValue(result)! ||
                    result.GetType().GetProperty("ReturnValue")!.GetValue(result)?.ToString() != "1000" ||
                    result.GetType().GetProperty("StandardOutput")!.GetValue(result)?.ToString() != "")
                    throw new InvalidDataException("CSV benchmark golden failed.");
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
        observations.Add(new { field, milliseconds = times, allocatedBytes = allocations,
            medianMilliseconds = times.Order().ElementAt(3), medianAllocatedBytes = allocations.Order().ElementAt(3) });
    }
    var identities = Directory.EnumerateFiles(directory, "Lokad.*.dll").ToDictionary(p => Path.GetFileName(p)!,
        p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))!;
    Console.WriteLine(JsonSerializer.Serialize(new { revision = args[1], rowCount = 1000, columns = 6,
        boundary = "Existing CsvReaderBenchmarks warm public RunAsync, return-count golden and result disposal; compilation/startup excluded; common reflection overhead", warmups = 4, rounds = 7, batch = 5,
        observations, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        processors = Environment.ProcessorCount, identities, performanceQualified = false }));
}
finally { type.GetMethod("Cleanup")!.Invoke(benchmark, null); }
