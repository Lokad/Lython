using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

var engine = new LythonEngine();
var rows = new List<object>();
foreach (var lane in new[] { "compile", "Run", "RunAsync" })
foreach (var imported in new[] { false, true })
{
    var source = imported ? "for i in range(32):\n import math\nreturn 32" : "for i in range(32):\n pass\nreturn 32";
    var budget = new LythonMemoryBudget(64 * 1024 * 1024);
    using var compiled = lane == "compile" ? null : engine.Compile(source, budget);
    var baseline = budget.ReservedBytes;
    var options = new LythonRunOptions { MemoryBudget = budget };
    void Run()
    {
        using var own = lane == "compile" ? engine.Compile(source, budget) : null;
        var code = compiled ?? own!;
        if (!code.IsValid) throw new Exception("Invalid source");
        if (lane != "compile")
        {
            using var result = lane == "RunAsync" ? code.RunAsync(new MockLythonHost(), options).GetAwaiter().GetResult()
                : code.Run(new MockLythonHost(), options);
            if (!result.Success || !Equals(result.ReturnValue, new BigInteger(32)))
                throw new Exception(result.Failure?.Message ?? "Wrong result");
        }
        own?.Dispose();
        if (budget.ReservedBytes != baseline) throw new Exception("Owner did not return to baseline");
    }
    for (var i = 0; i < 8; i++) Run();
    var bytes = new long[7]; var ticks = new long[7];
    for (var sample = 0; sample < 7; sample++)
    {
        var allocated = GC.GetTotalAllocatedBytes(true); var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < 8; i++) Run();
        ticks[sample] = Stopwatch.GetTimestamp() - start;
        bytes[sample] = GC.GetTotalAllocatedBytes(true) - allocated;
    }
    rows.Add(new { lane, imported, iterations = 8, bytes, ticks });
}
Console.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, runtime = Environment.Version.ToString(),
    processorCount = Environment.ProcessorCount, frequency = Stopwatch.Frequency,
    assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))), rows }));
