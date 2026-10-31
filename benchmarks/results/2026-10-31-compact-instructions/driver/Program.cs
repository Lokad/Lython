using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Existing allocation diagnostic adapted to complete public compilations.
var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var engineType = core.GetType("Lokad.Lython.LythonEngine", true)!;
var engine = Activator.CreateInstance(engineType)!;
var budgetType = core.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
var budget = Activator.CreateInstance(budgetType, [null])!;
var balance = budgetType.GetProperty("ReservedBytes")!;
var compile = engineType.GetMethod("Compile", [typeof(string), core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var ids = new[] { "scripts.csv-summary-helpers.large", "scripts.text-summary-helpers.large",
    "scripts.json-summary-helpers.large", "scripts.config-edit.small", "scripts.file-search.large",
    "scripts.file-manifest-helpers.large" };
var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().Where(c =>
    ids.Contains(c.GetProperty("id").GetString())).ToArray();
if (cases.Length != ids.Length) throw new InvalidDataException("Missing representative compilation source.");
var observations = new List<object>();
foreach (var item in cases)
{
    var id = item.GetProperty("id").GetString();
    var source = item.GetProperty("source").GetString()!;
    void Once()
    {
        using var script = (IDisposable)compile.Invoke(engine, [source, budget])!;
        if (!(bool)script.GetType().GetProperty("IsValid")!.GetValue(script)!)
            throw new InvalidDataException("Compilation golden failed: " + id);
    }
    for (var i = 0; i < 4; i++) Once();
    var times = new List<double>(); var allocations = new List<double>();
    for (var round = 0; round < 7; round++)
    {
        var before = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) Once();
        timer.Stop(); times.Add(timer.Elapsed.TotalMilliseconds / 5);
        allocations.Add((GC.GetTotalAllocatedBytes(true) - before) / 5.0);
        if ((long)balance.GetValue(budget)! != 0) throw new InvalidDataException("Compilation ownership did not end.");
    }
    observations.Add(new { id, sourceBytes = System.Text.Encoding.UTF8.GetByteCount(source), milliseconds = times,
        allocatedBytes = allocations, medianMilliseconds = times.Order().ElementAt(3),
        medianAllocatedBytes = allocations.Order().ElementAt(3) });
}
var identities = Directory.EnumerateFiles(directory, "Lokad.*.dll").ToDictionary(p => Path.GetFileName(p)!,
    p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
var instructionType = core.GetType("Lokad.Lython.Frontend.ExecutableInstruction", true)!;
GC.KeepAlive(Array.CreateInstance(instructionType, 1));
const int arrayLength = 16384;
long Allocation(int count)
{
    var before = GC.GetAllocatedBytesForCurrentThread();
    var array = Array.CreateInstance(instructionType, count);
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    GC.KeepAlive(array);
    return allocated;
}
var emptyArrayBytes = Allocation(0);
var fullArrayBytes = Allocation(arrayLength);
var instructionStorage = new { arrayLength, emptyArrayBytes, fullArrayBytes,
    elementBytes = (fullArrayBytes - emptyArrayBytes) / arrayLength,
    boundary = "Managed array allocation delta; no unsafe or native size/layout APIs" };
Console.WriteLine(JsonSerializer.Serialize(new { revision = args[1],
    catalogSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))),
    boundary = "Complete public Compile(source, sharedBudget) and script disposal; execution/startup excluded; common reflection overhead", warmups = 4, rounds = 7, batch = 5,
    instructionStorage, observations, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    processors = Environment.ProcessorCount, identities, performanceQualified = false }));
