using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UrlQueryHostTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "mapping-items-property", "import urllib.parse as p\nfrom pathlib import Path\nclass Mapping:\n    @property\n    def items(self):\n        value=Path('/url').read_text()\n        return lambda:[('x',value+' a')]\nprint(p.urlencode(Mapping()))\n", "x=3+a\n" };
        yield return new object[] { "quote-callback", "import urllib.parse as p\nfrom pathlib import Path\ndef quote(value,*options):\n    Path('/url').read_text()\n    return p.quote_plus(value,*options)\nprint(p.urlencode([('x',['a b',b'z'])],doseq=True,quote_via=quote))\n", "x=a+b&x=z\n" };
        yield return new object[] { "string-protocol", "import urllib.parse as p\nfrom pathlib import Path\nclass Value:\n    def __str__(self):\n        return Path('/url').read_text()\nprint(p.urlencode([(Value(),Value())]))\n", "3=3\n" };
        yield return new object[] { "sequence-length-and-item", "import urllib.parse as p\nfrom pathlib import Path\nclass Query:\n    def __len__(self):\n        return int(Path('/url').read_text())\n    def __getitem__(self,index):\n        Path('/url').read_text()\n        if index>=2:\n            raise IndexError\n        return ('x',index)\nprint(p.urlencode(Query()))\n", "x=0&x=1\n" };
        yield return new object[] { "blank-value-truth", "import urllib.parse as p\nfrom pathlib import Path\nclass Flag:\n    def __bool__(self):\n        return int(Path('/url').read_text())>0\nprint(p.parse_qsl('a=&b&c=1',keep_blank_values=Flag()))\n", "[('a', ''), ('b', ''), ('c', '1')]\n" };
        yield return new object[] { "field-limit-comparison", "import urllib.parse as p\nfrom pathlib import Path\nclass Maximum:\n    def __lt__(self,count):\n        return int(Path('/url').read_text())<count\nprint(p.parse_qs('a=1&a=2&b=',keep_blank_values=True,max_num_fields=Maximum()))\n", "{'a': ['1', '2'], 'b': ['']}\n" };
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
