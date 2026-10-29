using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;

// Frozen, success-only experiment. Real internal phases through reflection;
// no instrumentation, policy or alternative execution backend in production.
var benchmarkPath = Path.GetFullPath(args[0]);
var catalogPath = Path.GetFullPath(args[1]);
var outputPath = Path.GetFullPath(args[2]);
if (File.Exists(outputPath)) throw new IOException("Preserve existing observations.");
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(Path.GetDirectoryName(benchmarkPath)!, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var benchmark = AssemblyLoadContext.Default.LoadFromAssemblyPath(benchmarkPath);
var core = typeof(LythonEngine).Assembly;
const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
Type Type(string name) => core.GetType("Lokad.Lython." + name, true)!;
MethodInfo Method(Type type, string name, int count) => type.GetMethods(all).Single(m => m.Name == name && m.GetParameters().Length == count);
object? Get(object owner, string property) => owner.GetType().GetProperty(property, all)!.GetValue(owner);
var runtime = Type("Runtime.LythonRuntime");
var contextType = Type("Runtime.LythonRuntime+ExecutionContext");
var stateType = Type("Runtime.ExecutionState");
var hop = Method(runtime, "RunOnDedicatedStack", 1).CreateDelegate<Func<Func<LythonExecutionResult>, LythonExecutionResult>>();
var execute = Method(runtime, "ExecuteExecutableCodeObject", 2);
var annotate = Method(runtime, "InitializeModuleAnnotations", 2);
var success = Method(runtime, "CreateSuccessfulResult", 3);
var closeWriters = Method(stateType, "CloseOpenFileWriters", 1);
var closeHashes = Method(stateType, "CloseHashes", 0);
var own = Method(typeof(LythonExecutionResult), "OwnMemoryScope", 1);
var withOwners = Method(typeof(LythonRunOptions), "WithMemoryOwners", 2);
var fixtureType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileFixture", true)!;
var fileHostType = benchmark.GetType("Lokad.Lython.Benchmarks.Comparison.ComparisonFileHost", true)!;
var parseFixture = Method(fixtureType, "Parse", 1);
var reset = Method(fileHostType, "Reset", 0);
var outputs = Method(fileHostType, "ReadOutputs", 0);
var rows = new List<object>();
using var catalog = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().Where(c =>
    c.GetProperty("id").GetString() is "scripts.csv-summary-helpers.large" or "scripts.text-summary-helpers.large" or
        "scripts.json-summary-helpers.large" or "scripts.config-edit.small" or
        "scripts.file-manifest-helpers.large" or "scripts.file-search.large").ToArray();
if (cases.Length != 6) throw new ArgumentException("Expected six existing representative sources.");
foreach (var item in cases)
{
    var source = item.GetProperty("source").GetString()!;
    var expected = item.GetProperty("expectedOutput").GetString()!;
    var fixture = parseFixture.Invoke(null, [item.GetProperty("fixtureJson").GetString()])!;
    var fixtureDirectory = Path.Combine(Path.GetTempPath(), "lython-comparison-files-" + Guid.NewGuid().ToString("N"));
    var inner = (ILythonHost)Activator.CreateInstance(fileHostType, all, null, [fixture, fixtureDirectory], null)!;
    var host = new MeasuredHost(inner);
    var budget = new LythonMemoryBudget();
    using var script = new LythonEngine().Compile(source, budget);
    if (!script.IsValid) throw new InvalidOperationException(string.Join("; ", script.Diagnostics.Select(x => x.Message)));
    var baseline = budget.ReservedBytes;
    var runner = typeof(LythonCompiledScript).GetField("_runner", all)!.GetValue(script) as Delegate;
    var executable = runner!.Target!.GetType().GetField("executableScript", all)!.GetValue(runner.Target)!;
    var entry = Get(executable, "EntryPoint");
    var annotationSpan = Get(executable, "AnnotationInitializationSpan");
    var options = new LythonRunOptions { MemoryBudget = budget };
    var observations = new List<object>();

    void Verify(LythonExecutionResult result)
    {
        if (!result.Success || result.ReturnValue is not null || result.ExitCode is not null ||
            result.StandardOutput != expected || result.StandardError != "")
            throw new InvalidDataException(JsonSerializer.Serialize(result));
        outputs.Invoke(inner, null); // Existing complete byte/file golden checks.
    }
    void Baseline()
    {
        if (budget.ReservedBytes != baseline) throw new InvalidOperationException("Execution/result owner leaked.");
    }
    void Phases(int iteration, bool record)
    {
        reset.Invoke(inner, null);
        host.Reset(false);
        var phases = new Dictionary<string, Counter>();
        var publicStart = Stopwatch.GetTimestamp();
        var executionOwner = budget.CreateScope();
        var resultOwner = budget.CreateScope();
        try
        {
            var effective = withOwners.Invoke(options, [executionOwner, resultOwner]);
            var enqueued = Stopwatch.GetTimestamp();
            var result = hop(() => {
                phases["handoff"] = new(Stopwatch.GetElapsedTime(enqueued).TotalMicroseconds, null);
                var setup = Measure(() => {
                    var context = Activator.CreateInstance(contextType, all, null, [host, effective, null], null)!;
                    annotate.Invoke(null, [annotationSpan, context]);
                    return context;
                });
                phases["context"] = setup.Counter;
                var context = setup.Value;
                var state = Get(context, "State")!;
                try
                {
                    phases["executionInclusive"] = Measure(() => execute.Invoke(null, [entry, context])).Counter;
                    phases["publishWriters"] = Measure(() => closeWriters.Invoke(state, [context])).Counter;
                    var projection = Measure(() => (LythonExecutionResult)success.Invoke(null, [context, null, effective])!);
                    phases["projection"] = projection.Counter;
                    return projection.Value;
                }
                finally { phases["hashCleanup"] = Measure(() => closeHashes.Invoke(state, null)).Counter; }
            });
            own.Invoke(result, [resultOwner]);
            // End the execution owner at the same actual boundary as public Run.
            phases["executionOwnerCleanup"] = Measure(() => { executionOwner.Dispose(); return 0; }).Counter;
            var beforeVerification = Stopwatch.GetElapsedTime(publicStart).TotalMicroseconds;
            var retained = budget.ReservedBytes - baseline;
            Verify(result);
            phases["resultDisposal"] = Measure(() => { result.Dispose(); return 0; }).Counter;
            Baseline();
            if (record) observations.Add(new { iteration, kind = "reflected synchronous phases", phases,
                totalBeforeVerificationUs = beforeVerification, host = host.Snapshot(), retainedAfterExecutionBytes = retained,
                accountingAfterCleanup = budget.ReservedBytes });
        }
        finally { executionOwner.Dispose(); resultOwner.Dispose(); }
    }
    async Task Public(int iteration, string lane, bool record)
    {
        reset.Invoke(inner, null);
        host.Reset(lane == "async-delayed");
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var taskIncomplete = false;
        LythonExecutionResult result;
        if (lane == "sync") result = script.Run(host, options);
        else
        {
            var task = script.RunAsync(host, options);
            taskIncomplete = !task.IsCompleted;
            result = await task.ConfigureAwait(false);
        }
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMicroseconds;
        var bytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        var retained = budget.ReservedBytes - baseline;
        Verify(result);
        var dispose = Measure(() => { result.Dispose(); return 0; });
        Baseline();
        if (lane == "async-delayed" && host.Delays != 1) throw new InvalidOperationException("No actual host suspension.");
        if (record) observations.Add(new { iteration, kind = "public " + lane, totalBeforeVerificationUs = elapsed,
            processAllocatedBytes = bytes, taskIncomplete, host = host.Snapshot(), retainedAfterExecutionBytes = retained,
            resultDisposal = dispose.Counter, accountingAfterCleanup = budget.ReservedBytes });
    }
    try
    {
        for (var i = 0; i < 8; i++)
        {
            Phases(i, false);
            foreach (var lane in new[] { "sync", "async-completed", "async-delayed" }) await Public(i, lane, false);
        }
        for (var i = 0; i < 16; i++)
        {
            Phases(i, true);
            // Rotate the three public lanes; retain every observation, including slow ones.
            var lanes = new[] { "sync", "async-completed", "async-delayed" };
            for (var j = 0; j < 3; j++) await Public(i, lanes[(i + j) % lanes.Length], true);
        }
        rows.Add(new { id = item.GetProperty("id").GetString(), compilationRetainedBytes = baseline, observations });
    }
    finally
    {
        script.Dispose();
        if (budget.ReservedBytes != 0) throw new InvalidOperationException("Compilation owner leaked.");
        // A freshly generated fixed-prefix temp directory, only its known job tree.
        if (Path.GetFileName(fixtureDirectory).StartsWith("lython-comparison-files-", StringComparison.Ordinal) &&
            Path.GetDirectoryName(fixtureDirectory) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
            Directory.Delete(fixtureDirectory, recursive: true);
        else throw new InvalidOperationException("Fixture containment changed.");
    }
}
File.WriteAllText(outputPath, JsonSerializer.Serialize(new {
    performanceQualified = false, dateUtc = DateTimeOffset.UtcNow,
    boundary = "Six unchanged complete workloads. Success-only reflected synchronous phases use normal worker, executable code, owners, initialization, publication and projection. Public lanes use unmodified API; async-delayed delays exactly the first I/O by Task.Delay(2), awaits it, then does the same host operation. Reset/verification excluded from these diagnostic clocks.",
    limitations = "Reflection/decorator/stopwatch overhead included. Phase allocation is current-thread sync only; public allocation is process-wide and may include unrelated runtime work. Host elapsed is nested inside execution/publication; subtract only matching per-observation sums, never medians. No CPU-share, startup, CPython comparison or stable performance claim from this experiment.",
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    coreVersion = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    coreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(core.Location))),
    benchmarkVersion = benchmark.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    catalogSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(catalogPath))),
    warmupsPerLane = 8, observationsPerLane = 16, rows,
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");

static (T Value, Counter Counter) Measure<T>(Func<T> operation)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var started = Stopwatch.GetTimestamp();
    var value = operation();
    return (value, new(Stopwatch.GetElapsedTime(started).TotalMicroseconds,
        GC.GetAllocatedBytesForCurrentThread() - allocated));
}
internal sealed record Counter(double Microseconds, long? AllocatedBytes);

