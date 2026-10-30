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
        public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request.Path != "/data" || options.Access != LythonFileAccess.Read)
                throw new LythonHostCapabilityUnavailableException("fixture read-only /data");
            var owner = budget.CreateScope();
            if (!owner.TryReserve(256, out _))
            {
                owner.Dispose();
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "fixture file refused", requestedBytes: 256);
            }
            return ValueTask.FromResult<ILythonByteFile>(new ReadFile(this, owner));
        }
        private sealed class ReadFile(ReadHost host, ILythonMemoryScope owner) : ILythonByteFile
        {
            private bool _closed;
            public long Position { get; private set; }
            public long? LengthHint => host._bytes.Length;
            public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                host.Suspended = true;
                await Task.Delay(10, token).ConfigureAwait(false);
                var count = Math.Min(buffer.Length, host._bytes.Length - (int)Position);
                host._bytes.AsMemory((int)Position, count).CopyTo(buffer);
                Position += count;
                return count;
            }
            public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
                => throw new LythonHostCapabilityUnavailableException("fixture file writes");
            public ValueTask FlushAsync(CancellationToken token) => ValueTask.CompletedTask;
            public ValueTask DisposeAsync()
            {
                if (!_closed) { _closed = true; owner.Dispose(); }
                return ValueTask.CompletedTask;
            }
        }
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, _bytes.Length, null));
        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => ValueTask.FromResult(true);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask MkDirAsync(LythonPathRequest p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask RemoveAsync(LythonPathRequest p, CancellationToken t) => throw new NotSupportedException();
        public ValueTask CopyAsync(LythonPathRequest s, LythonPathRequest d, CancellationToken t) => throw new NotSupportedException();
        public ValueTask MoveAsync(LythonPathRequest s, LythonPathRequest d, CancellationToken t) => throw new NotSupportedException();
    }
}
