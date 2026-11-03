using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
var images = new[] { new Probe(args[0], "before"), new Probe(args[1], "candidate"), new Probe(args[0], "before-control") };
for (var block = 0; block < 16; block++) foreach (var image in images) image.Run(65536);
Thread.Sleep(2000);
var rows = new List<object>();
for (var round = 0; round < 9; round++)
    for (var offset = 0; offset < 3; offset++)
    {
        var image = images[(round + offset) % 3];
        var cpu = Process.GetCurrentProcess().TotalProcessorTime;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        image.Run(1048576);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        rows.Add(new { round, image.Label, pairs = 1048576, elapsedMilliseconds = elapsed,
            cpuMilliseconds = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds, allocatedBytes = allocation });
    }
foreach (var image in images) image.Dispose();
File.WriteAllText(args[2], JsonSerializer.Serialize(new { purpose = "B177 direct shared-budget resize diagnostic; no whole-script claim", rows,
    identities = images.ToDictionary(i => i.Label, i => i.Hash), normalGc = !System.Runtime.GCSettings.IsServerGC,
    allOwnershipBaselinesZero = true, boundary = "Bound public reservation delegate: grow128/shrink64 pair, no reflection/allocation in the measured loop" }));
sealed class Probe : IDisposable
{
    private readonly object _budget;
    private readonly PropertyInfo _balance;
    private readonly IDisposable _reservation;
    private readonly Func<long, bool> _resize;
    internal string Label { get; }
    internal string Hash { get; }
    internal Probe(string path, string label)
    {
        Label = label; path = Path.GetFullPath(path); Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var assembly = new AssemblyLoadContext(label, false).LoadFromAssemblyPath(path);
        var type = assembly.GetType("Lokad.Lython.LythonMemoryBudget", true)!;
        _budget = Activator.CreateInstance(type, [256L])!;
        _balance = type.GetProperty("ReservedBytes")!;
        object?[] arguments = [0L, null];
        if (!(bool)type.GetMethod("TryReserve")!.Invoke(_budget, arguments)!) throw new InvalidDataException();
        _reservation = (IDisposable)arguments[1]!;
        _resize = _reservation.GetType().GetMethod("TryResize")!.CreateDelegate<Func<long, bool>>(_reservation);
    }
    internal void Run(int pairs)
    {
        for (var i = 0; i < pairs; i++)
            if (!_resize(128) || !_resize(64)) throw new InvalidDataException("Admission unexpectedly refused.");
    }
    public void Dispose()
    {
        if ((long)_balance.GetValue(_budget)! != 64) throw new InvalidDataException("Resize accounting failed.");
        _reservation.Dispose();
        if ((long)_balance.GetValue(_budget)! != 0) throw new InvalidDataException("Owner did not return its allowance.");
    }
}
