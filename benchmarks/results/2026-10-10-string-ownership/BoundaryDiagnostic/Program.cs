using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var started = DateTimeOffset.UtcNow;
var wall = Stopwatch.StartNew();
var workerPath = Path.GetFullPath(args[0]);
var catalogPath = Path.GetFullPath(args[1]);
var outputPath = Path.GetFullPath(args[2]);
var preflight = args[3] == "preflight";
if (!preflight && args[3] != "collect") throw new ArgumentException("Mode must be preflight or collect");
using (new FileStream(outputPath, FileMode.CreateNew)) { }
var directory = Path.GetDirectoryName(workerPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var workerAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(workerPath);
var manifestType = workerAssembly.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonManifest", true)!;
var workerType = workerAssembly.GetType("Lokad.Lython.Benchmarks.Comparison.LythonComparisonWorker", true)!;
var manifest = manifestType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [catalogPath])!;
var workerIdentity = JsonSerializer.SerializeToElement(workerType.GetMethod("CreateIdentity", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [manifest, true]));
var library = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Lokad.Lython");
Type Type(string name) => library.GetType("Lokad.Lython." + name, true)!;
var governorType = Type("Runtime.MemoryGovernor");
var poolType = Type("Runtime.ChargeReclamationPool");
var stringType = Type("Runtime.Text.PyString");
var listType = Type("Runtime.PyList");
var spanType = Type("LythonSourceSpan");
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
var a = Expression.Parameter(typeof(object), "a");
var b = Expression.Parameter(typeof(object), "b");
var n = Expression.Parameter(typeof(int), "n");
var textArgument = Expression.Parameter(typeof(string), "text");
var nullSpan = Expression.Constant(null, spanType);
var budget = (long)Type("LythonRunOptions").GetField("DefaultMaxExecutionMemoryBytes")!.GetRawConstantValue()!;
var createGovernor = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(governorType.GetConstructor([typeof(long?)])!, Expression.Convert(Expression.Constant(budget), typeof(long?))), typeof(object))).Compile();
var createPool = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.New(poolType.GetConstructor([governorType])!, Expression.Convert(a, governorType)), typeof(object)), a).Compile();
var freeString = Expression.Lambda<Func<string, object>>(Expression.Convert(Expression.Call(stringType.GetMethod("FromString", [typeof(string)])!, textArgument), typeof(object)), textArgument).Compile();
var adopt = Expression.Lambda<Func<object, object, object>>(Expression.Convert(Expression.Call(Type("Runtime.LythonRuntime").GetMethod("OwnSplitListResult", flags)!, Expression.Convert(a, listType), nullSpan, Expression.Convert(b, poolType)), typeof(object)), a, b).Compile();
Func<object, long> LongProperty(Type type, string name) => Expression.Lambda<Func<object, long>>(Expression.Property(Expression.Convert(a, type), type.GetProperty(name, flags)!), a).Compile();
Func<object, int> IntProperty(Type type, string name) => Expression.Lambda<Func<object, int>>(Expression.Property(Expression.Convert(a, type), type.GetProperty(name, flags)!), a).Compile();
var accounted = LongProperty(governorType, "CurrentAccountedBytes");
var reserved = LongProperty(governorType, "CurrentReservedBytes");
var committed = LongProperty(governorType, "CurrentCommittedBytes");
var denied = LongProperty(governorType, "LastDeniedReservationBytes");
var storage = LongProperty(listType, "CommittedStorageBytes");
var stringCharge = LongProperty(stringType, "CommittedOwnedBytes");
var poolCount = IntProperty(poolType, "Count");
var listCount = IntProperty(listType, "Count");
var item = Expression.Lambda<Func<object, int, object>>(Expression.Property(Expression.Convert(a, listType), listType.GetProperty("Item")!, n), a, n).Compile();
var render = Expression.Lambda<Func<object, string>>(Expression.Call(Expression.Convert(a, stringType), stringType.GetMethod("AsString", System.Type.EmptyTypes)!), a).Compile();
var isTracked = Expression.Lambda<Func<object, bool>>(Expression.Call(poolType.GetMethod("IsTrackedValue", flags)!, a), a).Compile();
var owner = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Property(Expression.Convert(a, stringType), stringType.GetProperty("OwnerMemoryGovernor")!), typeof(object)), a).Compile();
using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
var workload = catalog.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "strings.pipeline-ascii.medium");
using var fixture = JsonDocument.Parse(workload.GetProperty("fixtureJson").GetString()!);
var sourceText = fixture.RootElement.GetProperty("text").GetString()!;
var replacedText = sourceText.Replace("::", "/", StringComparison.Ordinal);
var expectedParts = replacedText.Split('/');
Check(expectedParts.Length == 2049 && expectedParts[^1] == "" && expectedParts.Take(2048).Select((p, index) => p == (index % 2 == 0 ? "abZ!" : "tail")).All(v => v), "Unexpected fixture parts");
Check(string.Join('|', expectedParts) + "\n" == workload.GetProperty("expectedOutput").GetString(), "Independent golden differs");
var input = freeString(replacedText);
var separator = freeString("/");
var splitMethod = Type("Runtime.Text.PyStringOps").GetMethod("Split", [stringType, stringType, governorType, spanType])!;
var split = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Call(splitMethod, Expression.Constant(input, stringType), Expression.Constant(separator, stringType), Expression.Convert(a, governorType), nullSpan), typeof(object)), a).Compile();
const int invocations = 16;
const int rounds = 7;
var shapes = new[] { "split.construct", "split.adopt", "split.adopt-tracked-control", "split.construct-and-adopt" };
var constructionStrings = expectedParts.Where(p => p.Length != 0).Sum(p => 128L + Encoding.UTF8.GetByteCount(p));
long expectedStorage = 0;
var capacity = 9;
while (true)
{
    expectedStorage += 64 + 16L * (capacity == 9 ? capacity : capacity / 2);
    if (capacity >= expectedParts.Length) break;
    capacity *= 2;
}
var expectedCount = 2049;
var tierCapacity = 4;
while (tierCapacity < expectedCount) tierCapacity *= 2;
var registryCharge = expectedCount * 128L + tierCapacity * 8L;
var records = new List<object>();
var state = "Running";
string? error = null;
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
void Save() => File.WriteAllText(outputPath, JsonSerializer.Serialize(new
{
    state, error, started, updated = DateTimeOffset.UtcNow, diagnosticOnly = true, performanceQualified = false, fullLanes = false,
    preflight, processId = Environment.ProcessId, wallSeconds = wall.Elapsed.TotalSeconds,
    clockFrequency = Stopwatch.Frequency, clockHighResolution = Stopwatch.IsHighResolution,
    workerIdentity, workerSha256 = Hash(workerPath), librarySha256 = Hash(library.Location),
    diagnosticSha256 = Hash(Assembly.GetExecutingAssembly().Location), catalogSha256 = Hash(catalogPath),
    sourceSha256 = workload.GetProperty("sourceSha256").GetString(), fixtureSha256 = workload.GetProperty("fixtureSha256").GetString(),
    expectedOutputSha256 = workload.GetProperty("expectedOutputSha256").GetString(),
    budget, invocationsPerBatch = invocations, rounds, shapes, expectedParts = expectedParts.Length,
    constructionStrings, expectedStorage, expectedCount, registryCharge,
    serverGc = GCSettings.IsServerGC, gcLatencyMode = GCSettings.LatencyMode.ToString(),
    purpose = "private split construction/adoption boundaries; pinned live values; normal GC/tiering/PGO; no public Python ratio or additive attribution",
    allocationScope = "current helper thread managed allocations in selected boundary only; native handles and other threads excluded",
    setupAndVerificationOutsideBoundary = true, forcedCollections = 0,
    records
}, new JsonSerializerOptions { WriteIndented = true }));
void Verify(Case c, bool tracked)
{
    Check(c.List is not null && listCount(c.List) == expectedParts.Length, "Missing list parts");
    Check(storage(c.List!) == expectedStorage, "List storage charge differs");
    Check(poolCount(c.Pool) == (tracked ? expectedCount : 0), "Entry count differs");
    Check(reserved(c.Governor) == 0 && denied(c.Governor) == 0, "Reservation/denial differs");
    Check(accounted(c.Governor) == constructionStrings + expectedStorage + (tracked ? registryCharge : 0), "Accounted charge differs");
    Check(committed(c.Governor) == accounted(c.Governor) && isTracked(c.List!) == tracked, "Committed/list ownership differs");
    for (var index = 0; index < expectedParts.Length; index++)
    {
        var value = item(c.List!, index);
        var owned = expectedParts[index].Length != 0;
        Check(render(value) == expectedParts[index], "Split item content differs");
        Check(owned ? ReferenceEquals(owner(value), c.Governor) : owner(value) is null, "Item owner differs");
        Check(stringCharge(value) == (owned ? 132 : 128) && isTracked(value) == (tracked && owned), "Item nominal estimate/registration differs");
    }
}
void Batch(string shape, string phase, int round, int count)
{
    if (wall.Elapsed.TotalSeconds > 22) throw new TimeoutException("Internal whole diagnostic deadline");
    var cases = Enumerable.Range(0, count).Select(_ => { var g = createGovernor(); return new Case(g, createPool(g)); }).ToArray();
    var construct = shape is "split.construct" or "split.construct-and-adopt";
    var trackedBefore = shape == "split.adopt-tracked-control";
    foreach (var c in cases)
    {
        if (!construct) { c.List = split(c.Governor); if (trackedBefore) adopt(c.List, c.Pool); Verify(c, trackedBefore); }
        else Check(accounted(c.Governor) == 0 && reserved(c.Governor) == 0 && poolCount(c.Pool) == 0, "Fresh state differs");
    }
    var beforeCharge = cases.Sum(c => accounted(c.Governor));
    var beforeEntries = cases.Sum(c => poolCount(c.Pool));
    var beforeCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    var beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    foreach (var c in cases)
    {
        if (construct) c.List = split(c.Governor);
        if (shape != "split.construct") Check(ReferenceEquals(adopt(c.List!, c.Pool), c.List), "Adoption changed list identity");
    }
    var end = Stopwatch.GetTimestamp();
    var afterAllocation = GC.GetAllocatedBytesForCurrentThread();
    var afterCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
    foreach (var c in cases) Verify(c, shape != "split.construct");
    var afterCharge = cases.Sum(c => accounted(c.Governor));
    var afterEntries = cases.Sum(c => poolCount(c.Pool));
    var chargeDelta = shape switch { "split.construct" => constructionStrings + expectedStorage, "split.adopt" => registryCharge, "split.adopt-tracked-control" => 0, _ => constructionStrings + expectedStorage + registryCharge };
    var entryDelta = shape is "split.adopt" or "split.construct-and-adopt" ? expectedCount : 0;
    Check(afterCharge - beforeCharge == count * chargeDelta && afterEntries - beforeEntries == count * entryDelta, "Boundary delta differs");
    records.Add(new { shape, phase, round, invocations = count, startTicks = start, endTicks = end, elapsedTicks = end - start,
        beforeCharge, afterCharge, beforeEntries, afterEntries, beforeAllocation, afterAllocation, allocatedBytes = afterAllocation - beforeAllocation,
        beforeCollections, afterCollections, partsVerified = count * expectedParts.Length, outcomes = "All exact contents, owners, snapshots, entries and charges verified" });
    GC.KeepAlive(cases);
}
try
{
    Save();
    foreach (var shape in shapes) { Batch(shape, "preflight", -1, 1); Save(); }
    if (!preflight)
    {
        foreach (var shape in shapes)
        {
            var warm = Stopwatch.StartNew();
            var batch = 0;
            do { Batch(shape, "warmup", batch++, invocations); Save(); } while (warm.Elapsed.TotalSeconds < 1);
        }
        Thread.Sleep(2000);
        for (var round = 0; round < rounds; round++)
            for (var offset = 0; offset < shapes.Length; offset++)
            {
                var index = round % 2 == 0 ? (round + offset) % shapes.Length : (round + shapes.Length - offset) % shapes.Length;
                Batch(shapes[index], "measure", round, invocations); Save();
            }
    }
    state = "Completed";
    Save();
    Console.WriteLine($"BOUNDARY_DIAGNOSTIC_COMPLETED {wall.Elapsed.TotalSeconds:F2} seconds, {records.Count} checked batches");
}
catch (Exception failure)
{
    state = "Failed"; error = failure.ToString(); Save(); throw;
}

sealed class Case(object governor, object pool)
{
    public object Governor { get; } = governor;
    public object Pool { get; } = pool;
    public object? List { get; set; }
}
