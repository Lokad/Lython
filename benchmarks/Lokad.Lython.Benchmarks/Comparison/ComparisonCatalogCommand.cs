using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonCatalogCommand
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length > 0 && arguments[0] == "check-machine") return ComparisonMachineCommand.Run(arguments[1..]);
        if (arguments.Length > 0 && arguments[0] == "qualify") return ComparisonQualificationCommand.Run(arguments[1..]);
        if (arguments.Length > 0 && arguments[0] == "render-report") return ComparisonReportCommand.Run(arguments[1..]);
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
        if (arguments.Length == 0 || arguments[0] != "list")
        {
            Console.Error.WriteLine("Usage: --compare list | worker | once | verify | smoke-fresh | check-machine | qualify | render-report (see benchmarks/COMPARISON.md)");
            return 2;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < arguments.Length; i += 2)
            if (i + 1 == arguments.Length || arguments[i] is not ("--out" or "--profile")
                || !options.TryAdd(arguments[i], arguments[i + 1])) return ListUsage();
        if (options.TryGetValue("--profile", out var profile) && profile is not ("quick" or "full")) return ListUsage();
        IReadOnlyList<ComparisonWorkload> workloads = WorkloadCatalog.Create();
        if (profile == "quick")
        {
            var all = workloads.ToDictionary(w => w.Id, StringComparer.Ordinal);
            workloads = ComparisonPolicy.QuickCaseIds.Select(id => all[id]).ToArray();
        }
        if (!options.TryGetValue("--out", out var destination))
        {
            foreach (var workload in workloads)
                Console.WriteLine($"{workload.Id}\t{workload.Category}\t{workload.Size}\t{Encoding.UTF8.GetByteCount(workload.Source)} source bytes");
            return 0;
        }

        var path = Path.GetFullPath(destination);
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

    private static int ListUsage()
    {
        Console.Error.WriteLine("Usage: --compare list [--profile <full|quick>] [--out <catalog.json>]");
        return 2;
    }
}
