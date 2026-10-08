using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonCatalogCommand
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] != "list"
            || (arguments.Length != 1 && (arguments.Length != 3 || arguments[1] != "--out")))
        {
            Console.Error.WriteLine("Usage: --compare list [--out <catalog.json>] (catalog only; timing driver pending)");
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
