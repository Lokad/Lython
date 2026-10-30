using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed class LythonComparisonWorker(ComparisonManifest manifest, string? fileDirectory = null)
{
    private readonly LythonEngine _engine = new(Utf8RegexProvider.Instance);
    private readonly ComparisonHost _host = new();
    private readonly Dictionary<string, LythonCompiledScript> _compiled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComparisonFileHost> _files = new(StringComparer.Ordinal);
    internal LythonMemoryBudget MemoryBudget { get; } = new();
    private LythonExecutionResult? _lastResult;
    private IReadOnlyDictionary<string, byte[]>? _lastFiles;
    private int _lastRequestId;

    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        try { await ProcessRequestsAsync(input, output, cancellationToken).ConfigureAwait(false); }
        finally
        {
            _lastResult?.Dispose();
            _lastResult = null;
            foreach (var script in _compiled.Values) script.Dispose();
            _compiled.Clear();
        }
    }

    private async Task ProcessRequestsAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        await ComparisonProtocol.WriteAsync(output, CreateIdentity(manifest), cancellationToken).ConfigureAwait(false);
        while (true)
        {
            using var frame = await ComparisonProtocol.ReadAsync(input, cancellationToken).ConfigureAwait(false);
            if (frame is null) return;
            var request = frame.RootElement;
            var requestId = request.GetProperty("requestId").GetInt32();
            if (request.GetProperty("protocolVersion").GetInt32() != ComparisonProtocol.Version
                || requestId <= _lastRequestId)
                throw new InvalidDataException("Invalid protocol version or request sequence.");
            _lastRequestId = requestId;
            var operation = request.GetProperty("operation").GetString();
            if (operation == "quit")
            {
                await ComparisonProtocol.WriteAsync(output, new
                {
                    protocolVersion = ComparisonProtocol.Version, requestId, status = "Closed",
                }, cancellationToken).ConfigureAwait(false);
                return;
            }
            if (operation is not ("verify" or "batch"))
                throw new InvalidDataException("Unknown comparison operation.");
            var id = request.GetProperty("caseId").GetString()!;
            if (!manifest.Cases.TryGetValue(id, out var workload)
                || request.GetProperty("sourceSha256").GetString() != workload.SourceSha256
                || request.GetProperty("fixtureSha256").GetString() != workload.FixtureSha256
                || request.GetProperty("expectedOutputSha256").GetString() != workload.ExpectedOutputSha256)
                throw new InvalidDataException("Wrong comparison case or source/fixture/output digest.");
            var response = operation == "verify" ? Verify(workload, requestId)
                : Batch(workload, requestId, request.GetProperty("lane").GetString()!,
                    request.GetProperty("iterations").GetInt32(), cancellationToken);
            await ComparisonProtocol.WriteAsync(output, response, cancellationToken).ConfigureAwait(false);
            // The response contains hashes and a consumed failure message, not
            // a result payload. Wait for its final borrower before refunding.
            _lastResult?.Dispose();
            _lastResult = null;
        }
    }

    private WorkerResponse Verify(ComparisonWorkload workload, int requestId)
    {
        var completed = 0;
        string? actual = null;
        try
        {
            // Reuse one compilation and prove fresh state twice before a batch.
            var script = _compiled.TryGetValue(workload.Id, out var existing) ? existing : Compile(workload);
            _compiled[workload.Id] = script;
            var files = FileHost(workload);
            for (; completed < 2; completed++)
            {
                actual = null;
                actual = Invoke(script, workload, files);
            }
            _verified.Add(workload.Id);
            return Response(workload, requestId, "Equivalent", completed, null, actual);
        }
        catch (WorkerJobException failure)
        {
            _verified.Remove(workload.Id);
            return Response(workload, requestId, failure.Status, completed, null, failure.ActualOutput, failure.Message);
        }
    }

    private WorkerResponse Batch(ComparisonWorkload workload, int requestId, string lane, int iterations,
        CancellationToken cancellationToken)
    {
        if (!_verified.Contains(workload.Id) || iterations is < 1 or > ComparisonProtocol.MaximumBatchIterations
            || lane is not ("warm" or "warm-async" or "compile-run" or "compile"))
            throw new InvalidDataException("A batch needs a verified case, supported lane and bounded positive count.");
        var completed = 0;
        string? actual = null;
        LythonCompiledScript? lastCompiled = null;
        var files = FileHost(workload);
        var started = Stopwatch.GetTimestamp();
        try
        {
            for (; completed < iterations; completed++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(started).TotalSeconds >= ComparisonProtocol.MaximumBatchSeconds)
                    throw new WorkerJobException("Timeout", "Batch exceeded its internal 60-second ceiling.");
                if (lane == "compile")
                {
                    var script = Compile(workload);
                    lastCompiled?.Dispose();
                    lastCompiled = script;
                }
                else if (lane == "compile-run")
                {
                    using var script = Compile(workload);
                    actual = null;
                    actual = Invoke(script, workload, files);
                }
                else
                {
                    actual = null;
                    actual = Invoke(_compiled[workload.Id], workload, files, asynchronous: lane == "warm-async");
                }
            }
            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            // Retain the final compiled object; hashing/artifact encoding stays
            // outside the clock. No forced collections or result subtraction.
            if (lastCompiled is not null)
            {
                _compiled[workload.Id].Dispose();
                _compiled[workload.Id] = lastCompiled;
                lastCompiled = null;
            }
            return Response(workload, requestId, "Completed", completed, elapsedTicks, actual);
        }
        catch (WorkerJobException failure)
        {
            _verified.Remove(workload.Id);
            return Response(workload, requestId, failure.Status, completed, null, failure.ActualOutput, failure.Message);
        }
        finally { lastCompiled?.Dispose(); }
    }

    private LythonCompiledScript Compile(ComparisonWorkload workload)
    {
        var script = _engine.Compile(workload.Source, MemoryBudget);
        if (!script.IsValid)
        {
            var message = string.Join(" | ", script.Diagnostics.Select(d => d.Message));
            script.Dispose();
            throw new WorkerJobException("Unsupported", message);
        }
        return script;
    }

    private ComparisonFileHost? FileHost(ComparisonWorkload workload)
    {
        if (_files.TryGetValue(workload.Id, out var existing)) return existing;
        if (workload.Files is not { } fixture) return null;
        var host = new ComparisonFileHost(fixture, fileDirectory ?? Environment.CurrentDirectory);
        _files.Add(workload.Id, host);
        return host;
    }

    private string Invoke(LythonCompiledScript script, ComparisonWorkload workload, ComparisonFileHost? files, bool asynchronous = false)
    {
        // Default per-invocation public options/context/capture/projection stay
        // inside timing. File cases expose only the declared fixture directory.
        _lastFiles = null;
        _lastResult?.Dispose();
        _lastResult = null;
        try { files?.Reset(); }
        catch (IOException error) { throw new WorkerJobException("Failure", "File fixture reset failed: " + error.Message); }
        var host = (ILythonHost?)files ?? _host;
        var result = asynchronous ? script.RunAsync(host).GetAwaiter().GetResult() : script.Run(host);
        _lastResult = result;
        if (!result.Success)
            throw new WorkerJobException(result.DeniedReservationBytes > 0 || result.Failure?.ExceptionType == "MemoryError"
                ? "BudgetDenied" : "Failure", result.Failure?.Message ?? "Lython execution failed.", result.StandardOutput);
        if (result.ExitCode is not null || result.ReturnValue is not null || result.StandardError.Length != 0
            || !string.Equals(result.StandardOutput, workload.ExpectedOutput, StringComparison.Ordinal))
            throw new WorkerJobException("Mismatch", "Complete public result differs from the independent golden.", result.StandardOutput);
        if (files is not null)
        {
            try { _lastFiles = files.ReadOutputs(); }
            catch (Exception error) when (error is IOException or InvalidDataException)
            { throw new WorkerJobException("Mismatch", error.Message, result.StandardOutput); }
        }
        return result.StandardOutput;
    }

    private WorkerResponse Response(ComparisonWorkload workload, int requestId, string status, int completed,
        long? elapsedTicks, string? actual, string? reason = null)
        => new(ComparisonProtocol.Version, requestId, workload.Id, status, completed, elapsedTicks,
            Stopwatch.Frequency, workload.SourceSha256, workload.FixtureSha256, workload.ExpectedOutputSha256,
            actual is null ? null : ComparisonProtocol.Digest(actual), reason,
            actual is not null && reason is null && _lastFiles is not null ? ComparisonFileFixture.Digest(_lastFiles) : null);

    internal static object CreateIdentity(ComparisonManifest manifest, bool includeFileDigests = true)
    {
        var library = typeof(LythonEngine).Assembly;
        var adapter = typeof(LythonComparisonWorker).Assembly;
        var core = typeof(object).Assembly;
        return new
        {
            protocolVersion = ComparisonProtocol.Version, status = "Ready", engine = "Lython",
            catalogSha256 = manifest.Sha256, catalogVersion = WorkloadCatalog.Version,
            processId = Environment.ProcessId, runtime = RuntimeInformation.FrameworkDescription,
            fileFixtureDirectory = manifest.Cases.Values.Any(c => c.Files is not null) ? Environment.CurrentDirectory : null,
            runtimeVersion = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            platform = RuntimeInformation.OSDescription, processorCount = Environment.ProcessorCount,
            serverGc = GCSettings.IsServerGC, gcLatencyMode = GCSettings.LatencyMode.ToString(),
            runtimeOverrides = RuntimeOverrides(), clockFrequency = Stopwatch.Frequency, clockHighResolution = Stopwatch.IsHighResolution,
            maximumFrameBytes = ComparisonProtocol.MaximumFrameBytes,
            maximumBatchIterations = ComparisonProtocol.MaximumBatchIterations,
            maximumBatchSeconds = ComparisonProtocol.MaximumBatchSeconds,
            publicLimits = "ordinary defaults; cooperative cancellation; no forced GC",
            ownershipBoundary = "shared unbounded budget; prior-result and per-invocation compilation cleanup timed; final result retained through response; cached compilations disposed on exit",
            benchmarkDotNetLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "BenchmarkDotNet"),
            libraries = new[] { Identity(library), Identity(adapter), Identity(core) },
        };

        object Identity(Assembly assembly) => new
        {
            path = assembly.Location,
            sha256 = includeFileDigests ? ComparisonProtocol.Digest(File.ReadAllBytes(assembly.Location)) : null,
            version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            buildSdk = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == "BuildSdkVersion")?.Value,
            moduleId = assembly.ManifestModule.ModuleVersionId,
        };
    }

    internal static readonly string[] RuntimeOverrideNames = new[] { "gcServer", "GCHeapCount", "GCHeapHardLimit", "GCHeapHardLimitPercent",
        "TieredCompilation", "TieredPGO", "TC_QuickJit", "TC_QuickJitForLoops", "ReadyToRun" }
        .SelectMany(name => new[] { "DOTNET_" + name, "COMPlus_" + name }).Append("DOTNET_PROCESSOR_COUNT").ToArray();

    internal static Dictionary<string, string?> RuntimeOverrides()
    {
        var overrides = RuntimeOverrideNames.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if ((name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase))
                && name is not ("DOTNET_ROOT" or "DOTNET_ROOT_X64" or "DOTNET_CLI_TELEMETRY_OPTOUT" or "DOTNET_NOLOGO")
                && !overrides.ContainsKey(name)) overrides.Add(name, "<present>");
        }
        return overrides;
    }

    private sealed class WorkerJobException(string status, string message, string? actualOutput = null) : Exception(message)
    {
        public string Status => status;
        public string? ActualOutput => actualOutput;
    }
}

internal sealed record WorkerResponse(int ProtocolVersion, int RequestId, string CaseId, string Status,
    int CompletedInvocations, long? ElapsedTicks, long ClockFrequency, string SourceSha256, string FixtureSha256,
    string ExpectedOutputSha256, string? ActualOutputSha256, string? Reason,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? ActualFilesSha256 = null);

internal sealed class ComparisonHost : ILythonHost
{
    public string Cwd => "/";
    public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public DateTimeOffset UtcNow => LocalNow;
    private static LythonHostCapabilityUnavailableException Denied() => new("benchmark host IO");
    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask MkDirAsync(LythonPathRequest path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask RemoveAsync(LythonPathRequest path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken cancellationToken) => throw Denied();
    public ValueTask MoveAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken cancellationToken) => throw Denied();
    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken cancellationToken)
        => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
}
