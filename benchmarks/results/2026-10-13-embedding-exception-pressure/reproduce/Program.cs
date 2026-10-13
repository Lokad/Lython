using System.Diagnostics;
using System.Reflection;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

// Fixed prospective policy. The outer supervisor owns a 28-second deadline.
const int HostBytes = 128 * 1024 * 1024, SlotBytes = 1024, Capacity = 12000;
// New prospective feasibility model; historical embedding-v1 is unchanged.
const int windowSeconds = 4;
if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Linux controlled exception-pressure model");
if (args.Length != 3) throw new ArgumentException("case data-root script-root");
var name = args[0];
var control = name.EndsWith("-control", StringComparison.Ordinal);
var activeName = control ? name[..^8] : name;
var asynchronous = false;
if (!activeName.EndsWith("-sync", StringComparison.Ordinal)) throw new ArgumentException("Fixed sync matrix");
var job = activeName[..^5];
var pressure = true;
var exposed = job is "pressure-exposed" or "pressure-retained";
var retained = job == "pressure-retained";
if (job is not ("pressure-unbound" or "pressure-bound" or "pressure-exposed" or "pressure-retained")) throw new ArgumentException("Unknown fixed case");
var directory = Path.Combine(args[1], (pressure ? "csv-summary" : job) + "-1024");
var originalSource = File.ReadAllText(Path.Combine(args[2], job + ".py"));
var source = originalSource;
var script = new LythonEngine().Compile(source);
if (!script.IsValid) throw new Exception(string.Join("|", script.Diagnostics));
var synchronousHost = new MockLythonHost();
var delayedHost = new DelayedLythonHost();
ILythonHost host = asynchronous ? delayedHost : synchronousHost;
var expected = new Dictionary<string, byte[]>();
var fixtureHashes = new SortedDictionary<string, string>();
foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
{
    var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
    var bytes = File.ReadAllBytes(file);
    fixtureHashes[relative] = Convert.ToHexString(SHA256.HashData(bytes));
    if (relative.StartsWith("input.", StringComparison.Ordinal))
    {
        synchronousHost.SeedRawTextUtf8("/" + relative, bytes);
        delayedHost.SeedFile("/" + relative, Encoding.UTF8.GetString(bytes));
    }
    else if (relative.StartsWith("inbox/", StringComparison.Ordinal))
    {
        synchronousHost.SeedBytes("/" + relative, bytes);
        delayedHost.SeedBytes("/" + relative, bytes);
    }
    else if (relative.StartsWith("report.", StringComparison.Ordinal) || relative.StartsWith("outbox/", StringComparison.Ordinal) || relative == "manifest.json")
        expected["/" + relative] = bytes;
}
var stdout = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "stdout.bin"))).Replace("\r\n", "\n");
var cap = 48000L;
var options = new LythonRunOptions { MaxExecutionMemoryBytes = cap };
if (pressure) { expected.Clear(); stdout = ""; }
long peak = 0;
int yieldedJobs = 0;
void RunJob()
{
    LythonExecutionResult result;
    if (asynchronous)
    {
        var pending = script.RunAsync(host, options);
        if (pending.IsCompleted) throw new Exception("Async job did not genuinely yield");
        result = pending.GetAwaiter().GetResult();
        yieldedJobs++;
    }
    else result = script.Run(host, options);
    peak = Math.Max(peak, result.PeakExecutionMemoryBytes);
    if (!result.Success || result.StandardOutput != stdout || result.StandardError.Length != 0)
        throw new Exception(result.Failure?.Message ?? "Complete output mismatch");
    if (pressure && (result.ReturnValue is not IReadOnlyList<object?> values || values.Count != 4 ||
        !Equals(values[0], new BigInteger(128)) || !Equals(values[1], new BigInteger(32000)) ||
        !Equals(values[2], new BigInteger(retained ? 8 : 0)) || !Equals(values[3], new BigInteger(exposed ? 128 : 0))))
        throw new Exception("Pinned data/denial count mismatch");
    foreach (var file in expected)
    {
        var actual = file.Key.StartsWith("/outbox/", StringComparison.Ordinal)
            ? (asynchronous ? delayedHost.ReadBytes(file.Key) : synchronousHost.ReadBytes(file.Key))
            : Encoding.UTF8.GetBytes(asynchronous ? delayedHost.ReadText(file.Key) : synchronousHost.ReadText(file.Key));
        if (!actual.AsSpan().SequenceEqual(file.Value)) throw new Exception("Published bytes differ: " + file.Key);
    }
    if (result.PeakExecutionMemoryBytes > cap) throw new Exception("Live-memory cap exceeded");
}
var hostState = new byte[HostBytes / SlotBytes][];
for (var i = 0; i < hostState.Length; i++) { hostState[i] = new byte[SlotBytes]; hostState[i][0] = (byte)i; }
var request = "{\"tenant\":\"benchmark\",\"values\":[1,2,3,4],\"accepted\":true}"u8.ToArray();
long requestChecksum = 0;
void Request(int index)
{
    using var document = JsonDocument.Parse(request);
    requestChecksum += document.RootElement.GetProperty("values")[3].GetInt32() + hostState[index % hostState.Length][0];
}
for (var i = 0; i < 100; i++) Request(i);
var preparationBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
// Two declared blocking, noncompacting preparation collections promote every
// retained array to Gen2. They are outside both warmup and measurement.
GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
var preparationCollections = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - preparationBefore[g]).ToArray();
RunJob(); RunJob();
Thread.Sleep(2000); // Same fixed preparation pause for every independent child.
var hostGenerations = new int[3];
foreach (var value in hostState) hostGenerations[GC.GetGeneration(value)]++;
if (hostGenerations[2] != hostState.Length) throw new Exception("Declared old-host preparation failed");
var yieldedBeforeMeasurement = yieldedJobs;
var delayedCallsBeforeMeasurement = delayedHost.CompletedAsynchronously;

