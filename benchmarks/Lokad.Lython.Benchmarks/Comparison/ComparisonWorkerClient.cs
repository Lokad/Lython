using System.Text.Json;
using System.Threading.Channels;

namespace Lokad.Lython.Benchmarks.Comparison;

// The parent clock is a watchdog, not a warm-lane sample. Warm timing comes
// only from a complete, independently validated worker response.
internal sealed class ComparisonWorkerClient : IAsyncDisposable
{
    public static readonly TimeSpan StartupDeadline = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(65);
    public static readonly TimeSpan ShutdownDeadline = TimeSpan.FromSeconds(5);
    private const int MaximumErrorBytes = 64 * 1024;
    private readonly OwnedWorkerProcess _process;
    private readonly Channel<JsonDocument> _frames = Channel.CreateBounded<JsonDocument>(new BoundedChannelOptions(1)
    {
        SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait,
    });
    private readonly CancellationTokenSource _transportFailure = new();
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    private readonly Task _outputPump;
    private readonly Task _errorPump;
    private Exception? _failure;
    private int _expectedFrames = 1;
    private int _requestId;
    private bool _closed;
    private bool _disposed;
    private long _clockFrequency;

    public JsonElement Identity { get; private set; }
    public int ProcessId => _process.ProcessId;
    public string StandardError { get; private set; } = "";
    public bool CleanupCompleted { get; private set; }

    private ComparisonWorkerClient(OwnedWorkerProcess process)
    {
        _process = process;
        _outputPump = PumpOutputAsync();
        _errorPump = PumpErrorAsync();
    }

    public static async Task<ComparisonWorkerClient> StartAsync(WorkerLaunch launch, string engine,
        string catalogSha256, CancellationToken cancellationToken = default, TimeSpan? startupDeadline = null)
    {
        var deadline = startupDeadline ?? StartupDeadline;
        ValidateDeadline(deadline);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(deadline);
        ComparisonWorkerClient? client = null;
        try
        {
            client = new ComparisonWorkerClient(await OwnedWorkerProcess.StartAsync(launch, startup.Token).ConfigureAwait(false));
            client.Identity = await client.UnderDeadlineAsync(async token =>
            {
                using var ready = await client._frames.Reader.ReadAsync(token).ConfigureAwait(false);
                var identity = ready.RootElement;
                if (identity.GetProperty("protocolVersion").GetInt32() != ComparisonProtocol.Version
                    || identity.GetProperty("status").GetString() != "Ready"
                    || identity.GetProperty("engine").GetString() != engine
                    || identity.GetProperty("catalogSha256").GetString() != catalogSha256
                    || identity.GetProperty("catalogVersion").GetInt32() != WorkloadCatalog.Version
                    || identity.GetProperty("processId").GetInt32() != client.ProcessId
                    || identity.GetProperty("maximumFrameBytes").GetInt32() != ComparisonProtocol.MaximumFrameBytes
                    || identity.GetProperty("maximumBatchIterations").GetInt32() != ComparisonProtocol.MaximumBatchIterations
                    || identity.GetProperty("maximumBatchSeconds").GetInt32() != ComparisonProtocol.MaximumBatchSeconds)
                    throw new InvalidDataException("Wrong worker identity, catalog, process or protocol bounds.");
                client._clockFrequency = identity.GetProperty("clockFrequency").GetInt64();
                if (client._clockFrequency is <= 0 or > 1_000_000_000_000)
                    throw new InvalidDataException("Invalid worker clock frequency.");
                return identity.Clone();
            }, deadline, startup.Token).ConfigureAwait(false);
            return client;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && startup.IsCancellationRequested)
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            throw new TimeoutException("Worker startup exceeded its deadline.");
        }
        catch
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<WorkerResponse> VerifyAsync(ComparisonWorkload workload, CancellationToken cancellationToken = default,
        TimeSpan? deadline = null) => RequestAsync(workload, "verify", null, 2, cancellationToken, deadline ?? RequestDeadline);

