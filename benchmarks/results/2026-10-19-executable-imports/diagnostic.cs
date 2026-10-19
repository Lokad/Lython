using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

const string module = "offset=3\ndef score(size):\n total=offset\n for item in range(size):total+=item\n return total\n";
var engine = new LythonEngine();
var rows = new List<object>();
foreach (var asynchronous in new[] { false, true })
foreach (var fresh in new[] { false, true })
foreach (var imported in new[] { false, true })
{
    var source = imported ? "import helper\nreturn helper.score(64)" : "return sum(range(64))+3";
    var budget = new LythonMemoryBudget(16 * 1024 * 1024);
    using var compiled = fresh ? null : engine.Compile(source, budget);
    if (compiled is not null && !compiled.IsValid) throw new Exception("Invalid entry");
    var baseline = budget.ReservedBytes;
    void Run()
    {
        using var own = fresh ? engine.Compile(source, budget) : null;
        var code = compiled ?? own!;
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", module);
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        using (var result = asynchronous ? code.RunAsync(host, options).GetAwaiter().GetResult() : code.Run(host, options))
        {
            if (!result.Success || !Equals(result.ReturnValue, new BigInteger(2019)))
                throw new Exception(result.Failure?.Message ?? "Wrong result");
        }
        own?.Dispose();
        if (budget.ReservedBytes != baseline) throw new Exception("Owner did not return to baseline");
    }
    for (var i=0;i<8;i++) Run();
    var bytes = new long[7]; var ticks = new long[7];
    for (var sample=0;sample<7;sample++)
    {
        var allocated = GC.GetTotalAllocatedBytes(true); var start = Stopwatch.GetTimestamp();
        for (var i=0;i<8;i++) Run();
        ticks[sample] = Stopwatch.GetTimestamp()-start;
        bytes[sample] = GC.GetTotalAllocatedBytes(true)-allocated;
    }
    rows.Add(new { asynchronous, lane=fresh?"fresh-compile-to-result":"warm-entry-fresh-import", imported, iterations=8, bytes, ticks });
}
var assembly=typeof(LythonEngine).Assembly;
Console.WriteLine(JsonSerializer.Serialize(new { sourceModule=module, timestamp=DateTimeOffset.UtcNow,
    runtime=Environment.Version.ToString(),processorCount=Environment.ProcessorCount,frequency=Stopwatch.Frequency,
    librarySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
    version=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    boundary="Host creation, seeded source, execution/result cleanup; fresh lane also compiles entry. Local nonquiet diagnostic; no forced GC, no CPython timing.",rows }));