var requestTicks = new long[Capacity];
var heartbeatTicks = new long[Capacity];
int samples = 0;
using var stop = new CancellationTokenSource();
using var started = new ManualResetEventSlim();
Exception? observerFailure = null;
var observer = new Thread(() =>
{
    try
    {
        var previous = Stopwatch.GetTimestamp();
        started.Set();
        while (!stop.IsCancellationRequested)
        {
            Thread.Sleep(1);
            var start = Stopwatch.GetTimestamp();
            var late = Math.Max(0, start - previous - Stopwatch.Frequency / 1000);
            Request(samples);
            var end = Stopwatch.GetTimestamp();
            if (samples == Capacity) throw new Exception("Observer capacity exhausted");
            heartbeatTicks[samples] = late;
            requestTicks[samples++] = late + end - start;
            previous = end;
        }
    }
    catch (Exception error) { observerFailure = error; started.Set(); }
}) { IsBackground = true, Name = "managed-host-observer" };
var process = Process.GetCurrentProcess();
var cpuBefore = process.TotalProcessorTime;
var allocationsBefore = GC.GetTotalAllocatedBytes();
var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
var measured = Stopwatch.StartNew();
int completed = 0;
observer.Start();
started.Wait();
try
{
    while (measured.Elapsed.TotalSeconds < windowSeconds)
    {
        if (control) Thread.Sleep(1);
        else { RunJob(); completed++; }
    }
}
finally
{
    stop.Cancel();
    if (!observer.Join(1000)) throw new Exception("Observer cleanup failed");
}
measured.Stop();
if (observerFailure is not null) throw new Exception("Observer failed", observerFailure);
if (samples < 1000) throw new Exception($"Insufficient request observations: {samples}");
if (!control && completed == 0) throw new Exception("No complete jobs");
var gc = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - gcBefore[g]).ToArray();
var allocated = GC.GetTotalAllocatedBytes() - allocationsBefore;
var cpu = (process.TotalProcessorTime - cpuBefore).TotalSeconds;
GC.KeepAlive(hostState);
object Describe(long[] ticks)
{
    var values = ticks.Take(samples).Select(t => 1000.0 * t / Stopwatch.Frequency).ToArray();
    var sorted = values.Order().ToArray();
    double Quantile(double p) => sorted[(int)Math.Ceiling(p * sorted.Length) - 1];
    return new { p50 = Quantile(.50), p95 = Quantile(.95), p99 = Quantile(.99), max = sorted[^1], rawMilliseconds = values };
}
var assembly = typeof(LythonEngine).Assembly;
Console.WriteLine(JsonSerializer.Serialize(new {
    policy = "embedding-v5-linux-exception-pressure", name, control, asynchronous, pressure, windowSeconds, preparationCollections,
    capBytes = cap, retainedPayloadBytes = 32000, caughtDenialsPerJob = 128, retainedExceptions = retained ? 8 : 0, exposedDenials = exposed ? 128 : 0,
    warmupYieldedJobs = yieldedBeforeMeasurement, warmupDelayedHostCalls = delayedCallsBeforeMeasurement,
    yieldedJobs = yieldedJobs - yieldedBeforeMeasurement, delayedHostCalls = delayedHost.CompletedAsynchronously - delayedCallsBeforeMeasurement,
    qualification = "prospective controlled model; supervisor determines eligibility",
    observationSeconds = measured.Elapsed.TotalSeconds, completed, peakBytes = peak,
    modeledHostBytes = HostBytes, modeledHostObjects = hostState.Length, hostGenerationsBefore = hostGenerations, samples, requestChecksum,
    allocatedBytes = allocated, gcCollections = gc, cpuSeconds = cpu, cpuCoreFraction = cpu / measured.Elapsed.TotalSeconds,
    processorCount = Environment.ProcessorCount, runtime = Environment.Version.ToString(),
    sourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))), originalSourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalSource))),
    fixtureHashes, subjectInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    subjectDllSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
    requestLatencyMilliseconds = Describe(requestTicks), heartbeatLatenessMilliseconds = Describe(heartbeatTicks)
}));
