using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileCancellationTests
{
    private const string MemberHex = "1f8b08007b00000002ff4bcc29c848e44a4a2d49e402006e50306e0b000000";
    [Theory]
    [InlineData("read")]
    [InlineData("index")]
    [InlineData("write")]
    public async Task PausedGuestReadsAndOwnedPublicationCancelWithoutReplacingPriorBytes(string operation)
    {
        var source = operation switch
        {
            "read" => "import gzip,io\nclass Source:\n    def __init__(self): self.inner=io.BytesIO(bytes.fromhex('" + MemberHex + "'))\n    def read(self,size):\n        with open('pause.txt') as file: file.read()\n        return self.inner.read(size)\nreader=gzip.GzipFile(fileobj=Source())\nprint('ready')\nprint(reader.read(1))\nreader.close()\n",
            "index" => "import gzip\nclass Level:\n    def __index__(self):\n        with open('pause.txt') as file: file.read()\n        return 1\nprint('ready')\nwith gzip.GzipFile('output.gz',mode='wb',compresslevel=Level(),mtime=0) as writer: writer.write(b'payload')\n",
            _ => "import gzip\nwith gzip.GzipFile('output.gz',mode='wb',mtime=0) as writer:\n    writer.write(b'payload')\n    print('ready')\n",
        };
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/pause.txt", "1");
        var prior = Convert.FromHexString(MemberHex);
        host.SeedBytes("/output.gz", prior);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = operation == "write" ? host.PauseWriteUntilCancellation("/output.gz")
            : host.PauseReadUntilCancellation("/pause.txt");
        var pending = script.RunAsync(host, new LythonRunOptions
        { CancellationToken = cancellation.Token, MaxExecutionMemoryBytes = 8 * 1024 * 1024 });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message);
        Assert.Equal("ready\n", result.StandardOutput);
        Assert.Equal(prior, host.ReadBytes("/output.gz"));
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/pause.txt", "1"); fresh.SeedBytes("/output.gz", prior);
        var retry = await script.RunAsync(fresh);
        Assert.True(retry.Success, retry.Failure?.Message);
        Assert.Equal(operation == "read" ? "ready\nb'a'\n" : "ready\n", retry.StandardOutput);
        if (operation != "read")
        {
            var verify = new LythonEngine().Compile("import gzip\nwith gzip.GzipFile('output.gz') as reader: print(reader.read())\n");
            var decoded = await verify.RunAsync(fresh);
            Assert.True(decoded.Success, decoded.Failure?.Message);
            Assert.Equal("b'payload'\n", decoded.StandardOutput);
        }
    }
}
