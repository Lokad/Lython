using System.Diagnostics;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record FreshProcessObservation(string Status, long? ElapsedTicks, long ClockFrequency,
    WorkerResponse Response, JsonElement Identity);

internal static class FreshProcessRunner
{
    public static async Task<FreshProcessObservation> RunAsync(WorkerLaunch launch, ComparisonManifest manifest,
        JsonElement preparedIdentity, CancellationToken cancellationToken = default, TimeSpan? deadline = null)
    {
        if (manifest.Cases.Count != 1) throw new ArgumentException("Fresh-process timing requires exactly one immutable case.");
        var budget = deadline ?? ComparisonWorkerClient.RequestDeadline;
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(deadline));
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(budget);
        OwnedWorkerProcess? process = null;
        Task<byte[]>? stdout = null, stderr = null;
        Exception? streamFailure = null;
        void FailStream(Exception failure)
        {
            Interlocked.CompareExchange(ref streamFailure, failure, null);
            bounded.Cancel();
        }
        var started = Stopwatch.GetTimestamp();
        long elapsed;
        int exitCode;
        try
        {
            process = await OwnedWorkerProcess.StartAsync(launch, bounded.Token).ConfigureAwait(false);
            stdout = DrainAsync(process.Output, ComparisonProtocol.MaximumFrameBytes + 4, FailStream);
            stderr = DrainAsync(process.Error, 64 * 1024, FailStream);
            process.Input.Dispose(); // File payload; no stdin exchange in this lane.
            await Task.WhenAll(stdout, stderr, process.Exit).WaitAsync(bounded.Token).ConfigureAwait(false);
            elapsed = Stopwatch.GetTimestamp() - started;
            exitCode = await process.Exit.ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            var transportFailure = Volatile.Read(ref streamFailure);
            if (process is not null) await process.DisposeAsync().ConfigureAwait(false);
            if (stdout is not null && stderr is not null)
            {
                try { await Task.WhenAll(stdout, stderr).WaitAsync(ComparisonWorkerClient.ShutdownDeadline).ConfigureAwait(false); }
                catch when (stdout.IsCompleted && stderr.IsCompleted) { /* Observe every pump after stopping the owned scope. */ }
                if (transportFailure is not null) throw new IOException("Fresh worker stream failed.", transportFailure);
            }
            if (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                throw new TimeoutException("Fresh worker exceeded its launch-to-drain deadline.");
            throw;
        }
        finally
        {
            if (process is not null) await process.DisposeAsync().ConfigureAwait(false);
        }
        // The parent timer has stopped. Strict decoding, identity/comparator
        // validation, full binary hashing and receipt writes are outside it.
        if (exitCode != 0 || ComparisonProtocol.Utf8.GetString(await stderr!.ConfigureAwait(false)).Length != 0)
            throw new InvalidDataException("Fresh worker exited nonzero or emitted unexpected stderr.");
        using var bytes = new MemoryStream(await stdout!.ConfigureAwait(false));
        using var frame = await ComparisonProtocol.ReadAsync(bytes, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Missing fresh worker result.");
        using var extra = await ComparisonProtocol.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (extra is not null) throw new InvalidDataException("Fresh worker emitted more than one result.");
        var root = frame.RootElement;
        var identity = root.GetProperty("identity");
        ValidateIdentity(identity, preparedIdentity, manifest, process!.ProcessId);
        var workload = manifest.Cases.Values.Single();
        var row = root.GetProperty("response");
        foreach (var name in new[] { "status", "completedInvocations", "elapsedTicks", "actualOutputSha256", "reason" })
            _ = row.GetProperty(name);
        var response = row.Deserialize<WorkerResponse>(ComparisonProtocol.JsonOptions)
            ?? throw new InvalidDataException("Missing fresh worker response.");
        if (response.ProtocolVersion != ComparisonProtocol.Version || response.RequestId != 1
            || response.CaseId != workload.Id || response.SourceSha256 != workload.SourceSha256
            || response.FixtureSha256 != workload.FixtureSha256 || response.ExpectedOutputSha256 != workload.ExpectedOutputSha256
            || response.ElapsedTicks is not null || response.ClockFrequency != identity.GetProperty("clockFrequency").GetInt64())
            throw new InvalidDataException("Wrong fresh response identity, hashes or clock ownership.");
        if (response.Status == "Equivalent")
        {
            if (response.CompletedInvocations != 1 || response.ActualOutputSha256 != workload.ExpectedOutputSha256
                || response.Reason is not null || elapsed <= 0)
                throw new InvalidDataException("A fresh process must complete exactly one correct invocation.");
        }
        else if (response.Status is not ("Mismatch" or "Unsupported" or "BudgetDenied" or "Failure" or "Timeout")
            || response.CompletedInvocations != 0)
            throw new InvalidDataException("Invalid fresh failure result.");
        return new FreshProcessObservation(response.Status, response.Status == "Equivalent" ? elapsed : null,
            Stopwatch.Frequency, response, identity.Clone());
    }

    private static async Task<byte[]> DrainAsync(Stream input, int maximumBytes, Action<Exception> failureSignal)
    {
        try
        {
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await input.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + count > maximumBytes) throw new InvalidDataException("Fresh worker stream exceeds its declared bound.");
                bytes.Write(buffer, 0, count);
            }
            return bytes.ToArray();
        }
        catch (Exception failure) { failureSignal(failure); throw; }
    }

    internal static void ValidateIdentity(JsonElement actual, JsonElement expected, ComparisonManifest manifest, int processId)
        => ValidateIdentity(actual, expected, manifest.Sha256, processId);

    internal static void ValidateIdentity(JsonElement actual, JsonElement expected, string catalogSha256, int processId)
    {
        foreach (var name in new[] { "protocolVersion", "status", "engine", "catalogVersion", "clockFrequency",
            "maximumFrameBytes", "maximumBatchIterations", "maximumBatchSeconds" })
            if (actual.GetProperty(name).GetRawText() != expected.GetProperty(name).GetRawText())
                throw new InvalidDataException("Fresh worker identity differs: " + name);
        if (actual.GetProperty("processId").GetInt32() != processId
            || actual.GetProperty("catalogSha256").GetString() != catalogSha256)
            throw new InvalidDataException("Wrong fresh process or payload identity.");
        if (actual.GetProperty("engine").GetString() == "Lython")
        {
            if (actual.GetProperty("benchmarkDotNetLoaded").GetBoolean())
                throw new InvalidDataException("Fresh comparison startup initialized BenchmarkDotNet.");
            foreach (var name in new[] { "runtimeVersion", "processorCount", "serverGc", "gcLatencyMode", "runtimeOverrides", "publicLimits" })
                if (actual.GetProperty(name).GetRawText() != expected.GetProperty(name).GetRawText())
                    throw new InvalidDataException("Fresh Lython runtime differs: " + name);
            var libraries = actual.GetProperty("libraries").EnumerateArray().ToArray();
            var prepared = expected.GetProperty("libraries").EnumerateArray().ToArray();
            if (libraries.Length != prepared.Length) throw new InvalidDataException("Wrong fresh Lython library count.");
            for (var i = 0; i < libraries.Length; i++)
            {
                if (libraries[i].GetProperty("sha256").ValueKind != JsonValueKind.Null)
                    throw new InvalidDataException("Fresh worker performed a full binary hash inside startup.");
                foreach (var name in new[] { "path", "moduleId", "version", "configuration", "buildSdk" })
                    if (libraries[i].GetProperty(name).GetRawText() != prepared[i].GetProperty(name).GetRawText())
                        throw new InvalidDataException("Fresh Lython library identity differs: " + name);
            }
        }
        else if (actual.GetProperty("engine").GetString() == "CPython")
        {
            foreach (var name in new[] { "version", "executable", "gcEnabled", "gilEnabled", "captureByteLimit" })
                if (actual.GetProperty(name).GetRawText() != expected.GetProperty(name).GetRawText())
                    throw new InvalidDataException("Fresh CPython runtime differs: " + name);
            if (!actual.GetProperty("isolated").GetBoolean() || !actual.GetProperty("noSite").GetBoolean()
                || actual.TryGetProperty("executableSha256", out _) || actual.TryGetProperty("adapterSha256", out _))
                throw new InvalidDataException("Fresh CPython isolation or lightweight provenance differs.");
        }
        else if (actual.GetProperty("engine").GetString() != "Fixture")
            throw new InvalidDataException("Unsupported fresh worker engine.");
    }
}
