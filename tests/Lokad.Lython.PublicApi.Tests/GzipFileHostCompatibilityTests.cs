using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileHostCompatibilityTests
{
    public static IEnumerable<object[]> PythonFileControls()
    {
        yield return new object[] { "gzipfile-filename-reader-lifecycle", "import gzip\nreader=gzip.GzipFile('input.gz')\nprint(reader.name,reader.mode,reader.mtime,reader.tell(),reader.closed)\nprint(reader.read(0),reader.mtime)\nprint(reader.read(2),reader.mtime,reader.tell())\nprint(reader.readline(),reader.readlines(),reader.tell())\nreader.close()\nprint(reader.closed)\n", "input.gz rb None 0 False\nb'' None\nb'al' 123 2\nb'pha\\n' [b'beta\\n'] 11\nTrue\n", new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000", ["bad.gz"] = "696e76616c6964", ["truncated.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000", ["checksum.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402000000000000000000", ["index.txt"] = "31" }, new Dictionary<string,string> {  } };
        yield return new object[] { "gzipfile-filename-writer-header-and-pathlike", "import gzip\nfrom pathlib import Path\nwriter=gzip.GzipFile(Path('nested/report.gz'),mode='w',compresslevel=1,mtime=169.75)\nprint(writer.name.replace('\\\\','/'),writer.mode,writer.closed,writer.writable(),writer.readable())\nprint(writer.write(b'alpha\\nbeta\\n'),writer.tell())\nwriter.close()\ndata=Path('nested/report.gz').read_bytes()\nprint(writer.closed,data[:10].hex(),data[10:17],gzip.decompress(data))\nwriter.close()\n", "nested/report.gz wb False True False\n11 11\nTrue 1f8b0808a900000004ff b'report\\x00' b'alpha\\nbeta\\n'\n", new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000", ["bad.gz"] = "696e76616c6964", ["truncated.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000", ["checksum.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402000000000000000000", ["index.txt"] = "31" }, new Dictionary<string,string> { ["nested/report.gz"] = "1f8b0808a900000004ff7265706f7274004bcc29c848e44a4a2d49e402006e50306e0b000000" } };
        yield return new object[] { "gzipfile-filename-append-canonical-mode", "import gzip\nfrom pathlib import Path\nwith gzip.GzipFile('input.gz',mode='ab',mtime=456) as writer:\n    print(writer.name,writer.mode,writer.tell())\n    print(writer.write(b'tail'),writer.tell())\nwith gzip.GzipFile('input.gz',mode='r') as reader:\n    print(reader.mode,reader.mtime,reader.read(),reader.mtime,reader.tell())\nprint(gzip.decompress(Path('input.gz').read_bytes()))\n", "input.gz wb 0\n4 4\nrb None b'alpha\\nbeta\\ntail' 456 15\nb'alpha\\nbeta\\ntail'\n", new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000", ["bad.gz"] = "696e76616c6964", ["truncated.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000", ["checksum.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402000000000000000000", ["index.txt"] = "31" }, new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000001f8b0808c801000002ff696e707574002b49cccc01005db4377c04000000" } };
        yield return new object[] { "gzipfile-filename-lazy-malformed-errors", "import gzip\nfor name in ['bad.gz','truncated.gz','checksum.gz']:\n    reader=gzip.GzipFile(name)\n    print(reader.mode,reader.mtime)\n    try: reader.read()\n    except Exception as error: print(type(error).__module__,type(error).__name__)\n    reader.close()\n", "rb None\ngzip BadGzipFile\nrb None\nbuiltins EOFError\nrb None\ngzip BadGzipFile\n", new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000", ["bad.gz"] = "696e76616c6964", ["truncated.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000", ["checksum.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402000000000000000000", ["index.txt"] = "31" }, new Dictionary<string,string> {  } };
        yield return new object[] { "gzipfile-filename-awaited-path-and-level", "import gzip\nfrom pathlib import Path\nclass FilePath:\n    def __fspath__(self):\n        with open('index.txt') as reader: print('path',reader.read())\n        return 'nested/result.gz'\nclass Level:\n    def __index__(self):\n        with open('index.txt') as reader: print('level',reader.read())\n        return 1\nlevel=Level()\nlevel.__index__=lambda:9\nwith gzip.GzipFile(FilePath(),mode='wb',compresslevel=level,mtime=0) as writer:\n    print(writer.name,writer.mode,writer.write(b'payload'))\ndata=Path('nested/result.gz').read_bytes()\nprint(data[:10].hex(),gzip.decompress(data))\n", "path 1\npath 1\nlevel 1\nnested/result.gz wb 7\n1f8b08080000000000ff b'payload'\n", new Dictionary<string,string> { ["input.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000", ["bad.gz"] = "696e76616c6964", ["truncated.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b0000", ["checksum.gz"] = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402000000000000000000", ["index.txt"] = "31" }, new Dictionary<string,string> { ["nested/result.gz"] = "1f8b08080000000000ff726573756c74002b48acccc94f4c0100156a2c4207000000" } };
    }

    [Theory]
    [MemberData(nameof(PythonFileControls))]
    public async Task OwnedFilesFollowFrozenPythonControls(string name, string source, string expected,
        Dictionary<string,string> seeds, Dictionary<string,string> finalFiles)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedBytes("/nested/.keep", []); delayed.SeedBytes("/nested/.keep", []);
        foreach (var (path,hex) in seeds)
        {
            if (path.EndsWith(".txt", StringComparison.Ordinal))
            {
                var text = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(hex));
                immediate.SeedFile("/" + path, text); delayed.SeedFile("/" + path, text);
            }
            else
            {
                immediate.SeedBytes("/" + path, Convert.FromHexString(hex));
                delayed.SeedBytes("/" + path, Convert.FromHexString(hex));
            }
        }
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.True(delayed.CompletedAsynchronously > 0);
        foreach (var (path,hex) in finalFiles)
        {
            Assert.Equal(Convert.FromHexString(hex), immediate.ReadBytes("/" + path));
            Assert.Equal(Convert.FromHexString(hex), delayed.ReadBytes("/" + path));
        }
    }
}
