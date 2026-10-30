using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PathBytesCompatibilityTests
{
    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ByteHelpersSuspendAndPublishThroughDelayedHost(string name, string source,
        string expected, string seedHex, string finalSampleHex, string? finalOutputHex)
    {
        _ = name;
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedBytes("/sample.bin", Convert.FromHexString(seedHex));
        host.SeedBytes("/empty.bin", []);
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Equal(Convert.FromHexString(finalSampleHex), host.ReadBytes("/sample.bin"));
        if (finalOutputHex is not null) Assert.Equal(Convert.FromHexString(finalOutputHex), host.ReadBytes("/output.bin"));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsBinaryAcquisitionOrPublication(bool write)
    {
        var host = new DelayedLythonHost();
        host.SeedBytes("/blob", [1, 2, 3]);
        var started = write ? host.PauseWriteUntilCancellation("/blob") : host.PauseReadUntilCancellation("/blob");
        var source = write ? "from pathlib import Path\nprint(Path('/blob').write_bytes(b'changed'))"
            : "from pathlib import Path\nprint(Path('/blob').read_bytes())";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
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
        if (write) Assert.Empty(host.ReadBytes("/blob"));
        else Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/blob"));
    }

    [Fact]
    public async Task KnownZeroWriteFailureDoesNotPublishPayload()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("from pathlib import Path\nprint(Path('/blob').write_bytes(b'changed'))");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/blob", [1, 2, 3]);
            host.FailNextFileWrite("/blob", "publication denied");
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Equal("OSError", result.Failure?.ExceptionType);
            Assert.Contains("publication denied", result.Failure?.Message);
            Assert.Empty(host.ReadBytes("/blob"));
        }
    }

    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "path-byte-read", "from pathlib import Path\ndata=Path('sample.bin').read_bytes()\nprint(repr(data),len(data),data[0],data[-1],type(data) is bytes)\n", "b'\\x00\\xffA\\r\\n' 5 0 10 True\n", "00ff410d0a", "00ff410d0a", null! };
        yield return new object[] { "path-byte-write", "from pathlib import Path\ntarget=Path('output.bin')\nprint(target.write_bytes(b'a'+bytes([255,10])))\nprint(repr(target.read_bytes()))\nprint(target.write_bytes(b''),repr(target.read_bytes()))\n", "3\nb'a\\xff\\n'\n0 b''\n", "00ff410d0a", "00ff410d0a", "" };
        yield return new object[] { "path-byte-keyword", "from pathlib import Path\ntarget=Path('output.bin')\nprint(target.write_bytes(data=b'keyword'))\nprint(repr(target.read_bytes()))\n", "7\nb'keyword'\n", "00ff410d0a", "00ff410d0a", "6b6579776f7264" };
        yield return new object[] { "path-byte-empty-read", "from pathlib import Path\ndata=Path('empty.bin').read_bytes()\nprint(repr(data),len(data),isinstance(data,bytes))\n", "b'' 0 True\n", "00ff410d0a", "00ff410d0a", null! };
        yield return new object[] { "path-byte-invalid-write", "from pathlib import Path\ntarget=Path('sample.bin')\nfor value in ['text',None,[],3]:\n    try: target.write_bytes(value)\n    except TypeError: print('type')\nprint(repr(target.read_bytes()))\n", "type\ntype\ntype\ntype\nb'\\x00\\xffA\\r\\n'\n", "00ff410d0a", "00ff410d0a", null! };
        yield return new object[] { "path-byte-bound-method", "from pathlib import Path\nread=getattr(Path('sample.bin'),'read_bytes')\nprint(repr(read()))\nwrite=getattr(Path('output.bin'),'write_bytes')\nprint(write(bytes([0,127,255])),repr(Path('output.bin').read_bytes()))\n", "b'\\x00\\xffA\\r\\n'\n3 b'\\x00\\x7f\\xff'\n", "00ff410d0a", "00ff410d0a", "007fff" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ByteHelpersFollowPythonAndPublishExactBytes(string name, string source,
        string expected, string seedHex, string finalSampleHex, string? finalOutputHex)
    {
        _ = name;
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/sample.bin", Convert.FromHexString(seedHex));
            host.SeedBytes("/empty.bin", []);
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            Assert.Equal(Convert.FromHexString(finalSampleHex), host.ReadBytes("/sample.bin"));
            if (finalOutputHex is not null)
                Assert.Equal(Convert.FromHexString(finalOutputHex), host.ReadBytes("/output.bin"));
        }
    }
}
