using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

var engine = new LythonEngine();
var rows = new List<object>();
foreach (var asynchronous in new[] { false, true })
foreach (var exactRange in new[] { false, true })
{
    var source = "total=0\nfor repeat in range(16):total+=sum(" +
        (exactRange ? "range(128)" : "iter(range(128))") + ")\nreturn total";
    var budget = new LythonMemoryBudget(64 * 1024 * 1024);
    using var code = engine.Compile(source, budget);
    if (!code.IsValid) throw new Exception("Invalid source");
    var baseline = budget.ReservedBytes;
    var options = new LythonRunOptions { MemoryBudget = budget };
    void Run()
    {
        using (var result = asynchronous ? code.RunAsync(new MockLythonHost(), options).GetAwaiter().GetResult()
            : code.Run(new MockLythonHost(), options))
            if (!result.Success || !Equals(result.ReturnValue, new BigInteger(130048)))
                throw new Exception(result.Failure?.Message ?? "Wrong result");
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
    rows.Add(new { asynchronous, exactRange, source, iterations = 8, bytes, ticks });
}
Console.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, runtime = Environment.Version.ToString(),
    processorCount = Environment.ProcessorCount, frequency = Stopwatch.Frequency,
    assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))), rows }));
