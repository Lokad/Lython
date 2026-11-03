using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// One private adaptation of the complete-job driver, not a new benchmark API.
var overrides = new[] { "DOTNET_gcServer", "COMPlus_gcServer", "DOTNET_GCHeapCount", "COMPlus_GCHeapCount",
    "DOTNET_GCHeapHardLimit", "COMPlus_GCHeapHardLimit", "DOTNET_GCHeapHardLimitPercent", "COMPlus_GCHeapHardLimitPercent",
    "DOTNET_TieredCompilation", "COMPlus_TieredCompilation", "DOTNET_TieredPGO", "COMPlus_TieredPGO",
    "DOTNET_TC_QuickJit", "COMPlus_TC_QuickJit", "DOTNET_TC_QuickJitForLoops", "COMPlus_TC_QuickJitForLoops",
    "DOTNET_ReadyToRun", "COMPlus_ReadyToRun", "DOTNET_PROCESSOR_COUNT" }.ToDictionary(n => n, Environment.GetEnvironmentVariable);
if (overrides.Values.Any(v => v is not null) || System.Runtime.GCSettings.IsServerGC)
    throw new InvalidDataException("Require the ordinary worker GC/tiering profile.");
var beforePath = Path.GetFullPath(args[0]);
var candidatePath = Path.GetFullPath(args[1]);
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[2]));
using var witness = JsonDocument.Parse(File.ReadAllBytes(args[3]));
var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().ToArray();
if (cases.Length != 8) throw new InvalidDataException("Require five complete jobs and three casing controls.");
var phase = args.Length > 5 ? args[5] : "warm";
if (phase is not ("warm" or "fresh" or "compile")) throw new InvalidDataException("Unknown phase.");
var warmups = phase == "warm" ? 32 : 32;
var batch = 16;
var asynchronous = args.Length > 6 && args[6] == "async";
var observations = new List<object>();
var evidenceDirectory = Path.GetDirectoryName(Path.GetFullPath(args[3]))!;
foreach (var item in cases)
{
    var caseId = item.GetProperty("id").GetString();
    var hashSource = witness.RootElement.GetProperty("source").GetString()!;
    using var before = new Image(beforePath, item, hashSource, "before", asynchronous);
    using var candidate = new Image(candidatePath, item, hashSource, "candidate", asynchronous);
    using var control = new Image(beforePath, item, hashSource, "before-control", asynchronous);
    var images = args.Length > 4 && args[4] == "reverse" ? new[] { control, candidate, before } : new[] { before, candidate, control };
    if (images.Select(x => x.Core).Distinct().Count() != 3 || images.Select(x => x.EngineType).Distinct().Count() != 3 ||
        images.Any(x => x.EngineType.BaseType!.Assembly != typeof(object).Assembly || !x.Hashes.SequenceEqual(before.Hashes)) ||
        JsonSerializer.Serialize(before.Files) != JsonSerializer.Serialize(control.Files) ||
        candidate.Files.Any(kv => kv.Key != "Lokad.Lython.dll" && before.Files[kv.Key] != kv.Value))
        throw new InvalidDataException("Image isolation, CoreLib, hashes or auxiliary bits differ.");
    foreach (var image in images) image.Once();
    for (var i = 0; i < warmups; i++) foreach (var image in images) image.Measure(phase);
    foreach (var image in images) image.FreezeBalance();
    Thread.Sleep(TimeSpan.FromSeconds(.2));
    var samples = new List<object>();
    for (var round = 0; round < 5; round++)
    {
        for (var offset = 0; offset < 3; offset++)
        {
            var index = round % 2 == 0 ? (round + offset) % 3 : (round + 3 - offset) % 3;
            var image = images[index];
            var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
            var phaseBefore = image.PhaseTicks;
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            var clock = Stopwatch.StartNew();
            for (var i = 0; i < batch; i++) image.Measure(phase);
            clock.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            samples.Add(new { round, image = image.Label, jobs = batch, elapsedMilliseconds = clock.Elapsed.TotalMilliseconds, cpuMilliseconds = (Process.GetCurrentProcess().TotalProcessorTime - cpuBefore).TotalMilliseconds, executionMilliseconds = (image.PhaseTicks - phaseBefore) * 1000.0 / Stopwatch.Frequency, allocatedBytes });
        }
    }
    foreach (var image in images) image.Once();
    foreach (var image in images) image.Dispose();
    var observation = new { caseId, samples, hashWitness = before.Hashes,
        admittedPeaks = images.ToDictionary(i => i.Label, i => new { i.PeakExecutionBytes, i.PeakRootBytes }),
        identities = images.ToDictionary(i => i.Label, i => i.Files),
        allGoldensPassed = true, allBalancesZero = true, ownedFixturesRemoved = true };
    observations.Add(observation);
    File.WriteAllText(Path.Combine(evidenceDirectory, caseId + ".json"), JsonSerializer.Serialize(observation));
}
Console.WriteLine(JsonSerializer.Serialize(new {
    performanceQualified = false, purpose = "B178 allocation/peak diagnostic ONLY; short sampling with shared ALCs; elapsed/CPU not a source-pair performance qualification; both APIs and three complete casing controls",
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    processId = Environment.ProcessId, processors = Environment.ProcessorCount,
    catalogSHA256 = Hash(args[2]), witnessSHA256 = Hash(args[3]),
    sharedCoreLib = typeof(object).Assembly.Location, sharedCoreLibSHA256 = Hash(typeof(object).Assembly.Location),
    imageIsolationVerified = true, asynchronous, phase, warmupsPerImage = warmups, settleSecondsPerCase = .2, rounds = 5, batch, observations,
    normalGcAndTiering = true, runtimeOverrides = overrides, serverGc = System.Runtime.GCSettings.IsServerGC,
    allGoldensPassed = true, allBalancesZero = true, ownedFixturesRemoved = true,
    boundary = "Complete public Run or awaited RunAsync/fixture reset/mediated I/O/full goldens/result disposal; startup/compilation excluded. Isolated Lython images share CoreLib/process GC and hashing. No context-unload guarantee."
}));

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