    public Task<WorkerResponse> BatchAsync(ComparisonWorkload workload, string lane, int iterations,
        CancellationToken cancellationToken = default, TimeSpan? deadline = null)
    {
        if (lane is not ("warm" or "compile-run" or "compile") || iterations is < 1 or > ComparisonProtocol.MaximumBatchIterations)
            throw new ArgumentException("Use a supported lane and bounded positive batch count.");
        return RequestAsync(workload, "batch", lane, iterations, cancellationToken, deadline ?? RequestDeadline);
    }

    private async Task<WorkerResponse> RequestAsync(ComparisonWorkload workload, string operation, string? lane,
        int count, CancellationToken cancellationToken, TimeSpan deadline)
    {
        ValidateDeadline(deadline);
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || _closed, this);
            if (operation == "batch" && !_verified.Contains(workload.Id))
                throw new InvalidOperationException("A case must pass verification before a timed batch.");
            var requestId = checked(++_requestId);
            var result = await UnderDeadlineAsync(async token =>
            {
                var response = await ExchangeAsync(new
                {
                    protocolVersion = ComparisonProtocol.Version, requestId, operation, caseId = workload.Id,
                    sourceSha256 = workload.SourceSha256, fixtureSha256 = workload.FixtureSha256,
                    expectedOutputSha256 = workload.ExpectedOutputSha256, lane, iterations = count,
                }, token).ConfigureAwait(false);
                using (response)
                {
                    var row = response.RootElement;
                    foreach (var name in new[] { "status", "completedInvocations", "elapsedTicks", "actualOutputSha256", "reason" })
                        _ = row.GetProperty(name); // Null is explicit; a missing field is malformed.
                    if (row.GetProperty("protocolVersion").GetInt32() != ComparisonProtocol.Version
                        || row.GetProperty("requestId").GetInt32() != requestId
                        || row.GetProperty("caseId").GetString() != workload.Id
                        || row.GetProperty("sourceSha256").GetString() != workload.SourceSha256
                        || row.GetProperty("fixtureSha256").GetString() != workload.FixtureSha256
                        || row.GetProperty("expectedOutputSha256").GetString() != workload.ExpectedOutputSha256
                        || row.GetProperty("clockFrequency").GetInt64() != _clockFrequency)
                        throw new InvalidDataException("Wrong response sequence, case, hashes or clock.");
                    var result = row.Deserialize<WorkerResponse>(ComparisonProtocol.JsonOptions)
                        ?? throw new InvalidDataException("Missing worker response.");
                    if (result.CompletedInvocations < 0 || result.CompletedInvocations > count)
                        throw new InvalidDataException("Invalid completed invocation count.");
                    if (result.Status == (operation == "verify" ? "Equivalent" : "Completed"))
                    {
                        if (result.CompletedInvocations != count
                            || (operation == "verify" ? result.ElapsedTicks is not null : result.ElapsedTicks is null or <= 0)
                            || (result.ElapsedTicks is { } ticks && (double)ticks / _clockFrequency > RequestDeadline.TotalSeconds)
                            || result.ActualOutputSha256 != (lane == "compile" ? null : workload.ExpectedOutputSha256)
                            || result.Reason is not null)
                            throw new InvalidDataException("Incomplete or inconsistent successful worker result.");
                    }
                    else if (result.Status is not ("Mismatch" or "Unsupported" or "BudgetDenied" or "Failure" or "Timeout")
                        || result.ElapsedTicks is not null)
                        throw new InvalidDataException("A failed or unknown worker result cannot carry a timing sample.");
                    return result;
                }
            }, deadline, cancellationToken).ConfigureAwait(false);
            if (operation == "verify" && result.Status == "Equivalent") _verified.Add(workload.Id);
            else if (result.Status is not ("Equivalent" or "Completed")) _verified.Remove(workload.Id);
            return result;
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally { _serial.Release(); }
    }

    private async Task<JsonDocument> ExchangeAsync(object request, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _expectedFrames, 1, 0) != 0)
            throw new InvalidDataException("A previous response is still pending.");
        // Receive is already live in the pump before any potentially blocking
        // stdin write. The same deadline covers write, read and validation.
        await ComparisonProtocol.WriteAsync(_process.Input, request, token).ConfigureAwait(false);
        return await _frames.Reader.ReadAsync(token).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || _closed, this);
            await UnderDeadlineAsync(async token =>
            {
                var requestId = checked(++_requestId);
                using var response = await ExchangeAsync(new
                {
                    protocolVersion = ComparisonProtocol.Version, requestId, operation = "quit",
                }, token).ConfigureAwait(false);
                if (response.RootElement.GetProperty("protocolVersion").GetInt32() != ComparisonProtocol.Version
                    || response.RootElement.GetProperty("requestId").GetInt32() != requestId
                    || response.RootElement.GetProperty("status").GetString() != "Closed")
                    throw new InvalidDataException("Wrong worker shutdown acknowledgment.");
                _process.Input.Dispose();
                await Task.WhenAll(_outputPump, _errorPump, _process.Exit).WaitAsync(token).ConfigureAwait(false);
                if (await _process.Exit.ConfigureAwait(false) != 0 || StandardError.Length != 0)
                    throw new InvalidDataException("Worker exited nonzero or emitted unexpected stderr.");
                _closed = true;
                return true;
            }, ShutdownDeadline, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
            _serial.Release();
        }
    }

    private async Task<T> UnderDeadlineAsync<T>(Func<CancellationToken, Task<T>> action, TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _transportFailure.Token);
        bounded.CancelAfter(deadline);
        var operation = action(bounded.Token);
        try { return await operation.WaitAsync(bounded.Token).ConfigureAwait(false); }
        catch (Exception failure)
        {
            var transportFailure = _failure;
            var timedOut = failure is OperationCanceledException && !cancellationToken.IsCancellationRequested;
            _process.Kill();
            // WaitAsync bounds even a synchronous pipe implementation. Killing
            // the owned scope releases its handles; join the actual operation
            // before cleanup so no abandoned write/read continues afterwards.
            try { await operation.WaitAsync(ShutdownDeadline).ConfigureAwait(false); }
            catch when (operation.IsCompleted) { /* Observed; original failure remains authoritative. */ }
            if (transportFailure is not null)
                throw new IOException("Worker transport failed: " + transportFailure.Message, transportFailure);
            if (timedOut) throw new TimeoutException("Worker operation exceeded its deadline.");
            throw;
        }
    }

    private async Task PumpOutputAsync()
    {
        try
        {
            while (await ComparisonProtocol.ReadAsync(_process.Output).ConfigureAwait(false) is { } frame)
            {
                if (Interlocked.CompareExchange(ref _expectedFrames, 0, 1) != 1 || !_frames.Writer.TryWrite(frame))
                {
                    frame.Dispose();
                    throw new InvalidDataException("Unsolicited or queued worker response.");
                }
            }
            _frames.Writer.TryComplete();
        }
        catch (Exception failure)
        {
            FailTransport(failure);
            _frames.Writer.TryComplete(failure);
            throw;
        }
    }

    private async Task PumpErrorAsync()
    {
        try
        {
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await _process.Error.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + count > MaximumErrorBytes) throw new InvalidDataException("Worker stderr exceeds 64 KiB.");
                bytes.Write(buffer, 0, count);
            }
            StandardError = ComparisonProtocol.Utf8.GetString(bytes.ToArray());
        }
        catch (Exception failure) { FailTransport(failure); throw; }
    }

    private void FailTransport(Exception failure)
    {
        Interlocked.CompareExchange(ref _failure, failure, null);
        _transportFailure.Cancel();
        try { _process.Kill(); }
        catch (Exception cleanup) { Interlocked.Exchange(ref _failure, new AggregateException(failure, cleanup)); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        var processCleaned = false;
        try
        {
            await _process.DisposeAsync().ConfigureAwait(false);
            processCleaned = true;
        }
        finally
        {
            try { await Task.WhenAll(_outputPump, _errorPump).WaitAsync(ShutdownDeadline).ConfigureAwait(false); }
            catch when (_outputPump.IsCompleted && _errorPump.IsCompleted) { /* Observed transport errors; no live pump. */ }
            while (_frames.Reader.TryRead(out var frame)) frame.Dispose();
            _transportFailure.Dispose();
            CleanupCompleted = processCleaned;
        }
    }

    private static void ValidateDeadline(TimeSpan deadline)
    {
        if (deadline <= TimeSpan.Zero || deadline > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(deadline), "Use a finite positive deadline of at most five minutes.");
    }
}
