using System.Diagnostics.Tracing;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

var pid = int.Parse(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var client = new DiagnosticsClient(pid);
// Observe allocation ticks. No collect/dump/rundown provider keywords.
var keywords = (long)ClrTraceEventParser.Keywords.GC;
using var session = client.StartEventPipeSession(
    [new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Verbose, keywords)],
    requestRundown: false, circularBufferMB: 16);
using var source = new EventPipeEventSource(session.EventStream);
using var writer = new StreamWriter(Path.Combine(output, "allocation-ticks.jsonl"), false, new UTF8Encoding(false), 65536);
long count = 0;
source.Clr.GCAllocationTick += data =>
{
    writer.WriteLine(JsonSerializer.Serialize(new
    {
        number = ++count, threadId = data.ThreadID, milliseconds = data.TimeStampRelativeMSec,
        utc = data.TimeStamp.ToUniversalTime().ToString("O"),
        type = data.TypeName, bytesSinceTick = data.AllocationAmount64, objectBytes = data.ObjectSize,
        kind = data.AllocationKind.ToString(), version = data.Version,
    }));
};
var processing = Task.Run(() => source.Process());
Console.WriteLine(JsonSerializer.Serialize(new { status = "Attached", targetPid = pid, keywords, requestRundown = false }));
Console.Out.Flush();
try
{
    var stop = await Console.In.ReadLineAsync();
    if (stop != "stop") throw new InvalidDataException("Expected owned stop command.");
}
finally
{
    session.Stop();
    await processing.WaitAsync(TimeSpan.FromSeconds(10));
    writer.Flush();
    File.WriteAllText(Path.Combine(output, "collector.json"), JsonSerializer.Serialize(new
    {
        targetPid = pid, allocationTicks = count, eventsLost = source.EventsLost,
        processCompleted = true, keywords, requestRundown = false,
        collector = typeof(DiagnosticsClient).Assembly.FullName,
        reader = typeof(EventPipeEventSource).Assembly.FullName,
    }));
}
if (count == 0 || source.EventsLost != 0) throw new InvalidDataException("Missing allocation ticks or lost trace events.");
