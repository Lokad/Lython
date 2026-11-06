using Lokad.Lython;
using System.Text.Json;

var cases = new (string Name, string Source)[]
{
    ("ordinary async host control", "import time\ntime.sleep(0)\nprint('done')"),
    ("public hash", "import time\nclass Key:\n def __hash__(self):\n  time.sleep(0)\n  return 1\nprint(hash(Key()))"),
    ("compound dictionary query", "import time\nclass Key:\n def __hash__(self):\n  time.sleep(0)\n  return 1\nprint({}.get((Key(),)))")
};
foreach (var (name, source) in cases)
{
    var budget = new LythonMemoryBudget(1024 * 1024);
    using var script = new LythonEngine().Compile(source, budget);
    if (!script.IsValid) throw new Exception(string.Join(";", script.Diagnostics));
    var baseline = budget.ReservedBytes;
    var timing = new GatedTiming();
    var host = new Host(timing);
    using var cancellation = new CancellationTokenSource();
    var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
    var first = await Task.WhenAny(pending, timing.Started.Task).WaitAsync(TimeSpan.FromSeconds(10));
    var hostStartedBeforeResult = timing.Started.Task.IsCompleted;
    var runWasPending = !pending.IsCompleted;
    var fundedWhilePending = budget.ReservedBytes > baseline;
    timing.Release.TrySetResult();
    using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
    {
        Console.WriteLine(JsonSerializer.Serialize(new { name, source, runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            hostStartedBeforeResult, runWasPending, fundedWhilePending, timing.Calls, timing.Completed, result.Success,
            result.StandardOutput, result.Failure, baseline, retained=budget.ReservedBytes }));
        if (name == "ordinary async host control")
        {
            if (!hostStartedBeforeResult || !runWasPending || !fundedWhilePending || timing.Completed != 1 || !result.Success)
                throw new Exception("Ordinary async control did not actually suspend and settle.");
        }
        else if (result.Success || timing.Calls != 0 || result.Failure?.Message.Contains("cannot run synchronously") != true)
            throw new Exception("Observed hash behavior changed; inspect rather than assume the limitation.");
    }
    if (budget.ReservedBytes != baseline) throw new Exception("Execution/result ownership did not return to compilation baseline.");
    script.Dispose();
    if (budget.ReservedBytes != 0) throw new Exception("Compilation ownership did not return to zero.");
}

sealed class GatedTiming : ILythonTiming
{
    public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls, Completed;
    public long MonotonicNanoseconds => 0;
    public async ValueTask DelayAsync(TimeSpan duration, CancellationToken token)
    {
        Interlocked.Increment(ref Calls);
        Started.TrySetResult();
        await Release.Task.WaitAsync(token).ConfigureAwait(false);
        Interlocked.Increment(ref Completed);
    }
}

sealed class Host(ILythonTiming timing) : ILythonHost
{
    public string Cwd => "/";
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    public ILythonTiming Timing => timing;
    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("exists");
    public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest path, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("listdir");
    public ValueTask MkDirAsync(LythonPathRequest path, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("mkdir");
    public ValueTask RemoveAsync(LythonPathRequest path, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("remove");
    public ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest target, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("copy");
    public ValueTask MoveAsync(LythonPathRequest source, LythonPathRequest target, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("move");
    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => throw new LythonHostCapabilityUnavailableException("stat");
}
