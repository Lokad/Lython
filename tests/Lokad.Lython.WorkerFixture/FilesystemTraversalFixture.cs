using System.Text.Json;
using Lokad.Lython;

internal static class FilesystemTraversalFixture
{
    internal static async Task<int> RunAsync(string scenario, string libraryHash)
    {
        var source = scenario == "native-rglob"
            ? "from pathlib import Path\nreturn len(list(Path('/deep').rglob('missing')))"
            : "import glob\nreturn len(glob.glob('/deep/**/missing',recursive=True))";
        using var script = new LythonEngine().Compile(source);
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        var host = new DeepHost(2000);
        bool safe;
        bool executionSuccess;
        LythonFailureReason? reason;
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            executionSuccess = result.Success;
            reason = result.FailureAttribution?.Reason;
            safe = result.Success && result.ReturnValue?.ToString() == "0";
        }
        safe &= budget.ReservedBytes == 0;
        using (var recovery = await script.RunAsync(new DeepHost(2), new LythonRunOptions { MemoryBudget = budget }))
            safe &= recovery.Success && recovery.ReturnValue?.ToString() == "0";
        safe &= budget.ReservedBytes == 0;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            scenario,
            Success = safe,
            executionSuccess,
            reason,
            completedAsynchronously = host.Suspended ? 1 : 0,
            libraryHash,
            processId = Environment.ProcessId
        }));
        return safe && host.Suspended ? 0 : 7;
    }

    private sealed class DeepHost(int depth) : ILythonHost
    {
        private static readonly string[] Child = ["d"];
        private static readonly LythonPathStat Directory = new(LythonPathKind.Directory, 0, null);
        public bool Suspended { get; private set; }
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public async ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Suspended) { Suspended = true; await Task.Delay(25, token).ConfigureAwait(false); }
            return (path.Length - 5) / 2 < depth ? Child : Array.Empty<string>();
        }
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return ValueTask.FromResult(path.Contains("missing", StringComparison.Ordinal) ? new LythonPathStat(LythonPathKind.Missing, 0, null) : Directory); }
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => ValueTask.FromResult(true);
        public ValueTask MkDirAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => throw new NotSupportedException();
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => throw new NotSupportedException();
    }
}
