using Lokad.Lython;
using System.Text;

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

const string compatibilitySource = """
    import csv; import json; import re
    from collections import Counter, defaultdict
    def field(k, v):
        return k + v
    def asdict(value):
        return value
    print(field('a', 'b'), asdict(3))
    reader = csv.reader(['a,b', '1,2'])
    print(iter(reader) is reader, next(reader), list(reader), next(reader, 'empty'))
    d = {}
    key = (1, 2)
    assert key not in d or d[key] == 3
    d[key] = 3
    print(d[key])
    totals = defaultdict(Counter)
    totals['sample']['x'] += 2
    print(json.dumps(totals, sort_keys=True))
    match = re.search(r'(?P<first>a)(?P<tail>b)?', 'a')
    print(match[0], match['first'], match[2])
    try:
        match.group(10**100)
    except IndexError:
        print('IndexError')
    def generate():
        yield from (n for n in range(4) if n > 0 if n < 3)
    print(list(generate()))
    """;
var compatibility = engine.Compile(compatibilitySource);
if (!compatibility.IsValid)
    throw new Exception(string.Join("; ", compatibility.Diagnostics.Select(d => d.Message)));
const string compatibilityOutput = "ab 3\nTrue ['a', 'b'] [['1', '2']] empty\n3\n{\"sample\": {\"x\": 2}}\na a None\nIndexError\n[1, 2]\n";
foreach (var result in new[] { compatibility.Run(new PureHost()), await compatibility.RunAsync(new PureHost()) })
    RequireOutput(result, compatibilityOutput);

const string streamSource = """
    import csv
    class Writer:
        def __init__(self):
            self.parts = []
        write = lambda self, text: self.parts.append(text) or 42
    stream = Writer()
    writer = csv.writer(stream, lineterminator='\n')
    stream.write = lambda text: -1
    print(writer.writerow([1]) + 1)
    print(writer.writerows([[2]]))
    print(stream.parts)
    dictionary = csv.DictWriter(Writer(), ['a'], lineterminator='\n')
    print(dictionary.writeheader() + 1)
    class Callback:
        __call__ = lambda self, text: len(text)
    callback = Callback()
    callback.__call__ = lambda text: -1
    class Destination:
        write = callback
    print(csv.writer(Destination(), lineterminator='\n').writerow(['é😀']))
    class Missing:
        pass
    missing = Missing()
    missing.__call__ = lambda: 1
    print(callable(missing))
    """;
