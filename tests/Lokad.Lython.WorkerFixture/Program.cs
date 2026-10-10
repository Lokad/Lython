using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Benchmarks.Comparison;

// Deliberately controlled process faults, with no Python installation needed.
// This executable is a test build dependency, never a runtime package input.
if (args[0] == "stack-telemetry")
{
    return StackTelemetryFixture.Run(args[1]);
}
if (args[0] == "echo-environment")
{
    Console.Write(JsonSerializer.Serialize(new
    {
        tiering = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        pgo = Environment.GetEnvironmentVariable("COMPlus_TieredPGO"),
    }));
    return 0;
}
if (args[0] == "child")
{
    await Task.Delay(Timeout.Infinite);
    return 0;
}
var mode = args[0];
var receipt = args[2];
File.WriteAllText(Path.Combine(receipt, "root"), Environment.ProcessId.ToString());
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
var error = Console.OpenStandardError();
int? childId = null;
JsonElement? childEnvironment = null;
if (mode == "environment-parent")
{
    // Set overrides only in this controlled subprocess, never in the shared
    // test host. Exercise the actual Linux bootstrap environment inheritance.
    Environment.SetEnvironmentVariable("DOTNET_TieredCompilation", "0");
    Environment.SetEnvironmentVariable("COMPlus_TieredPGO", "1");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await using var child = await OwnedWorkerProcess.StartAsync(new(Environment.ProcessPath!,
        [Assembly.GetExecutingAssembly().Location, "echo-environment"], Environment.CurrentDirectory,
        RemoveSupervisorTieringOverride: args[3] == "clear"), deadline.Token);
    child.Input.Dispose();
    using var stdout = new StreamReader(child.Output);
    using var stderr = new StreamReader(child.Error);
    var readOutput = stdout.ReadToEndAsync(deadline.Token);
    var readError = stderr.ReadToEndAsync(deadline.Token);
    await Task.WhenAll(readOutput, readError, child.Exit).WaitAsync(deadline.Token);
    if (child.Exit.Result != 0 || readError.Result.Length != 0) throw new InvalidDataException("Environment child failed.");
    childEnvironment = JsonDocument.Parse(readOutput.Result).RootElement.Clone();
}
if (mode is "descendant-hang" or "orphan-pipes" or "once-orphan-pipes")
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("child");
    var child = Process.Start(start)!;
    childId = child.Id;
    File.WriteAllText(Path.Combine(receipt, "child"), childId.ToString());
}
if (mode.StartsWith("once-", StringComparison.Ordinal))
{
    if (mode == "once-hang") { await Task.Delay(Timeout.Infinite); return 0; }
    var identity = new
    {
        protocolVersion = 1, status = "Ready", engine = "Fixture", catalogSha256 = args[1], catalogVersion = 1,
        processId = Environment.ProcessId, clockFrequency = 1000,
        maximumFrameBytes = 4 * 1024 * 1024, maximumBatchIterations = 1_000_000, maximumBatchSeconds = 60,
    };
    var envelope = new
    {
        identity,
        response = new
        {
            protocolVersion = 1, requestId = 1, caseId = mode == "once-wrong-case" ? "other" : args[3],
            status = mode == "once-failure" ? "Failure" : "Equivalent",
            completedInvocations = mode == "once-two-jobs" ? 2 : mode == "once-failure" ? 0 : 1,
            elapsedTicks = mode == "once-worker-time" ? (long?)100 : null, clockFrequency = 1000,
            sourceSha256 = args[4], fixtureSha256 = args[5], expectedOutputSha256 = args[6],
            actualOutputSha256 = mode == "once-wrong-output" ? new string('0', 64) : mode == "once-failure" ? null : args[6],
            reason = mode == "once-failure" ? "controlled failure" : null,
        },
    };
    await WriteAsync(envelope);
    if (mode == "once-two-frames") await WriteAsync(envelope);
    if (mode == "once-truncated") await output.WriteAsync(new byte[] { 0, 0, 0 });
    if (mode == "once-stderr-flood") { await error.WriteAsync(new byte[128 * 1024]); await Task.Delay(Timeout.Infinite); }
    if (mode == "once-stdout-flood") { await output.WriteAsync(new byte[5 * 1024 * 1024]); await Task.Delay(Timeout.Infinite); }
    return mode == "once-nonzero" ? 7 : 0;
}
if (mode == "hang-startup") { await Task.Delay(Timeout.Infinite); return 0; }
if (mode == "oversize")
{
    var header = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(header, 4 * 1024 * 1024 + 1);
    await output.WriteAsync(header);
    await output.FlushAsync();
    await Task.Delay(Timeout.Infinite);
    return 0;
}
if (mode == "truncated")
{
    await output.WriteAsync(new byte[] { 0, 0, 0, 10, (byte)'{' });
    return 0;
}
await WriteAsync(new
{
    protocolVersion = mode == "wrong-version" ? 2 : 1, status = "Ready", engine = "Fixture",
    catalogSha256 = mode == "wrong-catalog" ? new string('f', 64) : args[1], catalogVersion = 1,
    processId = mode == "wrong-pid" ? 1 : Environment.ProcessId,
    clockFrequency = mode == "wrong-clock" ? 0 : 1000,
    maximumFrameBytes = 4 * 1024 * 1024, maximumBatchIterations = 1_000_000, maximumBatchSeconds = 60,
    childId, childEnvironment, echoedArguments = args.Skip(3).ToArray(),
});
if (mode == "stderr-flood")
{
    await error.WriteAsync(new byte[128 * 1024]);
    await error.FlushAsync();
    await Task.Delay(Timeout.Infinite);
    return 0;
}
if (mode == "orphan-pipes") return 0;
while (true)
{
    var header = new byte[4];
    await input.ReadExactlyAsync(header);
    var bytes = new byte[BinaryPrimitives.ReadInt32BigEndian(header)];
    await input.ReadExactlyAsync(bytes);
    using var request = JsonDocument.Parse(bytes);
    var row = request.RootElement;
    var requestId = row.GetProperty("requestId").GetInt32();
    var operation = row.GetProperty("operation").GetString();
    if (operation == "quit")
    {
        await WriteAsync(new { protocolVersion = 1, requestId, status = "Closed" });
        if (mode == "extra-output") await WriteAsync(new { unexpected = true });
        if (mode == "hang-shutdown") await Task.Delay(Timeout.Infinite);
        return mode == "nonzero-exit" ? 7 : 0;
    }
    if (mode is "hang-request" or "descendant-hang") { await Task.Delay(Timeout.Infinite); return 0; }
    string? Text(string name) => row.GetProperty(name).GetString();
    var failed = mode is "valid-failure" or "failure-with-time";
    var timed = operation == "batch" || mode == "failure-with-time";
    await WriteAsync(new
    {
        protocolVersion = 1, requestId = mode == "replay" ? requestId - 1 : requestId,
        caseId = mode == "wrong-case" ? "other" : Text("caseId"),
        status = failed ? "Failure" : operation == "batch" ? "Completed" : "Equivalent",
        completedInvocations = mode is "wrong-count" or "valid-failure" or "failure-with-time" ? 0
            : operation == "batch" ? row.GetProperty("iterations").GetInt32() : 2,
        elapsedTicks = timed ? mode == "oversized-time" ? (long?)long.MaxValue : 100 : null,
        clockFrequency = mode == "changed-clock" ? 2000 : 1000,
        sourceSha256 = mode == "wrong-source" ? new string('0', 64) : Text("sourceSha256"),
        fixtureSha256 = Text("fixtureSha256"), expectedOutputSha256 = Text("expectedOutputSha256"),
        actualOutputSha256 = mode == "wrong-output" ? new string('0', 64)
            : failed || (operation == "batch" && Text("lane") == "compile") ? null : Text("expectedOutputSha256"),
        reason = failed ? "controlled failure" : null,
    });
}

async Task WriteAsync(object value)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
    var header = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
    await output.WriteAsync(header);
    await output.WriteAsync(bytes);
    await output.FlushAsync();
}