// The same contained filesystem host, decorated only for this diagnostic.
// Every operation is awaited to completion; there is no background I/O.
internal sealed class MeasuredHost(ILythonHost inner) : ILythonHost, ILythonSynchronousHostCapability
{
    private long _ticks;
    private long _delayTicks;
    private int _calls;
    private bool _delay;
    internal int Delays { get; private set; }
    public bool CompletesSynchronously => !_delay;
    public string Cwd => inner.Cwd;
    public DateTimeOffset LocalNow => inner.LocalNow;
    public DateTimeOffset UtcNow => inner.UtcNow;
    internal void Reset(bool delay) { _ticks = _delayTicks = 0; _calls = Delays = 0; _delay = delay; }
    internal object Snapshot() => new { calls = _calls, delays = Delays,
        elapsedUs = _ticks * 1_000_000.0 / Stopwatch.Frequency,
        suspendedDelayUs = _delayTicks * 1_000_000.0 / Stopwatch.Frequency };
    private async ValueTask<T> Call<T>(Func<ValueTask<T>> operation)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            _calls++;
            if (_delay && Delays == 0)
            {
                Delays++;
                var pause = Stopwatch.GetTimestamp();
                await Task.Delay(2).ConfigureAwait(false);
                _delayTicks += Stopwatch.GetTimestamp() - pause;
            }
            return await operation().ConfigureAwait(false);
        }
        finally { _ticks += Stopwatch.GetTimestamp() - started; }
    }
    private async ValueTask Call(Func<ValueTask> operation)
        => await Call(async () => { await operation().ConfigureAwait(false); return 0; }).ConfigureAwait(false);
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token) => Call(() => inner.ReadTextUtf8Async(path, token));
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, CancellationToken token) => Call(() => inner.ReadTextUtf8RangeAsync(path, offset, count, token));
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken token) => Call(() => inner.ReadBytesAsync(path, token));
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path, long offset, int count, CancellationToken token) => Call(() => inner.ReadBytesRangeAsync(path, offset, count, token));
    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Call(() => inner.WriteTextUtf8Async(path, bytes, token));
    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Call(() => inner.AppendTextUtf8Async(path, bytes, token));
    public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Call(() => inner.WriteBytesAsync(path, bytes, token));
    public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Call(() => inner.AppendBytesAsync(path, bytes, token));
    public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => Call(() => inner.ExistsAsync(path, token));
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => Call(() => inner.ListDirAsync(path, token));
    public ValueTask MkDirAsync(string path, CancellationToken token) => Call(() => inner.MkDirAsync(path, token));
    public ValueTask RemoveAsync(string path, CancellationToken token) => Call(() => inner.RemoveAsync(path, token));
    public ValueTask CopyAsync(string source, string destination, CancellationToken token) => Call(() => inner.CopyAsync(source, destination, token));
    public ValueTask MoveAsync(string source, string destination, CancellationToken token) => Call(() => inner.MoveAsync(source, destination, token));
    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => Call(() => inner.StatAsync(path, token));
}
