using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StructuredUrlHostTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "fragment-truth", "import urllib.parse as p\nfrom pathlib import Path\nclass Flag:\n    def __bool__(self):\n        return int(Path('/url').read_text())>0\nprint(tuple(p.urlsplit('http://x/a?b#c',allow_fragments=Flag())))\n", "('http', 'x', '/a', 'b', 'c')\n" };
        yield return new object[] { "cache-key-hash", "import urllib.parse as p\nfrom pathlib import Path\nclass Flag:\n    def __hash__(self):\n        return int(Path('/url').read_text())\n    def __bool__(self):\n        return True\nprint(tuple(p.urlsplit('http://x/a?b#c',allow_fragments=Flag())))\n", "('http', 'x', '/a', 'b', 'c')\n" };
        yield return new object[] { "decode-descriptor", "import urllib.parse as p\nfrom pathlib import Path\nclass Input:\n    @property\n    def decode(self):\n        Path('/url').read_text()\n        def decode(*args):\n            Path('/url').read_text()\n            return 'http://X/a'\n        return decode\nprint(tuple(p.urlsplit(Input())))\n", "(b'http', b'X', b'/a', b'', b'')\n" };
        yield return new object[] { "reassembly-iteration-descriptor", "import urllib.parse as p\nfrom pathlib import Path\nclass Parts:\n    @property\n    def __iter__(self):\n        Path('/url').read_text()\n        def iterate():\n            Path('/url').read_text()\n            return iter(['http','X','a','',''])\n        return iterate\nprint(p.urlunsplit(Parts()))\n", "http://X/a\n" };
        yield return new object[] { "make-sequence-fallback", "import urllib.parse as p\nfrom pathlib import Path\nclass Parts:\n    def __getitem__(self,index):\n        Path('/url').read_text()\n        if index>=5:\n            raise IndexError\n        return ['http','X','a','',''][index]\nvalue=p.SplitResult._make(Parts())\nprint(tuple(value),value.geturl())\n", "('http', 'X', 'a', '', '') http://X/a\n" };
        yield return new object[] { "component-encoding-descriptor", "import urllib.parse as p\nfrom pathlib import Path\nclass Field:\n    @property\n    def encode(self):\n        Path('/url').read_text()\n        def encode(*args):\n            return Path('/url').read_text().encode('ascii')\n        return encode\nvalue=p.SplitResult('http','X',Field(),'','')\nprint(tuple(value.encode()))\n", "(b'http', b'X', b'3', b'', b'')\n" };
        yield return new object[] { "falsey-reassembly-protocol", "import urllib.parse as p\nfrom pathlib import Path\nclass Empty:\n    def __bool__(self):\n        Path('/url').read_text()\n        return False\nprint(p.urlunsplit(('http',Empty(),'/a','','')))\n", "http:///a\n" };
        yield return new object[] { "cache-collision-equality", "import urllib.parse as p\nfrom pathlib import Path\nclass Flag:\n    def __hash__(self): return 17\n    def __bool__(self): return True\n    def __eq__(self,other): return int(Path('/url').read_text())>0\nleft=p.urlsplit('http://x/await-equality',allow_fragments=Flag())\nprint(left is p.urlsplit('http://x/await-equality',allow_fragments=Flag()))\n", "True\n" };
        yield return new object[] { "cache-equality-result-truth", "import urllib.parse as p\nfrom pathlib import Path\nclass Truth:\n    def __bool__(self): return int(Path('/url').read_text())>0\nclass Flag:\n    def __hash__(self): return 17\n    def __bool__(self): return True\n    def __eq__(self,other): return Truth()\nleft=p.urlsplit('http://x/await-truth',allow_fragments=Flag())\nprint(left is p.urlsplit('http://x/await-truth',allow_fragments=Flag()))\n", "True\n" };
        yield return new object[] { "cache-nested-key-hash-descriptor", "import urllib.parse as p\nfrom pathlib import Path\nclass Flag:\n    @property\n    def __hash__(self):\n        Path('/url').read_text()\n        def hash(): return int(Path('/url').read_text())\n        return hash\nflag=(Flag(),)\nleft=p.urlsplit('http://x/await-nested-hash',allow_fragments=flag)\nprint(left is p.urlsplit('http://x/await-nested-hash',allow_fragments=flag))\n", "True\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task GuestProtocolsAwaitMediatedEffects(string name, string source, string expected)
    {
        _ = name;
        var script = Compile(source);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/url", "3");
        var host = new DelayedLythonHost();
        host.SeedFile("/url", "3");
        var sync = script.Run(syncHost);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);
        Assert.Equal(expected, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task CancellationStopsInsideGuestProtocols(string name, string source, string expected)
    {
        _ = name;
        _ = expected;
        var host = new DelayedLythonHost();
        host.SeedFile("/url", "3");
        var started = host.PauseReadUntilCancellation("/url");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile(source).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("", result.StandardOutput);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
