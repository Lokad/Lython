using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AsciiCodecHostTests
{
    private const string Source = """
        from pathlib import Path
        print(repr(Path('/input').read_text(encoding='ascii',errors='replace')))
        for errors in ['ignore','replace','backslashreplace']:
            with open('/input',encoding='US-ASCII',errors=errors,newline='') as source:
                print(repr(source.read()))
        print(Path('/path').write_text('é😀\n',encoding='ascii',errors='replace',newline='\r\n'))
        with open('/encoded','w',encoding='ascii',errors='backslashreplace',newline='\r\n') as target:
            print(target.write('é😀\n'),target.tell())
            target.flush()
            print(target.tell())
        with open('/strict','w',encoding='ascii') as target:
            target.write('A')
            try:
                target.write('prefixé')
            except UnicodeEncodeError:
                print('denied')
            target.write('B')
        """;

    [Fact]
    public async Task PathsAndHandlesDecodeAndEncodeThroughByteCapabilities()
    {
        var script = Compile(Source);
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/input", [0x61, 0x0d, 0x0a, 0x62, 0x80, 0xc3, 0xa9, 0x0d]);
        var sync = script.Run(syncHost);
        var host = new DelayedLythonHost();
        host.SeedBytes("/input", [0x61, 0x0d, 0x0a, 0x62, 0x80, 0xc3, 0xa9, 0x0d]);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("'a\\nb���\\n'\n'a\\r\\nb\\r'\n'a\\r\\nb���\\r'\n'a\\r\\nb\\\\x80\\\\xc3\\\\xa9\\r'\n3\n3 16\n16\ndenied\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal(new byte[] { 0x3f, 0x3f, 0x0d, 0x0a }, syncHost.ReadBytes("/path"));
        Assert.Equal(syncHost.ReadBytes("/path"), host.ReadBytes("/path"));
        Assert.Equal("\\xe9\\U0001f600\r\n", System.Text.Encoding.ASCII.GetString(syncHost.ReadBytes("/encoded")));
        Assert.Equal(syncHost.ReadBytes("/encoded"), host.ReadBytes("/encoded"));
        Assert.Equal("AB", System.Text.Encoding.ASCII.GetString(host.ReadBytes("/strict")));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task StrictPathEncodingFailurePreservesExistingBytes()
    {
        var script = Compile("from pathlib import Path\nPath('/output').write_text('é',encoding='ascii')");
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/output", [0x41]);
        var host = new DelayedLythonHost();
        host.SeedBytes("/output", [0x41]);
        foreach (var result in new[] { script.Run(syncHost), await script.RunAsync(host) })
        {
            Assert.False(result.Success);
            Assert.Equal("UnicodeEncodeError", result.Failure?.ExceptionType);
        }
        Assert.Equal(new byte[] { 0x41 }, syncHost.ReadBytes("/output"));
        Assert.Equal(syncHost.ReadBytes("/output"), host.ReadBytes("/output"));
    }

    [Fact]
    public async Task GzipTextHandlesReuseAsciiCodecAndByteTransport()
    {
        const string source = """
            import gzip
            with gzip.open('/input', 'rt', encoding='ascii', errors='replace', newline='') as reader:
                print(repr(reader.read()))
            with gzip.open('/output', 'wt', encoding='ascii', errors='backslashreplace', newline='\r\n') as writer:
                print(writer.write('é😀\n'))
            with gzip.open('/output', 'rb') as reader:
                print(repr(reader.read()))
            """;
        using var compressed = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(new byte[] { 0x41, 0xff, 0x0d, 0x0a });
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/input", compressed.ToArray());
        var host = new DelayedLythonHost();
        host.SeedBytes("/input", compressed.ToArray());
        var script = Compile(source);
        var sync = script.Run(syncHost);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("'A�\\r\\n'\n3\nb'\\\\xe9\\\\U0001f600\\r\\n'\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task ReadLimitsRejectBeforeDecodingHostBytes()
    {
        var script = Compile("from pathlib import Path\nPath('/input').read_text(encoding='ascii')");
        var options = new LythonRunOptions { MaxHostReadBytes = 1 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/input", [0xff, 0xff]);
        var host = new DelayedLythonHost();
        host.SeedBytes("/input", [0xff, 0xff]);
        foreach (var result in new[] { script.Run(syncHost, options), await script.RunAsync(host, options) })
        {
            Assert.False(result.Success);
            Assert.Contains("maximum bytes (1)", result.Failure?.Message);
        }
    }

    [Fact]
    public void StaticTextContractsAcceptAsciiAliases()
        => Compile("from pathlib import Path\nimport argparse,gzip\nopen('/input',encoding='ascii')\nPath('/input').read_text(encoding='US-ASCII')\nPath('/output').write_text('x',encoding='646')\nPath('/input').open(encoding='cp367')\nargparse.FileType('r',encoding='ascii')\ngzip.open('/input','rt',encoding='ascii')");

    [Theory]
    [InlineData("from pathlib import Path\nprint('before')\nPath('/input').read_text(encoding='ascii')\nprint('after')")]
    [InlineData("print('before')\nwith open('/input',encoding='ascii') as source:\n    source.read()\nprint('after')")]
    public async Task CancellationStopsDuringByteAcquisition(string source)
    {
        var host = new DelayedLythonHost();
        host.SeedBytes("/input", [0x41]);
        var started = host.PauseReadUntilCancellation("/input");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile(source).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("before\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("replace", true)]
    [InlineData("backslashreplace", false)]
    public async Task DecodeLimitsCountOutputScalars(string errors, bool success)
    {
        // Both argument strings fit this cap. Replacement emits twenty scalars
        // in sixty UTF-8 bytes; backslash replacement emits eighty scalars.
        var script = Compile("print((b'\\xff'*20).decode('ascii','" + errors + "'))");
        var options = new LythonRunOptions { MaxStringLength = 20, MaxCollectionSize = 20 };
        var expected = success ? new string('�', 20) + "\n" : "";
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success == success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            if (!success) Assert.Contains("maximum string length", result.Failure?.Message);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
