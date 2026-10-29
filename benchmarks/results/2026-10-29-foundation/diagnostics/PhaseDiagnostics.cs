using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Frozen experiment: invoke the real frontend phases without production hooks.
internal static class PhaseDiagnostics
{
    internal static void Run(string corePath, string catalogPath, string outputPath)
    {
        if (File.Exists(outputPath)) throw new IOException("Preserve existing diagnostic output.");
        corePath = Path.GetFullPath(corePath);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(Path.GetDirectoryName(corePath)!, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);
        Type Type(string name) => core.GetType("Lokad.Lython." + name, throwOnError: true)!;
        MethodInfo Static(string type, string method) => Type(type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == method);
        var frontend = Static("Frontend.LythonFrontend", "Compile");
        var lower = Static("Frontend.LoweredScript", "Lower");
        var executable = Static("Frontend.ExecutableScript", "Compile");
        var estimate = Static("Runtime.RuntimeMemoryEstimates", "EstimateCompilationBytes");
        var budgetType = Type("LythonMemoryBudget");
        var governorType = Type("Runtime.MemoryGovernor");
        var budgetInterface = Type("ILythonMemoryBudget");
        var createScope = budgetInterface.GetMethod("CreateScope")!;
        var reserve = budgetInterface.GetMethod("TryReserve")!;
        var reserved = budgetType.GetProperty("ReservedBytes")!;
        var peak = budgetType.GetProperty("PeakBytes")!;
        var engine = Activator.CreateInstance(Type("LythonEngine"))!;
        var compile = engine.GetType().GetMethod("Compile", [typeof(string), budgetInterface])!;
        var budget = Activator.CreateInstance(budgetType, new object?[] { null })!;
        var rows = new List<object>();
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
        var selected = catalog.RootElement.GetProperty("cases").EnumerateArray()
            .Where(c => c.GetProperty("id").GetString() is
                "scripts.csv-summary-helpers.large" or "scripts.text-summary-helpers.large" or
                "scripts.json-summary-helpers.large" or "scripts.config-edit.small" or
                "scripts.file-manifest-helpers.large" or "scripts.file-search.large").ToArray();
        if (selected.Length != 6) throw new ArgumentException("Expected the six frozen representative sources.");
        foreach (var item in selected)
        {
            var source = item.GetProperty("source").GetString()!;
            var bytes = (long)estimate.Invoke(null, [source.Length])!;
            var publicArgs = new object?[] { source, budget };
            for (var i = 0; i < 32; i++) ((IDisposable)compile.Invoke(engine, publicArgs)!).Dispose();
            var observations = new List<object>();
            for (var i = 0; i < 64; i++)
            {
                using (var scope = (IDisposable)createScope.Invoke(budget, null)!)
                {
                    if (!(bool)reserve.Invoke(scope, [bytes, null])!) throw new InvalidOperationException("Admission failed.");
                    var governor = Activator.CreateInstance(governorType, [scope])!;
                    var front = Measure(() => frontend.Invoke(null, [source, governor, null, CancellationToken.None])!);
                    var syntax = front.Value.GetType().GetProperty("Script")!.GetValue(front.Value)
                        ?? throw new InvalidOperationException("Invalid frontend result.");
                    var lowered = Measure(() => lower.Invoke(null, [syntax])!);
                    var code = Measure(() => executable.Invoke(null, [lowered.Value])!);
                    observations.Add(new { iteration = i, kind = "reflected phases",
                        frontend = front.Counters, lowering = lowered.Counters, executable = code.Counters,
                        reservedBytes = reserved.GetValue(budget), peakBytes = peak.GetValue(budget) });
                    GC.KeepAlive(code.Value);
                }
                if ((long)reserved.GetValue(budget)! != 0) throw new InvalidOperationException("Phase owner leaked.");
                var whole = Measure(() => compile.Invoke(engine, publicArgs)!);
                var retained = (long)reserved.GetValue(budget)!;
                var disposal = Measure(() => { ((IDisposable)whole.Value).Dispose(); return 0; });
                observations.Add(new { iteration = i, kind = "public compilation", compile = whole.Counters,
                    disposal = disposal.Counters, retainedBytes = retained, afterCleanup = reserved.GetValue(budget) });
                if ((long)reserved.GetValue(budget)! != 0) throw new InvalidOperationException("Compilation owner leaked.");
            }
            rows.Add(new { id = item.GetProperty("id").GetString(), observations });
        }
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new {
            performanceQualified = false,
            note = "Diagnostic only. Real frontend/lowering/executable phases invoked through reflection; invocation/argument-array overhead included. Current-thread allocations are valid for these synchronous compilation phases. Do not add phase medians or compare them with public run timings.",
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            corePath, coreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(corePath))),
            coreVersion = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            catalogSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(catalogPath))),
            rows,
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private sealed record Counters(double Microseconds, long AllocatedBytes);
    private static (object Value, Counters Counters) Measure(Func<object> operation)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var value = operation();
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMicroseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return (value, new(elapsed, bytes));
    }
}