sealed class Image : IDisposable
{
    private readonly object _budget;
    private readonly PropertyInfo _balance;
    private readonly object _host;
    private readonly MethodInfo _reset, _outputs, _digest, _validate;
    private object _script;
    private readonly object _engine;
    private readonly MethodInfo _compile;
    private readonly string _source;
    private readonly MethodInfo _run, _runAsync;
    private readonly PropertyInfo _asyncResult;
    private readonly bool _asynchronous;
    private readonly string _owned, _stdout, _fileGolden;
    private long _expected = -1;
    private bool _disposed;
    public long PhaseTicks { get; private set; }
    private bool _capturePeak = true;
    public long PeakExecutionBytes { get; private set; }
    public long PeakRootBytes { get; private set; }
    public string Label { get; }
    public Assembly Core { get; }
    public Type EngineType { get; }
    public string[] Hashes { get; }
    public Dictionary<string, string> Files { get; }

    public Image(string path, JsonElement item, string hashSource, string label, bool asynchronous)
    {
        Label = label;
        _asynchronous = asynchronous;
        var directory = Path.GetDirectoryName(path)!;
        var context = new ImageContext(directory, label);
        var benchmark = context.LoadFromAssemblyPath(path);
        Core = context.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.dll"));
        EngineType = Core.GetType("Lokad.Lython.LythonEngine", true)!;
        var engine = Activator.CreateInstance(EngineType)!;
        _engine = engine;
        _source = item.GetProperty("source").GetString()!;
        var budgetType = Core.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
        _budget = Activator.CreateInstance(budgetType, [null])!;
        _balance = budgetType.GetProperty("ReservedBytes")!;
        var compile = EngineType.GetMethod("Compile", [typeof(string), Core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
        _compile = compile;
        var fixtureType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileFixture", true)!;
        var fixture = fixtureType.GetMethod("Parse")!.Invoke(null, [item.GetProperty("fixtureJson").GetString()])!;
        var hostType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileHost", true)!;
        _validate = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.OwnedWorkerProcess", true)!
            .GetMethod("ValidateFixtureDirectory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        _owned = Path.Combine(Path.GetTempPath(), "lython-comparison-files-" + Guid.NewGuid().ToString("N"));
        _host = Activator.CreateInstance(hostType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [fixture, _owned], null)!;
        _reset = hostType.GetMethod("Reset")!; _outputs = hostType.GetMethod("ReadOutputs")!;
        _digest = fixtureType.GetMethod("Digest")!;
        try
        {
            using (var hashProgram = (IDisposable)compile.Invoke(engine, [hashSource, _budget])!)
            {
                RequireValid(hashProgram);
                var run = hashProgram.GetType().GetMethods().Single(m => m.Name == "Run" && m.GetParameters().Length == 1);
                using var result = (IDisposable)run.Invoke(hashProgram, [_host])!;
                RequireSuccess(result);
                var values = (IEnumerable)result.GetType().GetProperty("ReturnValue")!.GetValue(result)!;
                Hashes = values.Cast<object>().Select(v => ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture)).ToArray();
            }
            if (Balance != 0) throw new InvalidDataException("Witness ownership did not end.");
            _script = compile.Invoke(engine, [item.GetProperty("source").GetString(), _budget])!;
            RequireValid(_script);
            _run = _script.GetType().GetMethods().Single(m => m.Name == "Run" && m.GetParameters().Length == 1);
            _runAsync = _script.GetType().GetMethods().Single(m => m.Name == "RunAsync" && m.GetParameters().Length == 1);
            _asyncResult = _runAsync.ReturnType.GetProperty("Result")!;
            _stdout = item.GetProperty("expectedOutput").GetString()!;
            _fileGolden = item.GetProperty("expectedFilesSha256").GetString()!;
            Files = Directory.EnumerateFiles(directory, "Lokad.*.dll").Order().ToDictionary(p => Path.GetFileName(p),
                p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        }
        catch { Dispose(); throw; }
    }

    private long Balance => (long)_balance.GetValue(_budget)!;
    public void FreezeBalance() => _expected = Balance;
    public void Measure(string phase)
    {
        if (phase == "warm") { Once(); return; }
        var original = _script;
        var expected = _expected;
        using (var fresh = (IDisposable)_compile.Invoke(_engine, [_source, _budget])!)
        {
            RequireValid(fresh);
            if (phase == "fresh")
            {
                _script = fresh;
                _expected = -1;
                try { Once(); }
                finally { _script = original; _expected = expected; }
            }
        }
        if (expected >= 0 && Balance != expected) throw new InvalidDataException("Fresh ownership did not end.");
    }
    public void Once()
    {
        _reset.Invoke(_host, null);
        var started = Stopwatch.GetTimestamp();
        IDisposable completed;
        if (_asynchronous)
        {
            var pending = (Task)_runAsync.Invoke(_script, [_host])!;
            pending.GetAwaiter().GetResult();
            completed = (IDisposable)_asyncResult.GetValue(pending)!;
        }
        else completed = (IDisposable)_run.Invoke(_script, [_host])!;
        PhaseTicks += Stopwatch.GetTimestamp() - started;
        using (var result = completed)
        {
            RequireSuccess(result);
            if (_capturePeak)
            {
                PeakExecutionBytes = (long)result.GetType().GetProperty("PeakExecutionMemoryBytes")!.GetValue(result)!;
                PeakRootBytes = (long)_budget.GetType().GetProperty("PeakBytes")!.GetValue(_budget)!;
                _capturePeak = false;
            }
            if ((string)result.GetType().GetProperty("StandardOutput")!.GetValue(result)! != _stdout ||
                (string)result.GetType().GetProperty("StandardError")!.GetValue(result)! != "")
                throw new InvalidDataException("Complete output golden failed.");
            if ((string)_digest.Invoke(null, [_outputs.Invoke(_host, null)])! != _fileGolden)
                throw new InvalidDataException("Complete file golden failed.");
        }
        if (_expected >= 0 && Balance != _expected) throw new InvalidDataException("Execution ownership did not end.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            (_script as IDisposable)?.Dispose();
            if (Balance != 0) throw new InvalidDataException("Compiled owner did not end.");
        }
        finally
        {
            _validate.Invoke(null, [_owned]);
            if (Directory.Exists(_owned)) Directory.Delete(_owned, recursive: true);
        }
    }
    private static void RequireValid(object script)
    {
        if (!(bool)script.GetType().GetProperty("IsValid")!.GetValue(script)!) throw new InvalidDataException("Invalid control program.");
    }
    private static void RequireSuccess(object result)
    {
        if (!(bool)result.GetType().GetProperty("Success")!.GetValue(result)!) throw new InvalidDataException("Control invocation failed.");
    }
}

sealed class ImageContext(string directory, string name) : AssemblyLoadContext(name, isCollectible: false)
{
    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is "System" or "netstandard" || name.Name!.StartsWith("System.", StringComparison.Ordinal)) return null;
        var path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
