using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Tests.Harness;

var started = DateTimeOffset.UtcNow;
var budget = new LythonMemoryBudget(256L * 1024 * 1024);
const int inputLength = 32 * 1024 * 1024;
var rows = new List<object>();
foreach (var method in new[] { "isidentifier", "istitle" })
{
    var input = method == "istitle" ? "A" + new string('a', inputLength - 1) : new string('a', inputLength);
    using var script = new LythonEngine().Compile($"print('ready')\nprint(text.{method}())", budget);
    if (!script.IsValid) throw new Exception("Invalid diagnostic script");
    var baseline = budget.ReservedBytes;
    foreach (var asynchronous in new[] { false, true })
    {
        _ = await Observe(script, method == "istitle" ? "Aaa" : "abc", method, asynchronous, false, budget, baseline);
        rows.Add(await Observe(script, input, method, asynchronous, false, budget, baseline));
        rows.Add(await Observe(script, input, method, asynchronous, true, budget, baseline));
    }
}
if (budget.ReservedBytes != 0) throw new Exception("Compilation ownership did not return to zero");
var core = typeof(LythonEngine).Assembly.Location;
var payload = new
{
    revision = args[0], started, finished = DateTimeOffset.UtcNow, runtime = RuntimeInformation.FrameworkDescription,
    machine = RuntimeInformation.OSDescription, processors = Environment.ProcessorCount,
    tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default",
    coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core))),
    driverSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location))),
    inputUtf8Bytes = inputLength, cancellationDelayAfterReadyMs = 10, budgetBytes = budget.LimitBytes,
    finalReservedBytes = budget.ReservedBytes, rows,
};
File.WriteAllText(args[1], JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Eight observations completed; ownership returned to zero; {args[0]}");

static async Task<object> Observe(LythonCompiledScript script, string text, string method, bool asynchronous, bool cancel,
    LythonMemoryBudget budget, long baseline)
{
    var entered = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
    var host = new MockLythonHost { OnStandardOutputWrite = () => entered.TrySetResult(Stopwatch.GetTimestamp()) };
    using var cancellation = new CancellationTokenSource();
    var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = text }, CancellationToken = cancellation.Token };
    var pending = Task.Factory.StartNew(() => asynchronous ? script.RunAsync(host, options)
        : Task.FromResult(script.Run(host, options)), CancellationToken.None,
        TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    LythonExecutionResult? result = null;
    long ready = 0, requested = 0, finished = 0;
    var wasPending = false;
    var heldWhilePending = 0L;
    try
    {
        var first = await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
        if (ReferenceEquals(first, pending) && !entered.Task.IsCompleted)
        {
            result = await pending;
            throw new Exception(result.Failure?.Message ?? "No entry signal");
        }
        ready = await entered.Task;
        if (cancel)
        {
            await Task.Delay(10);
            wasPending = !pending.IsCompleted;
            heldWhilePending = budget.ReservedBytes;
            requested = Stopwatch.GetTimestamp();
            cancellation.Cancel();
        }
        result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        finished = Stopwatch.GetTimestamp();
        if (cancel && wasPending)
        {
            if (result.Success || result.FailureAttribution?.Reason != LythonFailureReason.Cancelled || result.StandardOutput != "ready\n")
                throw new Exception("Cancellation result/output mismatch");
            if (heldWhilePending <= baseline) throw new Exception("Pending input was not funded");
        }
        else if (!result.Success || result.StandardOutput != "ready\nTrue\n")
            throw new Exception(result.Failure?.Message ?? "Control result/output mismatch");
    }
    finally
    {
        cancellation.Cancel();
        result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
        result.Dispose();
    }
    if (budget.ReservedBytes != baseline) throw new Exception("Execution ownership did not return to baseline");
    return new
    {
        method, api = asynchronous ? "RunAsync" : "Run", cancel, wasPendingWhenCancelled = wasPending,
        readyToCompletionMs = Stopwatch.GetElapsedTime(ready, finished).TotalMilliseconds,
        actualDelayAfterReadyMs = requested == 0 ? (double?)null : Stopwatch.GetElapsedTime(ready, requested).TotalMilliseconds,
        cancellationToCompletionMs = requested == 0 ? (double?)null : Stopwatch.GetElapsedTime(requested, finished).TotalMilliseconds,
        heldWhilePending, baseline, afterCleanup = budget.ReservedBytes,
        status = cancel && !wasPending ? "Inconclusive: completed before cancellation request" : "Observed",
    };
}
