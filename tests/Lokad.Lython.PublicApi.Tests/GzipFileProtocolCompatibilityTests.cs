using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileProtocolCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "gzipfile-optional-header-fields", "import gzip,io\nmember=bytes.fromhex('1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000')\nheader=member[:3]+bytes([30])+member[4:10]\ndata=header+b'\\x03\\x00abcname\\x00comment\\x00\\x00\\x00'+member[10:]\nreader=gzip.GzipFile(fileobj=io.BytesIO(data))\nprint(reader.read(0),reader.mtime)\nprint(reader.read(),reader.mtime,reader.tell())\nreader.close()\n", "b'' None\nb'alpha\\nbeta\\n' 123 11\n" };
        yield return new object[] { "gzipfile-truncated-optional-header-metadata", "import gzip,io\nbase=bytes.fromhex('1f8b08007b00000002ff')\nfor flag,extra in [(4,b'\\x04\\x00x'),(8,b'name'),(16,b'comment'),(2,b'x')]:\n    data=base[:3]+bytes([flag])+base[4:]+extra\n    reader=gzip.GzipFile(fileobj=io.BytesIO(data))\n    try: reader.read()\n    except Exception as error: print(flag,type(error).__name__,reader.mtime)\n    reader.close()\n", "4 EOFError None\n8 EOFError 123\n16 EOFError 123\n2 EOFError None\n" };
        yield return new object[] { "gzipfile-flush-callback-error-keeps-writer-open", "import gzip,io\nclass Raw:\n    def __init__(self): self.buffer=io.BytesIO(); self.fail=True\n    def write(self,data): return self.buffer.write(data)\n    def flush(self):\n        if self.fail:\n            self.fail=False\n            raise ValueError('flush failure')\nraw=Raw()\nwriter=gzip.GzipFile(fileobj=raw,mode='wb',mtime=0)\nwriter.write(b'alpha')\ntry: writer.flush()\nexcept ValueError as error: print(str(error),writer.closed)\nwriter.write(b'beta')\nwriter.close()\nprint(writer.closed,gzip.decompress(raw.buffer.getvalue()))\n", "flush failure False\nTrue b'alphabeta'\n" };
        yield return new object[] { "gzipfile-close-final-output-error-closes-wrapper", "import gzip,io\nclass Raw:\n    def __init__(self): self.buffer=io.BytesIO(); self.fail=False; self.closed=False\n    def write(self,data):\n        if self.fail: raise ValueError('write failure')\n        return self.buffer.write(data)\nraw=Raw()\nwriter=gzip.GzipFile(fileobj=raw,mode='wb',mtime=0)\nwriter.write(b'alpha')\nwriter.tell()\nraw.fail=True\ntry: writer.close()\nexcept ValueError as error: print(str(error),writer.closed,raw.closed,writer.fileobj)\nwriter.close()\nprint(writer.closed,raw.closed)\n", "write failure True False None\nTrue False\n" };
        yield return new object[] { "gzipfile-close-callback-error-closes-wrapper", "import gzip,io\nclass Raw:\n    def __init__(self): self.buffer=io.BytesIO(); self.fail=False; self.closed=False\n    def write(self,data):\n        if self.fail: raise ValueError('write failure')\n        return self.buffer.write(data)\nraw=Raw()\nwriter=gzip.GzipFile(fileobj=raw,mode='wb',mtime=0)\nwriter.write(b'alpha')\nraw.fail=True\ntry: writer.close()\nexcept ValueError as error: print(str(error),writer.closed,raw.closed,writer.fileobj)\nwriter.close()\nprint(writer.closed,raw.closed)\n", "write failure True False None\nTrue False\n" };
        yield return new object[] { "gzipfile-context-error-finalizes-valid-data", "import gzip,io\nraw=io.BytesIO()\ntry:\n    with gzip.GzipFile(fileobj=raw,mode='wb',mtime=0) as writer:\n        writer.write(b'valid')\n        raise ValueError('body failure')\nexcept ValueError as error: print(str(error),writer.closed,raw.closed)\nprint(gzip.decompress(raw.getvalue()))\n", "body failure True False\nb'valid'\n" };
        yield return new object[] { "gzipfile-reader-byte-at-a-time-and-member-padding", "import gzip,io\nfirst=bytes.fromhex('1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000')\nsecond=bytes.fromhex('1f8b0800c801000002ff2b49cccc01005db4377c04000000')\nreader=gzip.GzipFile(fileobj=io.BytesIO(first+b'\\x00'*9+second+b'\\x00'*2))\npieces=[]\nfor index in range(16):\n    piece=reader.read(1)\n    pieces.append(piece)\n    print(index,piece,reader.tell(),reader.mtime)\nreader.close()\nprint(b''.join(pieces))\n", "0 b'a' 1 123\n1 b'l' 2 123\n2 b'p' 3 123\n3 b'h' 4 123\n4 b'a' 5 123\n5 b'\\n' 6 123\n6 b'b' 7 123\n7 b'e' 8 123\n8 b't' 9 123\n9 b'a' 10 123\n10 b'\\n' 11 123\n11 b't' 12 456\n12 b'a' 13 456\n13 b'i' 14 456\n14 b'l' 15 456\n15 b'' 15 456\nb'alpha\\nbeta\\ntail'\n" };
        yield return new object[] { "gzipfile-empty-and-malformed-initial-input", "import gzip,io\nfor data in [b'',b'\\x00'*5,b'\\x1f',b'\\x1f\\x8b',b'\\x1f\\x8b'+b'\\x00'*8]:\n    reader=gzip.GzipFile(fileobj=io.BytesIO(data))\n    try: print('read',reader.read(),reader.mtime)\n    except Exception as error: print(type(error).__name__,reader.mtime)\n    reader.close()\n", "read b'' None\nBadGzipFile None\nBadGzipFile None\nEOFError None\nBadGzipFile None\n" };
        yield return new object[] { "gzipfile-writer-reader-method-direction-errors", "import gzip,io\nreader=gzip.GzipFile(fileobj=io.BytesIO())\nwriter=gzip.GzipFile(fileobj=io.BytesIO(),mode='wb',mtime=0)\nfor method,args in [(reader.write,[b'x']),(reader.writelines,[[b'x']]),(writer.read,[]),(writer.readline,[]),(writer.readlines,[]),(writer.__next__,[])]:\n    try: method(*args)\n    except Exception as error: print(type(error).__name__)\nreader.close();writer.close()\n", "OSError\nOSError\nOSError\nUnsupportedOperation\nUnsupportedOperation\nUnsupportedOperation\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ProtocolEdgesFollowFrozenPython(string name, string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
