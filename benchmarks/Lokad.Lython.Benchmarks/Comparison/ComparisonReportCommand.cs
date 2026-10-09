using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonReportCommand
{
    internal const int MaximumReceiptBytes = 512 * 1024 * 1024;
    public static int Run(string[] arguments)
    {
        if (arguments.Length != 4 || arguments[0] != "--receipt" || arguments[2] != "--out")
        { Console.Error.WriteLine("Usage: --compare render-report --receipt <receipt.json> --out <report.md>"); return 2; }
        try
        {
            var input = Path.GetFullPath(arguments[1]); var output = Path.GetFullPath(arguments[3]);
            if (input == output) throw new ArgumentException("Report output must differ from its receipt.");
            var receipt = ReadReceipt(input);
            var report = Render(receipt, ComparisonProtocol.Digest(File.ReadAllBytes(input)));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var temporary = output + "." + Guid.NewGuid().ToString("N") + ".partial";
            try { File.WriteAllText(temporary, report, ComparisonProtocol.Utf8); File.Move(temporary, output, true); }
            finally { File.Delete(temporary); }
            Console.WriteLine("Wrote " + output + "; every ratio was recomputed from raw evidence."); return 0;
        }
        catch (Exception failure) { Console.Error.WriteLine("Report rejected: " + failure.GetType().Name + ": " + failure.Message); return 1; }
    }

    public static JsonElement ReadJson(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length is <= 0 or > MaximumReceiptBytes) throw new InvalidDataException("Receipt must be within its 512-MiB bound.");
        using var memory = new MemoryStream(); var buffer = new byte[16 * 1024]; int count;
        while ((count = input.Read(buffer)) != 0)
        {
            if (memory.Length + count > MaximumReceiptBytes) throw new InvalidDataException("Receipt grew beyond its bound.");
            memory.Write(buffer, 0, count);
        }
        using var document = JsonDocument.Parse(ComparisonProtocol.Utf8.GetString(memory.ToArray()), new JsonDocumentOptions { MaxDepth = 64 });
        ComparisonProtocol.ValidateUniqueProperties(document.RootElement); return document.RootElement.Clone();
    }
    public static QualificationReceipt ReadReceipt(string path)
        => ReadJson(path).Deserialize<QualificationReceipt>(ComparisonProtocol.JsonOptions) ?? throw new InvalidDataException("Missing receipt.");

    public static string Render(QualificationReceipt receipt, string receiptSha256)
    {
        var complete = QualificationEvidence.CompleteCampaign(receipt);
        var builder = new StringBuilder();
        builder.AppendLine("# Lython / CPython comparison").AppendLine();
        builder.AppendLine(complete ? "Complete collection; eligibility is assessed per case and session." : "**Partial or invalid evidence: no qualified ratios or winners.**").AppendLine();
        builder.AppendLine($"Lane: `{Escape(receipt.Lane)}`. Revision: `{Escape(receipt.Before?.Revision ?? "missing")}`. " +
            $"Policy/eligibility: {receipt.PolicyVersion}/{receipt.EligibilityVersion}. Receipt SHA-256: `{Escape(receiptSha256)}`.").AppendLine();
        builder.AppendLine($"Scope: {receipt.RequestedCaseIds.Length} selected cases from {receipt.CatalogCaseCount} manifest cases. " +
            "Policy v2 limits each lane to ten minutes including retries. The quick profile covers twelve workloads and two controls, " +
            "with shortened warmup and seven pairs in each of three independent sessions; it is a limited baseline.").AppendLine();
        builder.AppendLine("Ratio means CPython time / Lython time; values above one favor Lython. " +
            "Intervals are fixed-seed paired-bootstrap 95% intervals within one session. " +
            "Independent sessions are shown separately; there is no pooled interval or overall speedup.").AppendLine();
        builder.AppendLine("Lython uses ordinary public limits and output projection; CPython uses -I -S, normal GC/GIL, " +
            "and fresh namespaces without an equivalent in-process governor. Imports keep their source positions. " +
            "Fresh-process samples include parent-observed owned launch through pipe drain and exit, with OS file caches retained. " +
            "Invocation controls are visible and never subtracted.").AppendLine();
        if (receipt.Reason is not null) builder.AppendLine("Campaign reason: " + Escape(receipt.Reason)).AppendLine();
        builder.AppendLine("| Workload | Category | Scale | Session | Status | Lython µs/job | CPython µs/job | CPython/Lython [95% interval] | Winner |");
        builder.AppendLine("| --- | --- | --- | ---: | --- | ---: | ---: | --- | --- |");
        var exclusions = new List<string>();
        foreach (var row in receipt.Cases)
        {
            var assessment = QualificationEvidence.Assess(receipt, row);
            if (assessment.Sessions.Length == 0)
                builder.AppendLine($"| {Escape(row.Workload.Id)} | {Escape(row.Workload.Category)} | {Escape(row.Workload.Scale)} | — | {Escape(row.State)} | — | — | — | — |");
            for (var i = 0; i < assessment.Sessions.Length; i++)
            {
                var stats = assessment.Sessions[i]; var qualified = complete && assessment.Status == "Qualified" && stats.Qualified;
                var ratio = qualified ? $"{Number(stats.Ratio!.Value)} [{Number(stats.RatioLower!.Value)}, {Number(stats.RatioUpper!.Value)}]" : "—";
                builder.AppendLine($"| {Escape(row.Workload.Id)} | {Escape(row.Workload.Category)} | {Escape(row.Workload.Scale)} | {row.Sessions[i].Sampling.Index + 1} | " +
                    $"{assessment.Status} | {Time(stats.LythonMedianSeconds)} | {Time(stats.PythonMedianSeconds)} | {ratio} | {(qualified ? stats.Winner : "—")} |");
            }
            var reasons = assessment.Reasons.Concat(row.Sessions.SelectMany(s => (s.Sampling.Reason is null ? Array.Empty<string>() : new[] { s.Sampling.Reason })
                .Concat(PairedSampler.Evaluate(s.Sampling, false).Reasons.Where(r => !r.StartsWith("Incomplete semantic", StringComparison.Ordinal)))))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (reasons.Length > 0) exclusions.Add($"{Escape(row.Workload.Id)} exclusions: {string.Join("; ", reasons.Select(Escape))}.");
        }
        foreach (var exclusion in exclusions) builder.AppendLine().AppendLine(exclusion);
        builder.AppendLine().AppendLine("Regex is excluded pending Utf8Regex qualification. Native-library jobs measure their complete applications, " +
            "not interpreter dispatch alone. Allocation diagnostics are a separate profile; these timings contain no memory ratio.");
        return builder.ToString();
    }
    private static string Number(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string Time(double seconds) => seconds > 0 && double.IsFinite(seconds) ? Number(seconds * 1_000_000) : "—";
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("`", "'", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

internal static class ComparisonMachineCommand
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length != 2 || arguments[0] != "--out")
        { Console.Error.WriteLine("Usage: --compare check-machine --out <receipt.json>"); return 2; }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var inventory = QuietMachineProbe.Inventory();
            var gate = QuietMachineProbe.CheckAsync(deadline.Token).GetAwaiter().GetResult();
            ComparisonVerifyCommand.WriteAtomic(Path.GetFullPath(arguments[1]), new
            { schemaVersion = 1, policyVersion = ComparisonPolicy.Version, performanceQualified = false, inventory, gate, updated = DateTimeOffset.UtcNow });
            Console.WriteLine(gate.Quiet ? "Initial machine gate passed; a campaign still needs every sampling gate." : "Machine gate failed; qualification cannot proceed.");
            return gate.Quiet ? 0 : 3;
        }
        catch (Exception failure) { Console.Error.WriteLine("Machine check failed: " + failure.GetType().Name + ": " + failure.Message); return 1; }
    }
}
