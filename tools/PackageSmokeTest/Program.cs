using Lokad.Lython;

var expectedVersion = args.Single();
if (typeof(LythonEngine).Assembly.GetName().Version?.ToString(3) != expectedVersion)
    throw new Exception("Unexpected package assembly version.");

var engine = new LythonEngine();
const string source = """
    import json
    values = [x for x in range(5) if x > 1 if x < 4]
    encoder = json.JSONEncoder()
    encoder.ensure_ascii = False
    print(encoder.encode({'é': values}))
    return 42
    """;
var compiled = engine.Compile(source);
if (!compiled.IsValid) throw new Exception(string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { compiled.Run(new PureHost()), await compiled.RunAsync(new PureHost()), engine.Run(source, new PureHost()) })
{
    if (!result.Success || result.StandardOutput != "{\"é\": [2, 3]}\n" || result.ReturnValue?.ToString() != "42")
        throw new Exception("Package consumer failed: " + result.Failure?.Message);
}
Console.WriteLine("Package consumer passed: " + expectedVersion);

sealed class PureHost : ILythonHost
{
    public string Cwd => "/";
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
}
