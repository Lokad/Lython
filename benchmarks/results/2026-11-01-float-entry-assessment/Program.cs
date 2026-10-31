using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

using var corpus = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var rows = corpus.RootElement.EnumerateArray().Select(x => (
    Bytes: Encoding.UTF8.GetBytes(x.GetProperty("token").GetString()!),
    Bits: ulong.Parse(x.GetProperty("bits").GetString()!, NumberStyles.HexNumber))).ToArray();
var failures = new List<object>();
foreach (var row in rows)
{
    var current = double.Parse(row.Bytes, NumberStyles.Float, CultureInfo.InvariantCulture);
    if ((ulong)BitConverter.DoubleToInt64Bits(current) != row.Bits)
        throw new InvalidDataException("Current parser differs from independent corpus.");
    var success = Utf8Parser.TryParse(row.Bytes, out double candidate, out var consumed);
    if (!success || consumed != row.Bytes.Length || (ulong)BitConverter.DoubleToInt64Bits(candidate) != row.Bits)
        failures.Add(new { token = Encoding.UTF8.GetString(row.Bytes), success, consumed,
            expectedBits = row.Bits.ToString("x16"), actualBits = ((ulong)BitConverter.DoubleToInt64Bits(candidate)).ToString("x16") });
}
double Consume(bool candidate)
{
    var checksum = 0d;
    for (var pass = 0; pass < 16; pass++)
        foreach (var row in rows)
        {
            double value;
            if (candidate)
            {
                if (!Utf8Parser.TryParse(row.Bytes, out value, out var consumed) || consumed != row.Bytes.Length)
                    throw new InvalidDataException("Unexpected parse failure.");
            }
            else value = double.Parse(row.Bytes, NumberStyles.Float, CultureInfo.InvariantCulture);
            checksum += value;
        }
    return checksum;
}
var times = new[] { new List<double>(), new List<double>() };
if (failures.Count == 0)
{
    for (var warmup = 0; warmup < 4; warmup++) { _ = Consume(false); _ = Consume(true); }
    for (var round = 0; round < 7; round++)
        for (var offset = 0; offset < 2; offset++)
        {
            var engine = (round + offset) % 2;
            var timer = Stopwatch.StartNew();
            _ = Consume(engine == 1);
            timer.Stop();
            times[engine].Add(timer.Elapsed.TotalMilliseconds);
        }
}
Console.WriteLine(JsonSerializer.Serialize(new { purpose = "Standalone BCL UTF-8 float entry assessment; no whole-script/CPython speedup claim",
    runtime = Environment.Version.ToString(), tokenCount = rows.Length, failures, warmups = 4, rounds = 7,
    passesPerRound = 16, currentMilliseconds = times[0], candidateMilliseconds = times[1],
    currentMedian = times[0].Count == 0 ? (double?)null : times[0].Order().ElementAt(3),
    candidateMedian = times[1].Count == 0 ? (double?)null : times[1].Order().ElementAt(3) }));
