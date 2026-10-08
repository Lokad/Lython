using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonCatalogCommand
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length > 0 && arguments[0] == "verify")
            return ComparisonVerifyCommand.Run(arguments[1..]);
        if (arguments.Length > 0 && arguments[0] == "smoke-fresh")
            return ComparisonVerifyCommand.Run(arguments[1..], fresh: true);
        if (arguments.Length == 3 && arguments[0] is "worker" or "once" && arguments[1] == "--catalog")
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                var manifest = ComparisonManifest.Load(arguments[2]);
                if (arguments[0] == "once") LythonOnceWorker.RunAsync(manifest, Console.OpenStandardOutput()).GetAwaiter().GetResult();
                else new LythonComparisonWorker(manifest).RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput()).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("Comparison worker error: " + failure.GetType().Name + ": " + failure.Message);
                return 2;
            }
        }
        if (arguments.Length == 0 || arguments[0] != "list"
            || (arguments.Length != 1 && (arguments.Length != 3 || arguments[1] != "--out")))
        {
            Console.Error.WriteLine("Usage: --compare list [--out <catalog.json>] | worker/once --catalog <catalog.json> | verify/smoke-fresh --catalog ... --dotnet ... --python ... --python-worker ... --out ... (sampling driver pending)");
            return 2;
        }

        var workloads = WorkloadCatalog.Create();
        if (arguments.Length == 1)
        {
            foreach (var workload in workloads)
                Console.WriteLine($"{workload.Id}\t{workload.Category}\t{workload.Size}\t{Encoding.UTF8.GetByteCount(workload.Source)} source bytes");
            return 0;
        }

        var path = Path.GetFullPath(arguments[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            catalogVersion = WorkloadCatalog.Version,
            fixtureEncoding = "immutable literals rendered into shared source; mutable state created per invocation",
            entropySeed = "0x5EED1234",
            performanceQualified = false,
            cases = workloads,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            File.WriteAllText(temporary, json + "\n", new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        Console.WriteLine($"Wrote {workloads.Count} correctness candidates to {path}; no timings collected.");
        return 0;
    }
}
