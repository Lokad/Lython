using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Lokad.Lython;

using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var rows = new List<object>();
foreach (var scenario in catalog.RootElement.GetProperty("cases").EnumerateArray())
{
    var id = scenario.GetProperty("id").GetString()!;
    if (!id.Contains("rule-closures") && id != "scripts.ordinary-fully-supplied-helper" && id != "scripts.factory-observed-defaults") continue;
    var budget = new RecordingBudget();
    using var script = new LythonEngine().Compile(scenario.GetProperty("source").GetString()!, budget);
    if (!script.IsValid) throw new InvalidOperationException(id);
    var baseline = budget.Root.ReservedBytes;
    budget.Capturing = true;
    using (var result = script.Run(new NoFileHost(), new LythonRunOptions { MemoryBudget = budget }))
    {
        if (!result.Success || result.StandardOutput != scenario.GetProperty("expectedOutput").GetString())
            throw new InvalidOperationException(id + " output mismatch: " + result.Failure?.Message);
    }
    if (budget.Root.ReservedBytes != baseline) throw new InvalidOperationException(id + " execution refund failed");
    budget.Capturing = false;
    rows.Add(new { id, budget.Growths, baseline, sources = budget.Sources.Select(e => new
        { site = e.Key.Site, bytes = e.Key.Delta, count = e.Value }).OrderByDescending(e => e.count) });
    script.Dispose();
    if (budget.Root.ReservedBytes != 0) throw new InvalidOperationException(id + " compile refund failed");
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));

sealed class RecordingBudget : ILythonMemoryBudget
{
    internal LythonMemoryBudget Root { get; } = new();
    internal int Growths { get; private set; }
    internal bool Capturing { get; set; }
    internal Dictionary<(string Site, long Delta), int> Sources { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddGrowth(long delta)
    {
        if (!Capturing || delta <= 0) return;
        Growths++;
        var site = "unresolved";
        foreach (var frame in new StackTrace().GetFrames())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (type?.Assembly != typeof(LythonEngine).Assembly || type.FullName!.Contains("MemoryGovernor", StringComparison.Ordinal))
                continue;
            site = type.Name + "." + method!.Name;
            break;
        }
        var key = (site, delta);
        Sources[key] = Sources.GetValueOrDefault(key) + 1;
    }

    public ILythonMemoryScope CreateScope() => new Scope(this, Root.CreateScope());
    public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        => Reserve(Root, bytes, out reservation);

    private bool Reserve(ILythonMemoryBudget owner, long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
    {
        reservation = null;
        if (!owner.TryReserve(bytes, out var admitted)) return false;
        AddGrowth(bytes);
        reservation = new Reservation(this, admitted);
        return true;
    }

    private sealed class Scope(RecordingBudget root, ILythonMemoryScope inner) : ILythonMemoryScope
    {
        public ILythonMemoryScope CreateScope() => new Scope(root, inner.CreateScope());
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            => root.Reserve(inner, bytes, out reservation);
        public void Dispose() => inner.Dispose();
    }

    private sealed class Reservation(RecordingBudget root, ILythonMemoryReservation inner) : ILythonMemoryReservation
    {
        public long Bytes => inner.Bytes;
        public bool TryResize(long bytes)
        {
            var growth = bytes - inner.Bytes;
            if (!inner.TryResize(bytes)) return false;
            root.AddGrowth(growth);
            return true;
        }
        public void Dispose() => inner.Dispose();
    }
}

sealed class NoFileHost : ILythonHost
{
    public string Cwd => "/";
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => throw new NotSupportedException();
    public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest path, CancellationToken token) => throw new NotSupportedException();
    public ValueTask MkDirAsync(LythonPathRequest path, CancellationToken token) => throw new NotSupportedException();
    public ValueTask RemoveAsync(LythonPathRequest path, CancellationToken token) => throw new NotSupportedException();
    public ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) => throw new NotSupportedException();
    public ValueTask MoveAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) => throw new NotSupportedException();
    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => throw new NotSupportedException();
}
