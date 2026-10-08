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
    real_object = object
    class object:
        pass
    print(issubclass(ValueError, real_object), issubclass(ValueError, object))
    print(isinstance(ValueError('x'), real_object), isinstance(ValueError('x'), object))
    """;
var compatibility = engine.Compile(compatibilitySource);
if (!compatibility.IsValid)
    throw new Exception(string.Join("; ", compatibility.Diagnostics.Select(d => d.Message)));
const string compatibilityOutput = "ab 3\nTrue ['a', 'b'] [['1', '2']] empty\n3\n{\"sample\": {\"x\": 2}}\na a None\nIndexError\n[1, 2]\nTrue False\nTrue False\n";
foreach (var result in new[] { compatibility.Run(new PureHost()), await compatibility.RunAsync(new PureHost()) })
    RequireOutput(result, compatibilityOutput);

const string structSource = """
    import struct,math,json,operator
    data=struct.pack('>bHefds4p?',-3,514,-0.0,1.5,-2.5,b'x',b'abc',True)
    print(struct.calcsize('>bHefds4p?'),data.hex(),struct.unpack('>bHefds4p?',data))
    for value in [float('nan'),float.fromhex('-nan'),math.nan,json.loads('NaN')]:
        print(struct.pack('>d',value).hex())
    iterator=struct.iter_unpack('<2H',struct.pack('<6H',1,2,3,4,5,6))
    print(operator.length_hint(iterator),next(iterator),list(iterator),iterator.__length_hint__())
    print(struct.error.__name__,struct.error.__module__,issubclass(struct.error,Exception))
    """;
var structScript = engine.Compile(structSource);
if (!structScript.IsValid) throw new Exception(string.Join("; ", structScript.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { structScript.Run(new PureHost()), await structScript.RunAsync(new PureHost()) })
    RequireOutput(result, "23 fd020280003fc00000c004000000000000780361626301 (-3, 514, -0.0, 1.5, -2.5, b'x', b'abc', True)\n7ff8000000000000\nfff8000000000000\n7ff8000000000000\n7ff8000000000000\n3 (1, 2) [(3, 4), (5, 6)] 0\nerror struct True\n");

const string xmlSource = "import xml.etree.ElementTree as ET\ndata='<?xml version=\"1.0\" encoding=\"windows-1252\"?><r a=\"\u20ac\">\u00e9\u20ac</r>'.encode('cp1252')\nroot=ET.fromstring(data)\nprint(root.tag,root.attrib,repr(root.text))\nroot=ET.fromstring('<r xmlns:p=\"https://example.test/ns\">before<p:x n=\"1\">\u03a9\ud83d\ude00</p:x>after<p:x n=\"2\"/></r>'.encode('utf-16'))\nprint(root.tag,repr(root.text),[c.tag for c in root],repr(root[0].tail))\nprint(root.find('p:x',{'p':'https://example.test/ns'}) is root[0],[c.get('n') for c in root.findall('{https://example.test/ns}x')])\nprint(type(root) is ET.Element,ET.ParseError.__module__,ET.ParseError.__bases__[0] is SyntaxError)\nprint(ET.ParseError.__bases__ is ET.ParseError.__bases__)\ntry: ET.fromstring('<r>')\nexcept ET.ParseError as error: print(type(error) is ET.ParseError,isinstance(error,SyntaxError))\n";
var xmlScript = engine.Compile(xmlSource);
if (!xmlScript.IsValid) throw new Exception(string.Join("; ", xmlScript.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { xmlScript.Run(new PureHost()), await xmlScript.RunAsync(new PureHost()) })
    RequireOutput(result, "r {'a': '\u20ac'} '\u00e9\u20ac'\nr 'before' ['{https://example.test/ns}x', '{https://example.test/ns}x'] 'after'\nTrue ['1', '2']\nTrue xml.etree.ElementTree True\nTrue\nTrue True\n");

const string zlibSource = "import zlib,gzip,struct\nfor data in [\n    b'',b'hello hello',bytes(range(256))]:\n    encoded=zlib.compress(data,level=7)\n    print(encoded[0],encoded[1]>>6,zlib.decompress(encoded)==data)\nprint(gzip.decompress(gzip.compress(b'',mtime=0)))\nframe=struct.pack('>I',4)+zlib.compress(b'four')\nprint(struct.unpack('>I',frame[:4]),zlib.decompress(frame[4:]+b'unused'))\nprint(zlib.compress.__name__,type(zlib.compress).__name__,zlib.error.__bases__[0] is Exception)\nfor data in [b'bad',zlib.compress(b'one')[:-1]]:\n    try: zlib.decompress(data)\n    except zlib.error as error: print(type(error) is zlib.error)\n";
var zlibScript = engine.Compile(zlibSource);
if (!zlibScript.IsValid) throw new Exception(string.Join("; ", zlibScript.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { zlibScript.Run(new PureHost()), await zlibScript.RunAsync(new PureHost()) })
    RequireOutput(result, "120 3 True\n120 3 True\n120 3 True\nb''\n(4,) b'four'\ncompress builtin_function_or_method True\nTrue\nTrue\n");

const string windows1252Source = """
    data=bytes([i for i in range(256) if i not in [129,141,143,144,157]])
    text=data.decode('cp1252')
    print(len(text),text.encode('cp1252')==data,bytes(text,'cp1252')==data,str(data,'cp1252')==text)
    """;
var windows1252 = engine.Compile(windows1252Source);
if (!windows1252.IsValid)
    throw new Exception(string.Join("; ", windows1252.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { windows1252.Run(new PureHost()), await windows1252.RunAsync(new PureHost()) })
    RequireOutput(result, "251 True True True\n");

const string utf16Source = """
    text='A\U0001f600'
    for encoding in ['utf-16','utf-16-le','utf-16-be']:
        data=bytes(text,encoding)
        print(data.hex(),str(data,encoding)==text)
    print(bytes.fromhex('feff0041').decode('utf-16'),repr(bytes.fromhex('00d8').decode('utf-16-le','replace')))
    """;
var utf16 = engine.Compile(utf16Source);
if (!utf16.IsValid)
    throw new Exception(string.Join("; ", utf16.Diagnostics.Select(d => d.Message)));
foreach (var result in new[] { utf16.Run(new PureHost()), await utf16.RunAsync(new PureHost()) })
    RequireOutput(result, "fffe41003dd800de True\n41003dd800de True\n0041d83dde00 True\nA '\ufffd'\n");

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

const string utf16FileSource = """
    from pathlib import Path
    with open('/utf16-input.bin',encoding='utf-16',newline='') as reader:
        print(repr(reader.read()))
    with Path('/utf16-output.bin').open('w',encoding='utf-16') as writer:
        print(writer.write('A\U0001f600'))
        writer.flush()
        writer.write('\u20ac')
    with open('/utf16-output.bin','a',encoding='utf-16') as writer:
        writer.write('!')
    print(Path('/utf16-output.bin').read_bytes().hex())
    open('/utf16-pending.bin','w',encoding='utf-16').write('')
    """;
var utf16Files = engine.Compile(utf16FileSource);
if (!utf16Files.IsValid)
    throw new Exception(string.Join("; ", utf16Files.Diagnostics.Select(d => d.Message)));
const string utf16FileOutput = "'B\u20ac\ud83d\ude00\\r\\n'\n2\nfffe41003dd800deac202100\n";
var utf16SyncHost = new MemoryHost(delayed: false);
RequireOutput(utf16Files.Run(utf16SyncHost), utf16FileOutput);
utf16SyncHost.VerifyUtf16Files();
var utf16DelayedHost = new MemoryHost(delayed: true);
using var utf16Timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var utf16PendingRun = utf16Files.RunAsync(utf16DelayedHost, new LythonRunOptions { CancellationToken = utf16Timeout.Token });
await utf16DelayedHost.ReadStarted.Task.WaitAsync(utf16Timeout.Token);
if (utf16PendingRun.IsCompleted) throw new Exception("UTF-16 acquisition did not suspend.");
utf16DelayedHost.ReleaseRead.TrySetResult();
await utf16DelayedHost.WriteStarted.Task.WaitAsync(utf16Timeout.Token);
if (utf16PendingRun.IsCompleted) throw new Exception("UTF-16 publication did not suspend.");
utf16DelayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await utf16PendingRun.WaitAsync(utf16Timeout.Token), utf16FileOutput);
utf16DelayedHost.VerifyUtf16Files();
if (utf16DelayedHost.SuspendedOperations < 2) throw new Exception("UTF-16 consumer did not suspend twice.");

const string structFileSource = """
    import struct
    from pathlib import Path
    class Number:
        def __index__(self):
            with open('/struct-index.txt') as file: value=file.read()
            print('index',value)
            return int(value)
        def __float__(self):
            with open('/struct-float.txt') as file: value=file.read()
            print('float',value)
            return float(value)
        def __bool__(self):
            with open('/struct-truth.txt') as file: value=file.read()
            print('truth',value)
            return value=='yes'
    number=Number()
    number.__index__=lambda:999
    number.__float__=lambda:4.0
    number.__bool__=lambda:False
    data=struct.pack('>Hf?',number,number,number)
    print(data.hex(),struct.unpack('>Hf?',data))
    print(Path('/struct-output.bin').write_bytes(data),Path('/struct-output.bin').read_bytes().hex())
    """;
var structFiles = engine.Compile(structFileSource);
if (!structFiles.IsValid) throw new Exception(string.Join("; ", structFiles.Diagnostics.Select(d => d.Message)));
const string structFileOutput = "index 514\nfloat 1.5\ntruth yes\n02023fc0000001 (514, 1.5, True)\n7 02023fc0000001\n";
var structSyncHost = new MemoryHost(delayed: false);
RequireOutput(structFiles.Run(structSyncHost), structFileOutput);
structSyncHost.VerifyStructFiles();
var structDelayedHost = new MemoryHost(delayed: true);
using var structTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var structPendingRun = structFiles.RunAsync(structDelayedHost, new LythonRunOptions { CancellationToken = structTimeout.Token });
await structDelayedHost.ReadStarted.Task.WaitAsync(structTimeout.Token);
if (structPendingRun.IsCompleted) throw new Exception("Struct conversion did not suspend.");
structDelayedHost.ReleaseRead.TrySetResult();
await structDelayedHost.WriteStarted.Task.WaitAsync(structTimeout.Token);
if (structPendingRun.IsCompleted) throw new Exception("Struct publication did not suspend.");
structDelayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await structPendingRun.WaitAsync(structTimeout.Token), structFileOutput);
structDelayedHost.VerifyStructFiles();
if (structDelayedHost.SuspendedOperations < 2) throw new Exception("Struct consumer did not suspend twice.");

const string xmlFileSource = "import xml.etree.ElementTree as ET\nimport operator\nfrom pathlib import Path\nclass Index:\n    def __index__(self):\n        with open('/xml-index.txt') as file: value=file.read()\n        print('index',value)\n        return int(value)\nindex=Index()\nindex.__index__=lambda: 0\nroot=ET.fromstring('<r><a>A</a><b>B</b><c>C</c></r>')\nprint(root[index].tag,[element.tag for element in root[:index]])\nprint(operator.getitem(root,index).text)\nPath('/xml-output.txt').write_text(root[index].text)\n";
var xmlFiles = engine.Compile(xmlFileSource);
if (!xmlFiles.IsValid) throw new Exception(string.Join("; ", xmlFiles.Diagnostics.Select(d => d.Message)));
const string xmlFileOutput = "index 1\nindex 1\nb ['a']\nindex 1\nB\nindex 1\n";
var xmlSyncHost = new MemoryHost(delayed: false);
RequireOutput(xmlFiles.Run(xmlSyncHost), xmlFileOutput);
xmlSyncHost.VerifyXmlFiles();
var xmlDelayedHost = new MemoryHost(delayed: true);
using var xmlTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var xmlPendingRun = xmlFiles.RunAsync(xmlDelayedHost, new LythonRunOptions { CancellationToken = xmlTimeout.Token });
await xmlDelayedHost.ReadStarted.Task.WaitAsync(xmlTimeout.Token);
if (xmlPendingRun.IsCompleted) throw new Exception("XML index conversion did not suspend.");
xmlDelayedHost.ReleaseRead.TrySetResult();
await xmlDelayedHost.WriteStarted.Task.WaitAsync(xmlTimeout.Token);
if (xmlPendingRun.IsCompleted) throw new Exception("XML result publication did not suspend.");
xmlDelayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await xmlPendingRun.WaitAsync(xmlTimeout.Token), xmlFileOutput);
xmlDelayedHost.VerifyXmlFiles();
if (xmlDelayedHost.SuspendedOperations < 2) throw new Exception("XML consumer did not suspend twice.");

const string zlibFileSource = "import zlib,json,struct\nfrom pathlib import Path\nclass Index:\n    def __init__(self,label,value):\n        self.label=label\n        self.value=value\n    def __index__(self):\n        with open('/zlib-index.txt') as file: seed=file.read()\n        print('index',self.label,seed)\n        return self.value\nlevel=Index('level',1)\nlevel.__index__=lambda: 9\ncompressed=bytes.fromhex('789cab56ca4bcc4d55b252503abcf2dcca0ff3673428e9282825e797e69500058d6b01d3f90c4c')\nraw=zlib.decompress(compressed,Index('window',15),Index('buffer',1))\nprint(json.loads(raw.decode('utf-8')))\nagain=zlib.compress(raw,level,Index('window',15))\nframe=struct.pack('>I',len(raw))+again\nprint(struct.unpack('>I',frame[:4]),zlib.decompress(frame[4:])==raw)\nprint(Path('/zlib-output.bin').write_bytes(zlib.decompress(frame[4:])))\n";
var zlibFiles = engine.Compile(zlibFileSource);
if (!zlibFiles.IsValid) throw new Exception(string.Join("; ", zlibFiles.Diagnostics.Select(d => d.Message)));
const string zlibFileOutput = "index window 1\nindex buffer 1\n{'name': '\u00e9\u03a9\ud83d\ude00', 'count': 3}\nindex level 1\nindex window 1\n(32,) True\n32\n";
var zlibSyncHost = new MemoryHost(delayed: false);
RequireOutput(zlibFiles.Run(zlibSyncHost), zlibFileOutput);
zlibSyncHost.VerifyZlibFiles();
var zlibDelayedHost = new MemoryHost(delayed: true);
using var zlibTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var zlibPendingRun = zlibFiles.RunAsync(zlibDelayedHost, new LythonRunOptions { CancellationToken = zlibTimeout.Token });
await zlibDelayedHost.ReadStarted.Task.WaitAsync(zlibTimeout.Token);
if (zlibPendingRun.IsCompleted) throw new Exception("Zlib option conversion did not suspend.");
zlibDelayedHost.ReleaseRead.TrySetResult();
await zlibDelayedHost.WriteStarted.Task.WaitAsync(zlibTimeout.Token);
if (zlibPendingRun.IsCompleted) throw new Exception("Zlib publication did not suspend.");
zlibDelayedHost.ReleaseWrite.TrySetResult();
RequireOutput(await zlibPendingRun.WaitAsync(zlibTimeout.Token), zlibFileOutput);
zlibDelayedHost.VerifyZlibFiles();
if (zlibDelayedHost.SuspendedOperations < 2) throw new Exception("Zlib consumer did not suspend twice.");

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
        ["/zlib-index.txt"] = "1"u8.ToArray(),
        ["/xml-index.txt"] = "1"u8.ToArray(),
        ["/struct-index.txt"] = "514"u8.ToArray(),
        ["/struct-float.txt"] = "1.5"u8.ToArray(),
        ["/struct-truth.txt"] = "yes"u8.ToArray(),
        ["/source.bin"] = [0, 255, 65, 13, 10, 66, 10, 101, 110, 100],
        ["/utf16-input.bin"] = [0xfe, 0xff, 0, 0x42, 0x20, 0xac, 0xd8, 0x3d, 0xde, 0, 0, 13, 0, 10],
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

    public void VerifyZlibFiles()
    {
        if (!_files["/zlib-output.bin"].AsSpan().SequenceEqual(Convert.FromHexString("7b226e616d65223a2022c3a9cea9f09f9880222c2022636f756e74223a20337d")))
            throw new Exception("Zlib package consumer produced incorrect bytes.");
    }

    public void VerifyXmlFiles()
    {
        if (!_files["/xml-output.txt"].AsSpan().SequenceEqual("B"u8))
            throw new Exception("XML package consumer produced incorrect bytes.");
    }

    public void VerifyStructFiles()
    {
        if (!_files["/struct-output.bin"].AsSpan().SequenceEqual(Convert.FromHexString("02023fc0000001")))
            throw new Exception("Struct package consumer produced incorrect bytes.");
    }

    public void VerifyUtf16Files()
    {
        if (!_files["/utf16-output.bin"].AsSpan().SequenceEqual(Convert.FromHexString("fffe41003dd800deac202100"))
            || !_files["/utf16-pending.bin"].AsSpan().SequenceEqual(new byte[] { 0xff, 0xfe }))
            throw new Exception("UTF-16 package consumer produced incorrect bytes.");
    }
}
