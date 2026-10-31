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
Console.WriteLine(JsonSerializer.Serialize(new { purpose="B21 semantic-only qualification, no repeated component timing", runtime=Environment.Version.ToString(), tokenCount=rows.Length, failures }));
