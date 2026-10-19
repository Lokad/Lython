using Lokad.Lython;
using Lokad.Lython.Utf8Regex;

var version = args.Single();
if (typeof(Utf8RegexProvider).Assembly.GetName().Version?.ToString(3) != version)
    throw new Exception("Unexpected adapter assembly version.");
var budget = new LythonMemoryBudget(8 * 1024 * 1024);
var engine = new LythonEngine(Utf8RegexProvider.Instance);
const string source = """
    import re, json, fnmatch
    p = re.compile('(?i)(?P<first>a)(?P<tail>b)?')
    m = p.search('!A')
    assert m.group('first') == 'A' and m.group('tail') is None
    assert m.span() == (1,2) and p.flags == 34
    assert p.sub(r'<\g<first>>', '!A') == '!<A>'
    assert re.split('(,)', 'a,b') == ['a', ',', 'b']
    assert fnmatch.fnmatchcase('x.py','*.py')
    values = re.compile('(a)(b)').findall('ab!ab')
    assert type(values) is list
    values.append(('x','y'))
    print(json.dumps({'matches': values}))
    return values
    """;
using (var script = engine.Compile(source, budget))
{
    if (!script.IsValid) throw new Exception(string.Join("; ", script.Diagnostics.Select(d => d.Message)));
    var baseline = budget.ReservedBytes;
    foreach (var asynchronous in new[] { false, true })
    {
        using (var result = asynchronous ? await script.RunAsync(new PureHost()) : script.Run(new PureHost()))
        {
            if (!result.Success || result.StandardOutput != "{\"matches\": [[\"a\", \"b\"], [\"a\", \"b\"], [\"x\", \"y\"]]}\n"
                || result.ReturnValue is not List<object?> values || values.Count != 3
                || values[0] is not object?[] captures || !captures.SequenceEqual(new object?[] { "a", "b" }))
                throw new Exception("Adapter package consumer failed: " + result.Failure?.Message);
            if (budget.ReservedBytes <= baseline) throw new Exception("Returned regex storage is unfunded.");
        }
        if (budget.ReservedBytes != baseline) throw new Exception("Adapter cleanup did not return allowance.");
    }
}
if (budget.ReservedBytes != 0) throw new Exception("Compiled adapter storage survived disposal.");
using var cancellation = new CancellationTokenSource();
using var unsupported = engine.Compile("import re\np=re.compile('x')", budget, cancellation.Token);
using var failure = unsupported.Run(new PureHost());
if (failure.FailureAttribution?.Reason != LythonFailureReason.HostCapabilityUnavailable)
    throw new Exception("Adapter did not report unavailable in-flight cancellation.");
Console.WriteLine("Adapter package consumer passed: " + version);

class PureHost : ILythonHost
{
    public string Cwd => "/";
    public virtual ILythonTextOutput? StandardError => null;
    public virtual ILythonTextOutput? StandardOutput => null;
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    public virtual ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public virtual ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) => throw new NotSupportedException();
    public virtual ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
}
