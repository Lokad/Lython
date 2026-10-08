using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

// Deliberately controlled process faults, with no Python installation needed.
// This executable is a test build dependency, never a runtime package input.
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
if (mode is "descendant-hang" or "orphan-pipes")
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("child");
    var child = Process.Start(start)!;
    childId = child.Id;
    File.WriteAllText(Path.Combine(receipt, "child"), childId.ToString());
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
    childId, echoedArguments = args.Skip(3).ToArray(),
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
