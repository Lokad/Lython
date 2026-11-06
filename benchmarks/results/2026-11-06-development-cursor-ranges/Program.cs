using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Utf8Regex;

using var active = new CancellationTokenSource();
var budget = new LythonMemoryBudget(2 * 1024 * 1024);
var owner = budget.CreateScope();
var pattern = Utf8RegexProvider.Instance.Compile("(?P<first>ab)(?P<empty>)|(?P<second>β)"u8.ToArray(), new(32, false), owner, default);
var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ab β ", 32)));
var subject = pattern.Prepare(input, new(0, 160, 0, input.Length), owner, default);
var baseline = budget.ReservedBytes;
long Drain(bool verify)
{
    using var cursor = subject.Find(active.Token);
    long checksum = 0;
    int count = 0;
    while (cursor.Next(active.Token) is { } match)
    {
        if (verify)
        {
            var first = count % 2 == 0;
            var start = count / 2 * 6 + (first ? 0 : 3);
            if (match.Captures.Count != 4 || match.Captures[0] != new LythonRegexCapture(start, start + 2)
                || match.Captures[1] != (first ? new LythonRegexCapture(start, start + 2) : null)
                || match.Captures[2] != (first ? new LythonRegexCapture(start + 2, start + 2) : null)
                || match.Captures[3] != (first ? null : new LythonRegexCapture(start, start + 2))
                || match.LastIndex != (first ? 2 : 3)) throw new Exception("Complete byte/capture golden failed");
        }
        for (var i = 0; i < match.Captures.Count; i++)
            if (match.Captures[i] is { } capture) checksum += 17L * capture.StartByte + 31L * capture.EndByte + i;
        checksum += match.LastIndex ?? 0;
        count++;
    }
    if (count != 64) throw new Exception("Complete match count failed");
    return checksum;
}
var expected = Drain(true);
if (budget.ReservedBytes != baseline) throw new Exception("Verification cursor funding leaked");
for (var i = 0; i < 32; i++) if (Drain(false) != expected || budget.ReservedBytes != baseline) throw new Exception("Warmup validation failed");
var times = new double[7];
var allocations = new double[7];
for (var round = 0; round < 7; round++)
{
    long checksum = 0;
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var started = Stopwatch.GetTimestamp();
    for (var iteration = 0; iteration < 128; iteration++) checksum += Drain(false);
    var elapsed = Stopwatch.GetTimestamp() - started;
    allocations[round] = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 128.0;
    times[round] = 1000.0 * elapsed / Stopwatch.Frequency / 128;
    if (checksum != 128 * expected || budget.ReservedBytes != baseline) throw new Exception("Measured result/funding failed");
}
subject.Dispose(); pattern.Dispose(); owner.Dispose();
if (budget.ReservedBytes != 0) throw new Exception("Final owner balance failed");
var names = new[] {"Lokad.Lython", "Lokad.Lython.Utf8Regex", "Lokad.Utf8Regex", "Lokad.Utf8Regex.PythonRe"};
var hashes = names.ToDictionary(n => n, n => { var a = Assembly.Load(n); return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))); });
Console.WriteLine(JsonSerializer.Serialize(new { role = args[0], performanceQualified = false,
    boundary = "synchronous same-thread cursor creation/advances/conversion/disposal; precompiled pattern and prepared subject outside clocks; no Lython script or CPython ratio",
    inputBytes = input.Length, inputScalars = 160, matches = 64, checksum = expected, warmups = 32, rounds = 7, batch = 128,
    milliseconds = times, allocatedBytes = allocations, medianMilliseconds = times.Order().ElementAt(3),
    medianAllocatedBytes = allocations.Order().ElementAt(3), finalBudgetBytes = budget.ReservedBytes,
    runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    hashes, timingStatus = "Uncontrolled busy Windows host; elapsed diagnostic only. Exact same-thread allocations exclude other threads, startup/compilation and producer caches." }));
