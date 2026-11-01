using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

var overrides = new[] { "DOTNET_gcServer", "COMPlus_gcServer", "DOTNET_GCHeapCount", "COMPlus_GCHeapCount", "DOTNET_GCHeapHardLimit", "COMPlus_GCHeapHardLimit", "DOTNET_GCHeapHardLimitPercent", "COMPlus_GCHeapHardLimitPercent", "DOTNET_TieredCompilation", "COMPlus_TieredCompilation", "DOTNET_TieredPGO", "COMPlus_TieredPGO", "DOTNET_TC_QuickJit", "COMPlus_TC_QuickJit", "DOTNET_TC_QuickJitForLoops", "COMPlus_TC_QuickJitForLoops", "DOTNET_ReadyToRun", "COMPlus_ReadyToRun", "DOTNET_PROCESSOR_COUNT" }.ToDictionary(n => n, Environment.GetEnvironmentVariable);
if (overrides.Values.Any(v => v is not null) || System.Runtime.GCSettings.IsServerGC) throw new InvalidDataException("Ordinary GC and tiering required.");
var path = Path.GetFullPath(args[0]);
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[1]));
using var witness = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var item = catalog.RootElement.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("id").GetString() == "scripts.csv-summary-helpers.large");
var expanded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.GetRawText())!;
expanded["source"] = item.GetProperty("expandedSource");
var expandedItem = JsonSerializer.SerializeToElement(expanded);
var hashSource = witness.RootElement.GetProperty("source").GetString()!;
using var before = new Image(path, item, hashSource, "original");
using var candidate = new Image(path, expandedItem, hashSource, "expanded");
using var control = new Image(path, item, hashSource, "original-control");
var images = new[] { before, candidate, control };
if (images.Select(x => x.Core).Distinct().Count() != 3 || images.Any(x => x.EngineType.BaseType!.Assembly != typeof(object).Assembly || !x.Hashes.SequenceEqual(before.Hashes) || JsonSerializer.Serialize(x.Files) != JsonSerializer.Serialize(before.Files))) throw new InvalidDataException("Image/hash mismatch.");
foreach (var image in images) image.Once();
for (var i = 0; i < 128; i++) foreach (var image in images) image.Once();
foreach (var image in images) image.FreezeBalance();
foreach (var image in images)
{
    Thread.Sleep(TimeSpan.FromSeconds(2));
    Console.WriteLine(JsonSerializer.Serialize(new { phase = "ready", label = image.Label, processId = Environment.ProcessId, files = image.Files, hashes = image.Hashes, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, normalGcAndTiering = true, overrides }));
    if (Console.ReadLine() != "go") throw new InvalidDataException("Expected capture command.");
    var clock = Stopwatch.StartNew();
    for (var i = 0; i < 384; i++) image.Once();
    clock.Stop();
    Console.WriteLine(JsonSerializer.Serialize(new { phase = "complete", label = image.Label, jobs = 384, seconds = clock.Elapsed.TotalSeconds, fullGoldensPassed = true, executionBalancesReturned = true }));
    if (Console.ReadLine() != "next") throw new InvalidDataException("Expected stopped acknowledgment.");
}
foreach (var image in images) image.Dispose();
Console.WriteLine(JsonSerializer.Serialize(new { phase = "closed", allBalancesZero = true, ownedFixturesRemoved = true, performanceQualified = false }));



sealed class Image : IDisposable
{
    private readonly object _budget;
    private readonly PropertyInfo _balance;
    private readonly object _host;
    private readonly MethodInfo _reset, _outputs, _digest, _validate;
    private readonly object _script;
    private readonly MethodInfo _run;
    private readonly string _owned, _stdout, _fileGolden;
    private long _expected = -1;
    private bool _disposed;
    public string Label { get; }
    public Assembly Core { get; }
    public Type EngineType { get; }
    public string[] Hashes { get; }
    public Dictionary<string, string> Files { get; }

    public Image(string path, JsonElement item, string hashSource, string label)
    {
        Label = label;
        var directory = Path.GetDirectoryName(path)!;
        var context = new ImageContext(directory, label);
        var benchmark = context.LoadFromAssemblyPath(path);
        Core = context.LoadFromAssemblyPath(Path.Combine(directory, "Lokad.Lython.dll"));
        EngineType = Core.GetType("Lokad.Lython.LythonEngine", true)!;
        var engine = Activator.CreateInstance(EngineType)!;
        var budgetType = Core.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
        _budget = Activator.CreateInstance(budgetType, [null])!;
        _balance = budgetType.GetProperty("ReservedBytes")!;
        var compile = EngineType.GetMethod("Compile", [typeof(string), Core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
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
            _stdout = item.GetProperty("expectedOutput").GetString()!;
            _fileGolden = item.GetProperty("expectedFilesSha256").GetString()!;
            Files = Directory.EnumerateFiles(directory, "Lokad.*.dll").Order().ToDictionary(p => Path.GetFileName(p),
                p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        }
        catch { Dispose(); throw; }
    }

    private long Balance => (long)_balance.GetValue(_budget)!;
    public void FreezeBalance() => _expected = Balance;
    public void Once()
    {
        _reset.Invoke(_host, null);
        using (var result = (IDisposable)_run.Invoke(_script, [_host])!)
        {
            RequireSuccess(result);
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
