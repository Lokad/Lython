using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BinaryFileCompatibilityTests
{
    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task BinaryHandlesSuspendAndPublishThroughDelayedHost(string name, string source,
        string expected, string seedHex, string finalSourceHex, string? finalOutputHex)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedBytes("/source.bin", Convert.FromHexString(seedHex));
        host.SeedBytes("/empty.bin", []);
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Equal(Convert.FromHexString(finalSourceHex), host.ReadBytes("/source.bin"));
        if (finalOutputHex is not null) Assert.Equal(Convert.FromHexString(finalOutputHex), host.ReadBytes("/output.bin"));
        if (name == "binary-invalid-text-options") Assert.Equal(0, host.CompletedAsynchronously);
        else Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsBinaryAcquisitionOrPublication(bool write)
    {
        var host = new DelayedLythonHost();
        host.SeedBytes("/blob", [1, 2, 3]);
        var started = write ? host.PauseWriteUntilCancellation("/blob") : host.PauseReadUntilCancellation("/blob");
        var source = write ? "with open('/blob','wb') as f:\n    f.write(b'changed')\nprint('published')"
            : "with open('/blob','rb') as f:\n    print(f.read())";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("", result.StandardOutput);
        Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/blob"));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    [InlineData(0, false)]
    public async Task BinaryReadHonorsHostByteLimit(int cap, bool success)
    {
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/blob", [0, 1, 2, 3, 255]);
            var options = new LythonRunOptions { MaxHostReadBytes = cap };
            var script = new LythonEngine().Compile("with open('/blob','rb') as f:\n    print(len(f.read()))");
            Assert.True(script.IsValid);
            var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            Assert.Equal(success, result.Success);
            if (success) Assert.Equal("5\n", result.StandardOutput);
            else Assert.Contains("host binary read exceeded maximum bytes", result.Failure?.Message);
        }
    }

    [Fact]
    public async Task HostWriteFailurePreservesExistingBytes()
    {
        var script = new LythonEngine().Compile("with open('/blob','wb') as f:\n    f.write(b'changed')\nprint('published')");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/blob", [1, 2, 3]);
            host.FailWriteBytes("/blob", "publication denied");
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Contains("write_bytes", result.Failure?.Message);
            Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/blob"));
        }
    }

    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "binary-direction-exception", "import io\nprint(issubclass(io.UnsupportedOperation,OSError),issubclass(io.UnsupportedOperation,ValueError))\nwith open('source.bin','rb') as f:\n    print(f.readable(),f.writable())\n    for value in [b'x','text']:\n        try: f.write(value)\n        except io.UnsupportedOperation as e:\n            print(type(e).__name__,isinstance(e,OSError),isinstance(e,ValueError))\nwith open('output.bin','wb') as f:\n    print(f.readable(),f.writable())\n    for method in [f.read,f.readline,f.readlines]:\n        try: method()\n        except io.UnsupportedOperation: print('unsupported')\n", "True True\nTrue False\nUnsupportedOperation True True\nUnsupportedOperation True True\nFalse True\nunsupported\nunsupported\nunsupported\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "" };
        yield return new object[] { "binary-append-flush-close", "from pathlib import Path\nwith open('source.bin','ab') as f:\n    print(f.tell(),f.write(b'!'),f.tell())\n    print(f.flush(),f.tell(),f.write(b'?'))\n    f.close(); print(f.close(),f.closed)\nprint(repr(Path('source.bin').read_bytes()))\n", "10 1 11\nNone 11 1\nNone True\nb'\\x00\\xffA\\r\\nB\\nend!?'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64213f", null! };
        yield return new object[] { "binary-read-chunks", "with open('source.bin','rb') as file:\n    print(repr(file.read(2)),repr(file.read(0)),repr(file.read(3)),repr(file.read()),repr(file.read()))\nprint(file.closed)\n", "b'\\x00\\xff' b'' b'A\\r\\n' b'B\\nend' b''\nTrue\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-read-lines", "with open('source.bin','rb') as file:\n    print(repr(file.readline(2)))\n    print(repr(file.readline()))\n    print(repr(file.readline(1)))\n    print(repr(file.readline()))\n    print(repr(file.readline()),repr(file.readline()))\n", "b'\\x00\\xff'\nb'A\\r\\n'\nb'B'\nb'\\n'\nb'end' b''\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-iteration-position", "with open('source.bin','rb') as file:\n    print(iter(file) is file)\n    print(repr(file.read(1)))\n    print(repr(next(file)))\n    print(repr(file.read(1)))\n    print(list(file))\n    try: next(file)\n    except StopIteration: print('end')\n", "True\nb'\\x00'\nb'\\xffA\\r\\n'\nb'B'\n[b'\\n', b'end']\nend\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-write-and-close", "from pathlib import Path\nfile=open('output.bin','wb')\nprint(file.write(bytes([0,255,10])),file.write(b''))\ntry: file.write('text')\nexcept TypeError: print('type')\nprint(file.close(),file.close(),file.closed)\nprint(repr(Path('output.bin').read_bytes()))\ntry: file.write(b'x')\nexcept ValueError: print('closed')\n", "3 0\ntype\nNone None True\nb'\\x00\\xff\\n'\nclosed\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "00ff0a" };
        yield return new object[] { "binary-append-preserves-prefix", "from pathlib import Path\nwith open('source.bin','ab') as file:\n    print(file.write(b'!'))\nprint(repr(Path('source.bin').read_bytes()))\nwith open('output.bin','ab') as file:\n    print(file.write(b'new'))\nprint(repr(Path('output.bin').read_bytes()))\n", "1\nb'\\x00\\xffA\\r\\nB\\nend!'\n3\nb'new'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e6421", "6e6577" };
        yield return new object[] { "binary-empty-publication", "from pathlib import Path\nwith open('output.bin','wb') as file: pass\nprint(repr(Path('output.bin').read_bytes()))\nwith open('empty.bin','rb') as file:\n    print(repr(file.read()),repr(file.readline()),list(file))\n", "b''\nb'' b'' []\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "" };
        yield return new object[] { "binary-path-open", "from pathlib import Path\nwith Path('output.bin').open('wb') as file:\n    print(file.write(b'path'))\nwith Path('output.bin').open('rb') as file:\n    print(repr(file.read(2)),repr(file.read()))\n", "4\nb'pa' b'th'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "70617468" };
        yield return new object[] { "binary-context-exception", "from pathlib import Path\ntry:\n    with open('output.bin','wb') as file:\n        file.write(b'written')\n        raise ValueError('caught')\nexcept ValueError: print('caught',file.closed)\nprint(repr(Path('output.bin').read_bytes()))\n", "caught True\nb'written'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "7772697474656e" };
        yield return new object[] { "binary-size-protocols", "class Size:\n    def __index__(self): print('index'); return 2\nwith open('source.bin','rb') as f:\n    print(repr(f.read(Size())))\n    print(repr(f.readline(Size())))\n    for method in [f.read,f.readline,f.readlines]:\n        try: print(repr(method(None)))\n        except TypeError: print('none type')\n", "index\nb'\\x00\\xff'\nindex\nb'A\\r'\nb'\\nB\\nend'\nb''\n[]\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-size-errors", "with open('source.bin','rb') as f:\n    for value in [1.5,'2',10**100,-10**100]:\n        try: print(repr(f.read(value)))\n        except TypeError: print('type')\n        except OverflowError: print('overflow')\n    print(repr(f.read(True)),repr(f.read(False)))\n    try: f.read(-2)\n    except ValueError: print('negative value')\n", "type\ntype\noverflow\noverflow\nb'\\x00' b''\nnegative value\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-readlines-hints", "with open('source.bin','rb') as f:\n    print(f.readlines(5),f.tell())\n    print(f.readlines(-1),f.tell())\nwith open('source.bin','rb') as f:\n    print(f.readlines(1),f.readlines(0))\n", "[b'\\x00\\xffA\\r\\n', b'B\\n'] 7\n[b'end'] 10\n[b'\\x00\\xffA\\r\\n'] [b'B\\n', b'end']\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-readlines-index", "class Size:\n    def __index__(self): print('index'); return 1\nwith open('source.bin','rb') as f:\n    try: print(f.readlines(Size()))\n    except TypeError: print('type')\n", "index\n[b'\\x00\\xffA\\r\\n']\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-writelines-and-flush", "from pathlib import Path\nwith open('output.bin','wb') as f:\n    print(f.writelines([b'a',bytes([255]),bytes([13,10])]))\n    print(f.tell(),f.flush())\n    f.write(b'z')\n    print(f.flush(),f.tell())\nprint(repr(Path('output.bin').read_bytes()))\n", "None\n4 None\nNone 5\nb'a\\xff\\r\\nz'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "61ff0d0a7a" };
        yield return new object[] { "binary-closed-validation", "f=open('source.bin','rb'); f.close()\nfor method,value in [(f.read,None),(f.readline,None),(f.read,'bad'),(f.write,'bad'),(f.write,b'x')]:\n    try: method(value)\n    except TypeError: print('type')\n    except ValueError: print('closed')\n", "closed\nclosed\ntype\nclosed\nclosed\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-keyword-options", "from pathlib import Path\nwith open(file='source.bin',mode='rb',encoding=None,errors=None,newline=None) as f:\n    print(f.mode,hasattr(f,'encoding'),hasattr(f,'errors'))\n    for method,kwargs in [(f.read,{'size':1}),(f.readline,{'size':1}),(f.readlines,{'hint':1})]:\n        try: print(repr(method(**kwargs)))\n        except TypeError: print('keyword type')\nwith Path('output.bin').open(mode='wb') as f:\n    try: f.write(data=b'x')\n    except TypeError: print('write keyword type')\n", "rb False False\nkeyword type\nkeyword type\nkeyword type\nwrite keyword type\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "" };
        yield return new object[] { "binary-invalid-text-options", "for key,value in [('encoding','utf-8'),('errors','strict'),('newline','')]:\n    try: open('source.bin','rb',**{key:value})\n    except ValueError: print(key,'value')\nfrom pathlib import Path\nfor key,value in [('encoding','utf-8'),('errors','strict'),('newline','')]:\n    try: Path('source.bin').open('rb',**{key:value})\n    except ValueError: print(key,'value')\n", "encoding value\nerrors value\nnewline value\nencoding value\nerrors value\nnewline value\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", null! };
        yield return new object[] { "binary-reversed-modes", "from pathlib import Path\nwith open('source.bin','br') as f: print(f.mode,repr(f.read(2)))\nwith open('output.bin','bw') as f: print(f.mode,f.write(b'R'))\nwith open('output.bin','ba') as f: print(f.mode,f.write(b'S'))\nprint(repr(Path('output.bin').read_bytes()))\n", "rb b'\\x00\\xff'\nwb 1\nab 1\nb'RS'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "5253" };
        yield return new object[] { "binary-empty-append-create", "from pathlib import Path\nwith open('output.bin','ab') as f: pass\nprint(Path('output.bin').exists(),repr(Path('output.bin').read_bytes()))\nwith open('source.bin','ab') as f: pass\nprint(repr(Path('source.bin').read_bytes()))\n", "True b''\nb'\\x00\\xffA\\r\\nB\\nend'\n", "00ff410d0a420a656e64", "00ff410d0a420a656e64", "" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task BinaryHandlesFollowPythonAndPublishExactBytes(string name, string source,
        string expected, string seedHex, string finalSourceHex, string? finalOutputHex)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/source.bin", Convert.FromHexString(seedHex));
            host.SeedBytes("/empty.bin", []);
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            Assert.Equal(Convert.FromHexString(finalSourceHex), host.ReadBytes("/source.bin"));
            if (finalOutputHex is not null)
                Assert.Equal(Convert.FromHexString(finalOutputHex), host.ReadBytes("/output.bin"));
        }
    }
}
