using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
// Reuse the existing checked worker, adding only a final budget witness.
if (args[0] == "--worker")
{
    var workerPath = Path.GetFullPath(args[1]);
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        var file = Path.Combine(Path.GetDirectoryName(workerPath)!, name.Name + ".dll");
        return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
    };
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(workerPath);
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var manifestType = assembly.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonManifest", true)!;
    var manifest = manifestType.GetMethod("Load", flags)!.Invoke(null, [args[2]])!;
    var workerType = assembly.GetType("Lokad.Lython.Benchmarks.Comparison.LythonComparisonWorker", true)!;
    var worker = Activator.CreateInstance(workerType, flags, null, [manifest, null], null)!;
    var task = (Task)workerType.GetMethod("RunAsync", flags)!.Invoke(worker, [Console.OpenStandardInput(), Console.OpenStandardOutput(), CancellationToken.None])!;
    task.GetAwaiter().GetResult();
    var workerBudget = workerType.GetProperty("MemoryBudget", flags)!.GetValue(worker)!;
    var balance = (long)workerBudget.GetType().GetProperty("ReservedBytes")!.GetValue(workerBudget)!;
    if (balance != 0) throw new InvalidDataException("Worker budget did not return to zero.");
    File.WriteAllText(args[3], JsonSerializer.Serialize(new { finalReservedBytes = balance, allUsersFinished = true }));
    return;
}
