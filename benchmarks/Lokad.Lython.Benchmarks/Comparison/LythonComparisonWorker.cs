using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed class LythonComparisonWorker(ComparisonManifest manifest)
{
    private readonly LythonEngine _engine = new();
    private readonly ComparisonHost _host = new();
    private readonly Dictionary<string, LythonCompiledScript> _compiled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    private int _lastRequestId;

    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
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
            for (; completed < 2; completed++) actual = Invoke(script, workload);
            _compiled[workload.Id] = script;
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
            || lane is not ("warm" or "compile-run" or "compile"))
            throw new InvalidDataException("A batch needs a verified case, supported lane and bounded positive count.");
        var completed = 0;
        string? actual = null;
        LythonCompiledScript? lastCompiled = null;
        var started = Stopwatch.GetTimestamp();
        try
        {
            for (; completed < iterations; completed++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Stopwatch.GetElapsedTime(started).TotalSeconds >= ComparisonProtocol.MaximumBatchSeconds)
                    throw new WorkerJobException("Timeout", "Batch exceeded its internal 60-second ceiling.");
                var script = lane == "warm" ? _compiled[workload.Id] : Compile(workload);
                if (lane == "compile") lastCompiled = script;
                else actual = Invoke(script, workload);
            }
            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            // Retain the final compiled object; hashing/artifact encoding stays
            // outside the clock. No forced collections or result subtraction.
            if (lastCompiled is not null) _compiled[workload.Id] = lastCompiled;
            return Response(workload, requestId, "Completed", completed, elapsedTicks, actual);
        }
        catch (WorkerJobException failure)
        {
            _verified.Remove(workload.Id);
            return Response(workload, requestId, failure.Status, completed, null, failure.ActualOutput, failure.Message);
        }
    }

    private LythonCompiledScript Compile(ComparisonWorkload workload)
    {
        var script = _engine.Compile(workload.Source);
        if (!script.IsValid)
            throw new WorkerJobException("Unsupported", string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        return script;
    }

    private string Invoke(LythonCompiledScript script, ComparisonWorkload workload)
    {
        // Default per-invocation public options/context/capture/projection stay
        // inside timing. The host exposes no ambient IO or subprocess capability.
        var result = script.Run(_host);
        if (!result.Success)
            throw new WorkerJobException(result.DeniedReservationBytes > 0 || result.Failure?.ExceptionType == "MemoryError"
                ? "BudgetDenied" : "Failure", result.Failure?.Message ?? "Lython execution failed.", result.StandardOutput);
        if (result.ExitCode is not null || result.ReturnValue is not null || result.StandardError.Length != 0
            || !string.Equals(result.StandardOutput, workload.ExpectedOutput, StringComparison.Ordinal))
            throw new WorkerJobException("Mismatch", "Complete public result differs from the independent golden.", result.StandardOutput);
        return result.StandardOutput;
    }

    private WorkerResponse Response(ComparisonWorkload workload, int requestId, string status, int completed,
        long? elapsedTicks, string? actual, string? reason = null)
        => new(ComparisonProtocol.Version, requestId, workload.Id, status, completed, elapsedTicks,
            Stopwatch.Frequency, workload.SourceSha256, workload.FixtureSha256, workload.ExpectedOutputSha256,
            actual is null ? null : ComparisonProtocol.Digest(actual), reason);

    internal static object CreateIdentity(ComparisonManifest manifest, bool includeFileDigests = true)
    {
        var library = typeof(LythonEngine).Assembly;
        var adapter = typeof(LythonComparisonWorker).Assembly;
        var core = typeof(object).Assembly;
        var variableNames = new[] { "gcServer", "GCHeapCount", "GCHeapHardLimit", "GCHeapHardLimitPercent",
            "TieredCompilation", "TieredPGO", "TC_QuickJit", "TC_QuickJitForLoops", "ReadyToRun" };
        var overrides = variableNames.SelectMany(name => new[] { "DOTNET_" + name, "COMPlus_" + name })
            .Append("DOTNET_PROCESSOR_COUNT").ToDictionary(name => name, Environment.GetEnvironmentVariable);
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if ((name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase))
                && name is not ("DOTNET_ROOT" or "DOTNET_ROOT_X64" or "DOTNET_CLI_TELEMETRY_OPTOUT" or "DOTNET_NOLOGO")
                && !overrides.ContainsKey(name))
                overrides.Add(name, "<present>"); // Unknown settings reject the primary profile; do not expose their values.
        }
        return new
        {
            protocolVersion = ComparisonProtocol.Version, status = "Ready", engine = "Lython",
            catalogSha256 = manifest.Sha256, catalogVersion = WorkloadCatalog.Version,
            processId = Environment.ProcessId, runtime = RuntimeInformation.FrameworkDescription,
            runtimeVersion = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            platform = RuntimeInformation.OSDescription, processorCount = Environment.ProcessorCount,
            serverGc = GCSettings.IsServerGC, gcLatencyMode = GCSettings.LatencyMode.ToString(),
            runtimeOverrides = overrides, clockFrequency = Stopwatch.Frequency, clockHighResolution = Stopwatch.IsHighResolution,
            maximumFrameBytes = ComparisonProtocol.MaximumFrameBytes,
            maximumBatchIterations = ComparisonProtocol.MaximumBatchIterations,
            maximumBatchSeconds = ComparisonProtocol.MaximumBatchSeconds,
            publicLimits = "ordinary defaults; instruction fuel unset; no forced GC",
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

    private sealed class WorkerJobException(string status, string message, string? actualOutput = null) : Exception(message)
    {
        public string Status => status;
        public string? ActualOutput => actualOutput;
    }
}

internal sealed record WorkerResponse(int ProtocolVersion, int RequestId, string CaseId, string Status,
    int CompletedInvocations, long? ElapsedTicks, long ClockFrequency, string SourceSha256, string FixtureSha256,
    string ExpectedOutputSha256, string? ActualOutputSha256, string? Reason);

internal sealed class ComparisonHost : ILythonHost
{
    public string Cwd => "/";
    public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public DateTimeOffset UtcNow => LocalNow;
    private static LythonHostCapabilityUnavailableException Denied() => new("benchmark host IO");
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw Denied();
    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw Denied();
    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) => throw Denied();
    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) => throw Denied();
    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) => throw Denied();
    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
}
