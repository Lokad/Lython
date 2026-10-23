using System.Text.Json;
using Lokad.Lython;

internal static class DataclassTraversalFixture
{
    internal static async Task<int> RunAsync(string scenario, string libraryHash)
    {
        var operation = scenario.Contains("astuple", StringComparison.Ordinal) ? "astuple" : "asdict";
        var source = "from dataclasses import dataclass,asdict,astuple\n@dataclass\nclass D:\n x:object\nwith open('/data') as f:kind=f.read()\nx=None\nif kind=='cycle':\n x=[];x.append(x)\nelse:\n for i in range(int(kind)):x=[x]\n" + operation + "(D(x))\nprint('done')";
        using var script = new LythonEngine().Compile(source);
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        var host = new ReadHost(scenario.EndsWith("cycle", StringComparison.Ordinal) ? "cycle" : "50000");
        bool safe;
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
            safe = !result.Success && result.FailureAttribution?.Reason == LythonFailureReason.InterpreterStackLimit;
        safe &= budget.ReservedBytes == 0 && host.Suspended;
        using (var recovery = await script.RunAsync(new ReadHost("50"), new LythonRunOptions { MemoryBudget = budget }))
            safe &= recovery.Success && recovery.StandardOutput == "done\n";
        safe &= budget.ReservedBytes == 0;
        Console.WriteLine(JsonSerializer.Serialize(new { scenario, Success = safe, completedAsynchronously = host.Suspended ? 1 : 0, libraryHash, processId = Environment.ProcessId }));
        return safe ? 0 : 7;
    }

    private sealed class ReadHost(string text) : ILythonHost
    {
        private readonly byte[] _bytes = System.Text.Encoding.UTF8.GetBytes(text);
        public bool Suspended { get; private set; }
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token)
        { Suspended = true; await Task.Delay(10, token).ConfigureAwait(false); return _bytes; }
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, _bytes.Length, null));
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => ValueTask.FromResult(true);
        public ValueTask WriteTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => throw new NotSupportedException();
        public ValueTask AppendTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask MkDirAsync(string p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask CopyAsync(string s, string d, CancellationToken t) => throw new NotSupportedException();
        public ValueTask MoveAsync(string s, string d, CancellationToken t) => throw new NotSupportedException();
    }
}
