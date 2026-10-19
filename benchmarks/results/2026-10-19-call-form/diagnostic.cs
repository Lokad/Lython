using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;

var engine = new LythonEngine();
var budget = new LythonMemoryBudget(64 * 1024 * 1024);
var rows = new List<object>();
foreach (var (kind, source) in new[] { ("positional", "return max(1,2,3,4)"),
    ("keyword", "return round(1.2345,ndigits=2)"), ("zero-argument", "return dict()"), ("no-call", "return 7") })
{
    void Compile()
    {
        using (var compiled = engine.Compile(source, budget))
            if (!compiled.IsValid) throw new Exception("Invalid source");
        if (budget.ReservedBytes != 0) throw new Exception("Compile owner did not return allowance");
    }
    for (var i = 0; i < 8; i++) Compile();
    var bytes = new long[7]; var ticks = new long[7];
    for (var sample = 0; sample < 7; sample++)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < 8; i++) Compile();
        ticks[sample] = Stopwatch.GetTimestamp() - start;
        bytes[sample] = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
    rows.Add(new { kind, source, iterations = 8, bytes, ticks });
}
Console.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, runtime = Environment.Version.ToString(),
    processorCount = Environment.ProcessorCount, frequency = Stopwatch.Frequency,
    assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))), rows }));