var streams = engine.Compile(streamSource);
if (!streams.IsValid)
    throw new Exception(string.Join("; ", streams.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { streams.Run(new PureHost()), await streams.RunAsync(new PureHost()) })
    RequireOutput(result, "43\nNone\n['1\\n', '2\\n']\n43\n3\nFalse\n");

const string memoryStreamSource = """
    import io, csv, json
    stream=io.StringIO(newline='')
    csv.writer(stream,lineterminator='\n').writerows([['é😀',3]])
    print(repr(stream.getvalue()),stream.tell(),type(stream) is io.StringIO)
    stream.seek(0)
    print(next(csv.reader(stream)))
    with io.StringIO('a😀bc') as value:
        print(repr(value.read(2)),value.tell())
        value.seek(0)
        print(value.write('XY'),repr(value.getvalue()))
    print(value.closed,value.flush())
    value=io.StringIO()
    json.dump({'x':'😀'},value,ensure_ascii=False)
    value.seek(0)
    print(json.load(value))
    """;
var memoryStreams = engine.Compile(memoryStreamSource);
if (!memoryStreams.IsValid)
    throw new Exception(string.Join("; ", memoryStreams.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { memoryStreams.Run(new PureHost()), await memoryStreams.RunAsync(new PureHost()) })
    RequireOutput(result, "'é😀,3\\n' 5 True\n['é😀', '3']\n'a😀' 2\n2 'XYbc'\nTrue None\n{'x': '😀'}\n");

const string byteStreamSource = """
    import io
    with io.BytesIO(b'a\xff\nb') as stream:
        print(type(stream) is io.BytesIO,iter(stream) is stream,stream.readline())
        snapshot=stream.getvalue()
        print(stream.seek(-1,io.SEEK_END),stream.write(b'XY'),snapshot,stream.getvalue())
        stream.seek(7)
        print(stream.write(b'\x00'),stream.getvalue())
        try:
            stream.writelines([b'z','wrong',b'later'])
        except TypeError:
            print(stream.getvalue())
    print(stream.closed,iter(stream) is stream)
    try:
        stream.flush()
    except ValueError:
        print('closed')
    """;
var byteStreams = engine.Compile(byteStreamSource);
if (!byteStreams.IsValid)
    throw new Exception(string.Join("; ", byteStreams.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { byteStreams.Run(new PureHost()), await byteStreams.RunAsync(new PureHost()) })
    RequireOutput(result, "True True b'a\\xff\\n'\n3 2 b'a\\xff\\nb' b'a\\xff\\nXY'\n1 b'a\\xff\\nXY\\x00\\x00\\x00'\nb'a\\xff\\nXY\\x00\\x00\\x00z'\nTrue True\nclosed\n");

const string textHelperSource = """
    import textwrap
    print(repr(textwrap.dedent('  é😀\n \t\n  b')))
    print(repr(textwrap.indent('\x1c\x85\xa0\n😀','>')))
    print('\x1c\x1f'.isspace(),len('a\x1cb\x85c'.splitlines()))
    print(repr('\u0897\U000f0000'))
    """;
var textHelpers = engine.Compile(textHelperSource);
if (!textHelpers.IsValid)
    throw new Exception(string.Join("; ", textHelpers.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { textHelpers.Run(new PureHost()), await textHelpers.RunAsync(new PureHost()) })
    RequireOutput(result, "'é😀\\n\\nb'\n'\\x1c\\x85\\xa0\\n>😀'\nTrue 3\n'\\u0897\\U000f0000'\n");

const string htmlHelperSource = "import html, textwrap\nprint(repr(html.escape(\"\u00e9\ud83d\ude00<&\\\"'\")))\nprint(repr(html.unescape('&amp;&NotEqualTilde;&notit;&acE;&#0;&#128;&#x1f600;&#xFDD0;')))\nclass Truth:\n    def __bool__(self):\n        return False\ntruth = Truth(); truth.__bool__ = lambda: True\nclass Text:\n    def __contains__(self, needle):\n        return truth\ntext = Text(); text.__contains__ = lambda needle: True\nprint(bool(truth), 'x' in text, html.unescape(text) is text)\ntry:\n    textwrap.indent(text='x', *['y'])\nexcept TypeError:\n    print('collision')\n";
var htmlHelpers = engine.Compile(htmlHelperSource);
if (!htmlHelpers.IsValid)
    throw new Exception(string.Join("; ", htmlHelpers.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { htmlHelpers.Run(new PureHost()), await htmlHelpers.RunAsync(new PureHost()) })
    RequireOutput(result, "'\u00e9\ud83d\ude00&lt;&amp;&quot;&#x27;'\n'&\u2242\u0338\u00acit;\u223e\u0333\ufffd\u20ac\ud83d\ude00'\nFalse False True\ncollision\n");

const string asciiCodecSource = "print(repr('\u00e9\ud83d\ude00'.encode('ascii','backslashreplace')))\nprint(repr(b'A\\x80\\xff\\r\\n'.decode('US-ASCII','replace')))\nprint(str(bytes('\u00e9','ascii','ignore'),'ascii')=='')\n";
var asciiCodecs = engine.Compile(asciiCodecSource);
if (!asciiCodecs.IsValid)
    throw new Exception(string.Join("; ", asciiCodecs.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { asciiCodecs.Run(new PureHost()), await asciiCodecs.RunAsync(new PureHost()) })
    RequireOutput(result, "b'\\\\xe9\\\\U0001f600'\n'A\ufffd\ufffd\\r\\n'\nTrue\n");

const string urlHelperSource = "import urllib.parse as p\nprint(p.quote('/\u00e9\ud83d\ude00 a'),p.quote_plus('/\u00e9\ud83d\ude00 a'))\nprint(repr(p.unquote('\u00e9%FF\ud83d\ude00%C3%A9',errors='replace')))\nprint(p.urlencode([('x',['a b',b'\\xff']),('y',[])],doseq=True))\nprint(repr(p.parse_qs(b'x=%FF&x=a+b')),repr(p.parse_qsl('a=&a=\u00e9+\ud83d\ude00',keep_blank_values=True)))\nprint(p.quote is p.quote,{p.quote:'function'}[p.quote])\nclass Sequence:\n    def __getitem__(self,index):\n        if index>=3:\n            raise IndexError\n        return index\nprint(list(Sequence()))\n";
var urlHelpers = engine.Compile(urlHelperSource);
if (!urlHelpers.IsValid)
    throw new Exception(string.Join("; ", urlHelpers.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { urlHelpers.Run(new PureHost()), await urlHelpers.RunAsync(new PureHost()) })
    RequireOutput(result, "/%C3%A9%F0%9F%98%80%20a %2F%C3%A9%F0%9F%98%80+a\n'\u00e9\ufffd\ud83d\ude00\u00e9'\nx=a+b&x=%FF\n{b'x': [b'\\xff', b'a b']} [('a', ''), ('a', '\u00e9 \ud83d\ude00')]\nTrue function\n[0, 1, 2]\n");

const string structuredUrlSource = "import urllib.parse as p\nvalue=p.urlparse('HTTP://u:p@EXAMPLE:080/a;b?x=1#f')\nprint(tuple(value),value.username,value.password,value.hostname,value.port,value.geturl())\nprint(tuple(value._replace(netloc='Y',fragment='')),tuple(value.encode()),value.encode().decode()==value)\nprint(p.urlunsplit((b'https',b'X',b'a',b'x',b'f')))\nprint(isinstance(value,tuple),value==tuple(value),{value:'key'}[tuple(value)])\ntry:\n    print(p.urlsplit('http://x:65536').port)\nexcept ValueError:\n    print('port')\n";
var structuredUrls = engine.Compile(structuredUrlSource);
if (!structuredUrls.IsValid)
    throw new Exception(string.Join("; ", structuredUrls.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { structuredUrls.Run(new PureHost()), await structuredUrls.RunAsync(new PureHost()) })
    RequireOutput(result, "('http', 'u:p@EXAMPLE:080', '/a', 'b', 'x=1', 'f') u p example 80 http://u:p@EXAMPLE:080/a;b?x=1#f\n('http', 'Y', '/a', 'b', 'x=1', '') (b'http', b'u:p@EXAMPLE:080', b'/a', b'b', b'x=1', b'f') True\nb'https://X/a?x#f'\nTrue True key\nport\n");

const string urlCacheSource = "import urllib.parse as p\nclass Flag:\n    def __hash__(self):\n        print('hash')\n        return 7\n    def __bool__(self):\n        print('bool')\n        return False\nflag=Flag()\nleft=p.urlsplit('http://x/package-cache#f',allow_fragments=flag)\nright=p.urlsplit('http://x/package-cache#f',allow_fragments=flag)\nprint(left is right,tuple(right))\nprint(p.urlsplit('http://x/package-cache') is p.urlsplit(url='http://x/package-cache'))\n";
var urlCache = engine.Compile(urlCacheSource);
if (!urlCache.IsValid)
    throw new Exception(string.Join("; ", urlCache.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { urlCache.Run(new PureHost()), await urlCache.RunAsync(new PureHost()) })
    RequireOutput(result, "hash\nbool\nhash\nTrue ('http', 'x', '/package-cache#f', '', '')\nFalse\n");

const string pathByteSource = "from pathlib import Path\np=Path('byte-output.bin')\nprint(p.write_bytes(data=bytes([0,255,10])))\nvalue=p.read_bytes()\nprint(repr(value),len(value),value[1],type(value) is bytes)\nprint(p.write_bytes(b''),repr(p.read_bytes()))\n";
var pathBytes = engine.Compile(pathByteSource);
if (!pathBytes.IsValid)
    throw new Exception(string.Join("; ", pathBytes.Diagnostics.Select(d => d.Message)));
RequireOutput(pathBytes.Run(new MemoryHost(delayed: false)), "3\nb'\\x00\\xff\\n' 3 255 True\n0 b''\n");
var byteHost = new MemoryHost(delayed: true);
var pendingBytes = pathBytes.RunAsync(byteHost);
await byteHost.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(20));
byteHost.ReleaseWrite.TrySetResult();
await byteHost.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(20));
byteHost.ReleaseRead.TrySetResult();
RequireOutput(await pendingBytes, "3\nb'\\x00\\xff\\n' 3 255 True\n0 b''\n");
if (byteHost.SuspendedOperations < 2)
    throw new Exception("Binary package consumer did not suspend during both acquisition and publication.");

const string fileSource = """
    import json
    with open('/input.json') as source:
        data = json.load(source)
    with open('/output.json', 'w') as destination:
        json.dump(data, destination, ensure_ascii=False, sort_keys=True)
    pending = open('/pending.txt', 'w')
    pending.write('final 😀 bytes\n')
    print(data['value'])
    """;
var files = engine.Compile(fileSource);
if (!files.IsValid)
    throw new Exception(string.Join("; ", files.Diagnostics.Select(d => d.Message)));
var syncHost = new MemoryHost(delayed: false);
RequireOutput(files.Run(syncHost), "3\n");
syncHost.VerifyFiles();

var delayedHost = new MemoryHost(delayed: true);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var pendingRun = files.RunAsync(delayedHost, new LythonRunOptions { CancellationToken = timeout.Token });
await delayedHost.ReadStarted.Task.WaitAsync(timeout.Token);
if (pendingRun.IsCompleted) throw new Exception("RunAsync did not await the paused host read.");
delayedHost.ReleaseRead.TrySetResult();
await delayedHost.WriteStarted.Task.WaitAsync(timeout.Token);
if (pendingRun.IsCompleted) throw new Exception("RunAsync did not await file publication.");
delayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await pendingRun.WaitAsync(timeout.Token), "3\n");
delayedHost.VerifyFiles();
if (delayedHost.SuspendedOperations < 2) throw new Exception("Delayed consumer did not suspend.");
const string binaryFileSource = """
    import io
    from pathlib import Path
    with Path('source.bin').open('rb') as reader:
        print(reader.read(2).hex(),reader.readline().hex(),iter(reader) is reader)
        print(reader.read().hex(),reader.read().hex())
    with open('output.bin','wb') as writer:
        writer.writelines([b'head',bytes([0,255,13,10])])
    with open('output.bin','ab') as writer:
        writer.write(b'!')
    print(Path('output.bin').read_bytes().hex())
    open('pending.bin','wb').write(b'final')
    print(issubclass(io.UnsupportedOperation,OSError),issubclass(io.UnsupportedOperation,ValueError))
    """;
var binaryFiles = engine.Compile(binaryFileSource);
if (!binaryFiles.IsValid)
    throw new Exception(string.Join("; ", binaryFiles.Diagnostics.Select(d => d.Message)));
var binarySyncHost = new MemoryHost(delayed: false);
RequireOutput(binaryFiles.Run(binarySyncHost), "00ff 410d0a True\n420a656e64 \n6865616400ff0d0a21\nTrue True\n");
binarySyncHost.VerifyBinaryFiles();
var binaryDelayedHost = new MemoryHost(delayed: true);
using var binaryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var binaryPendingRun = binaryFiles.RunAsync(binaryDelayedHost, new LythonRunOptions { CancellationToken = binaryTimeout.Token });
await binaryDelayedHost.ReadStarted.Task.WaitAsync(binaryTimeout.Token);
if (binaryPendingRun.IsCompleted) throw new Exception("Binary reader did not suspend.");
binaryDelayedHost.ReleaseRead.TrySetResult();
await binaryDelayedHost.WriteStarted.Task.WaitAsync(binaryTimeout.Token);
if (binaryPendingRun.IsCompleted) throw new Exception("Binary publication did not suspend.");
binaryDelayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await binaryPendingRun.WaitAsync(binaryTimeout.Token), "00ff 410d0a True\n420a656e64 \n6865616400ff0d0a21\nTrue True\n");
binaryDelayedHost.VerifyBinaryFiles();
if (binaryDelayedHost.SuspendedOperations < 2) throw new Exception("Binary consumer did not suspend twice.");

Console.WriteLine("Package compatibility and mediated file consumer passed.");

static void RequireOutput(LythonExecutionResult result, string expected)
{
    if (!result.Success || result.StandardOutput != expected)
        throw new Exception("Package consumer failed: " + result.Failure?.Message + " Output: " + result.StandardOutput);
}

class PureHost : ILythonHost
{
    public string Cwd => "/";
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

sealed class MemoryHost(bool delayed) : PureHost, ILythonHost, ILythonSynchronousHostCapability
{
    public bool CompletesSynchronously => !delayed;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal)
    {
        ["/input.json"] = Encoding.UTF8.GetBytes("{\"value\": 3, \"name\": \"é\"}"),
        ["/source.bin"] = [0, 255, 65, 13, 10, 66, 10, 101, 110, 100]
    };
    public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int SuspendedOperations { get; private set; }

    public override async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (delayed && !ReleaseRead.Task.IsCompleted)
        {
            ReadStarted.TrySetResult();
            await ReleaseRead.Task.WaitAsync(cancellationToken);
            SuspendedOperations++;
        }
        return _files[path];
    }

    public override async ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (delayed && !ReleaseWrite.Task.IsCompleted)
        {
            WriteStarted.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(cancellationToken);
            SuspendedOperations++;
        }
        _files[path] = utf8.ToArray();
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        => ReadTextUtf8Async(path, cancellationToken);
    public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        => WriteTextUtf8Async(path, bytes, cancellationToken);

    public override ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stat = path == "/"
            ? new LythonPathStat(LythonPathKind.Directory, 0, null)
            : _files.TryGetValue(path, out var bytes)
                ? new LythonPathStat(LythonPathKind.File, bytes.Length, null)
                : new LythonPathStat(LythonPathKind.Missing, 0, null);
        return ValueTask.FromResult(stat);
    }

    public void VerifyBinaryFiles()
    {
        if (!_files["/output.bin"].AsSpan().SequenceEqual(new byte[] { 104, 101, 97, 100, 0, 255, 13, 10, 33 })
            || !_files["/pending.bin"].AsSpan().SequenceEqual("final"u8))
            throw new Exception("Binary package consumer produced incorrect bytes.");
    }

    public void VerifyFiles()
    {
        if (!_files["/output.json"].AsSpan().SequenceEqual("{\"name\": \"é\", \"value\": 3}"u8)
            || !_files["/pending.txt"].AsSpan().SequenceEqual("final 😀 bytes\n"u8))
            throw new Exception("Package file publication produced incorrect bytes.");
    }
}
