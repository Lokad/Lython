using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonTextStreamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardStreamsShareJsonOptionsAndCapturedOutput(bool asynchronous)
    {
        var script = Compile("""
            import json
            import sys
            data = json.load(sys.stdin, parse_int=lambda s: int(s) + 1)
            json.dump(data, sys.stdout, ensure_ascii=False, sort_keys=True)
            json.dump({'error': 'é'}, sys.stderr, ensure_ascii=False)
            """);
        var host = new MockLythonHost();
        host.SeedStandardInput("{\"value\": 2, \"name\": \"😀\"}");
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"name\": \"😀\", \"value\": 3}", result.StandardOutput);
        Assert.Equal("{\"error\": \"é\"}", result.StandardError);
        Assert.Equal(result.StandardOutput, host.CapturedStandardOutput());
        Assert.Equal(result.StandardError, host.CapturedStandardError());
    }

    [Fact]
    public async Task GuestReadersRunBeforeDecoderConstructionAndHooks()
    {
        await AssertBothModes("""
            import json
            events = []
            class Reader:
                def read(self):
                    events.append('read')
                    return '{"a": 2}'
            class Decoder(json.JSONDecoder):
                def __init__(self, label, **kw):
                    events.append(label)
                    super().__init__(**kw)
                def decode(self, text):
                    events.append('decode')
                    return super().decode(text)
            def number(text):
                events.append(text)
                return int(text) + 1
            print(json.load(Reader(), cls=Decoder, label='init', parse_int=number))
            print(events)
            """, "{'a': 3}\n['read', 'init', 'decode', '2']\n");
    }

    [Fact]
    public async Task DumpDispatchesIterencodeAndResolvesWriteForEveryChunk()
    {
        await AssertBothModes("""
            import json
            events = []
            class Encoder(json.JSONEncoder):
                def __init__(self, label, **kw):
                    events.append(label)
                    super().__init__(**kw)
                def encode(self, value):
                    raise AssertionError('dump must use iterencode')
                def iterencode(self, value):
                    for chunk in ['[', '3', ']']:
                        events.append('yield:' + chunk)
                        yield chunk
            class Writer:
                def write(self, chunk):
                    events.append('old:' + chunk)
                    self.write = self.updated
                    return None
                def updated(self, chunk):
                    events.append('new:' + chunk)
                    return 100
            print(json.dump(3, Writer(), cls=Encoder, label='init'))
            print(events)
            """, "None\n['init', 'yield:[', 'old:[', 'yield:3', 'new:3', 'yield:]', 'new:]']\n");
    }

    [Fact]
    public async Task EmptyCustomEncoderDoesNotAccessTheWriter()
    {
        await AssertBothModes("""
            import json
            class Encoder(json.JSONEncoder):
                def iterencode(self, value):
                    return []
            class Missing:
                pass
            print(json.dump(1, Missing(), cls=Encoder))
            """, "None\n");
    }

    [Fact]
    public async Task CustomChunksAndWriteReturnValuesBelongToTheWriter()
    {
        await AssertBothModes("""
            import json
            class Encoder(json.JSONEncoder):
                def iterencode(self, value):
                    return [None, 7]
            class Writer:
                def write(self, value):
                    print(value)
                    return object()
            print(json.dump(1, Writer(), cls=Encoder))
            """, "None\n7\nNone\n");
    }

    [Theory]
    [InlineData("7")]
    [InlineData("None")]
    [InlineData("b'{}'")]
    [InlineData("[]")]
    public async Task ReaderMustReturnText(string value)
    {
        await AssertBothModes("import json\nclass Reader:\n    def read(self):\n        return " + value +
            "\ntry:\n    json.load(Reader())\nexcept TypeError:\n    print('bad read')", "bad read\n");
    }

    [Theory]
    [InlineData("load", "pass", "AttributeError")]
    [InlineData("load", "read = None", "TypeError")]
    [InlineData("load", "def read(self):\n        raise ValueError('reader')", "ValueError")]
    [InlineData("dump", "pass", "AttributeError")]
    [InlineData("dump", "write = 1", "TypeError")]
    [InlineData("dump", "def write(self, text):\n        raise ValueError('writer')", "ValueError")]
    public async Task ProtocolLookupAndCallbackFailuresRemainCatchable(string operation, string body, string error)
    {
        await AssertBothModes("import json\nclass Stream:\n    " + body + "\ntry:\n    json." + operation +
            (operation == "load" ? "(Stream())" : "([1], Stream())") +
            "\nexcept " + error + ":\n    print('caught')", "caught\n");
    }

    [Fact]
    public async Task WriterFailureStopsPullsAndPreservesPublishedPrefix()
    {
        await AssertBothModes("""
            import json
            events = []
            class Encoder(json.JSONEncoder):
                def iterencode(self, value):
                    for chunk in ['[', '3', ']']:
                        events.append(chunk)
                        yield chunk
            class Writer:
                def write(self, chunk):
                    if chunk == '3':
                        raise ValueError('stop')
                    events.append('write:' + chunk)
            try:
                json.dump(3, Writer(), cls=Encoder)
            except ValueError:
                print(events)
            """, "['[', 'write:[', '3']\n");
    }

    [Fact]
    public async Task DefaultFailureOccursAfterTheOpeningPrefix()
    {
        await AssertBothModes("""
            import json
            chunks = []
            class Writer:
                def write(self, text):
                    chunks.append(text)
            def fail(value):
                print('prefix', ''.join(chunks))
                raise ValueError('stop')
            try:
                json.dump([object()], Writer(), default=fail)
            except ValueError:
                print('caught', ''.join(chunks))
            """, "prefix [\ncaught [\n");
    }

    [Fact]
    public async Task GuestAttributeLookupAndMethodsAwaitDelayedHostEffects()
    {
        const string source = """
            import json
            class Reader:
                @property
                def read(self):
                    with open('/marker') as marker:
                        marker.read()
                    return self.consume
                def consume(self):
                    with open('/input') as handle:
                        return handle.read()
            class Writer:
                def __getattr__(self, name):
                    with open('/marker') as marker:
                        marker.read()
                    if name == 'write':
                        return self.publish
                    raise AttributeError(name)
                def publish(self, text):
                    with open('/out', 'a') as handle:
                        handle.write(text)
            json.dump(json.load(Reader()), Writer(), ensure_ascii=False)
            """;
        var script = Compile(source);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/marker", "ok");
        syncHost.SeedFile("/input", "{\"name\": \"é\"}");
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("{\"name\": \"é\"}", syncHost.ReadText("/out"));
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/marker", "ok");
        delayedHost.SeedFile("/input", "{\"name\": \"é\"}");
        var asynchronous = await script.RunAsync(delayedHost);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(syncHost.ReadText("/out"), delayedHost.ReadText("/out"));
        Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("read", false)]
    [InlineData("stdout", false)]
    [InlineData("stderr", false)]
    [InlineData("read", true)]
    [InlineData("stdout", true)]
    [InlineData("stderr", true)]
    public async Task StandardStreamsAwaitSuspensionAndInFlightCancellation(string operation, bool cancel)
    {
        var source = "import json\nimport sys\n" + (operation == "read"
            ? "print(json.load(sys.stdin)['a'])" : "json.dump({'a': 1}, sys." + operation + ")");
        var script = Compile(source);
        var stream = new PausedStream();
        var host = new StreamHost(stream, operation);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await stream.Started.Task.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        if (cancel) cancellation.Cancel();
        else stream.Release.TrySetResult();
        var result = await pending;
        if (cancel)
        {
            Assert.False(result.Success);
            Assert.Contains("canceled", result.Failure?.Message);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(operation == "read" ? "1\n" : "{\"a\": 1}",
                operation == "stderr" ? result.StandardError : result.StandardOutput);
            if (operation != "read") Assert.Equal("{\"a\": 1}", Encoding.UTF8.GetString(stream.Bytes.ToArray()));
        }
    }

    [Fact]
    public async Task StandardInputAndOutputCapsRemainAuthoritative()
    {
        var read = Compile("import json\nimport sys\njson.load(sys.stdin)");
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedStandardInput("[12345]");
            var options = new LythonRunOptions { MaxHostReadBytes = 3 };
            var result = asynchronous ? await read.RunAsync(host, options) : read.Run(host, options);
            Assert.False(result.Success);
            Assert.Contains("maximum bytes", result.Failure?.Message);
            var output = Compile("import json\nimport sys\njson.dump('long text', sys.stdout)");
            options = new LythonRunOptions { MaxStandardOutputBytes = 3 };
            result = asynchronous ? await output.RunAsync(new MockLythonHost(), options) : output.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }

    [Fact]
    public async Task DiscardingWriterAvoidsMaterializingTheCompleteDocument()
    {
        var script = Compile("""
            import json
            class Writer:
                def __init__(self):
                    self.count = 0
                def write(self, text):
                    self.count += len(text)
            values = ['x' * 10000] * 20
            try:
                json.dumps(values)
                print('eager unexpectedly fit')
            except MemoryError:
                print('eager denied')
            writer = Writer()
            json.dump(values, writer)
            print(writer.count)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("eager denied\n200080\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task FailedDumpsReclaimAbandonedEncoderOwnership()
    {
        var script = Compile("""
            import json
            class Writer:
                def write(self, text):
                    raise ValueError('stop')
            writer = Writer()
            for i in range(3000):
                try:
                    json.dump({'a': list(range(50))}, writer, sort_keys=True)
                except ValueError:
                    pass
            print('done')
            """);
        // Match the existing abandoned-iterencode gate's 1 MiB cap: pool
        // bookkeeping remains accounted even after dropped values reclaim.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("done\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("json.load(1)")]
    [InlineData("json.dump([], None)")]
    public void DefiniteUnsupportedStreamShapesRetainStaticDiagnostics(string expression)
    {
        var script = new LythonEngine().Compile("import json\n" + expression);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3158");
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = Compile(source);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    private sealed class PausedStream : ILythonTextInput, ILythonTextOutput, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => false;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte> Bytes { get; } = new();
        private async Task Pause(CancellationToken token)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
        }
        public async ValueTask<ReadOnlyMemory<byte>> ReadToEndUtf8Async(CancellationToken token)
        {
            await Pause(token);
            return "{\"a\": 1}"u8.ToArray();
        }
        public ValueTask<ReadOnlyMemory<byte>?> ReadLineUtf8Async(CancellationToken token) => throw new NotSupportedException();
        public async ValueTask WriteUtf8Async(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await Pause(token);
            Bytes.AddRange(bytes.ToArray());
        }
        public ValueTask FlushAsync(CancellationToken token) => ValueTask.CompletedTask;
    }

    private sealed class StreamHost(PausedStream stream, string operation) : ILythonHost, ILythonSynchronousHostCapability
    {
        public string Cwd => "/";
        public bool CompletesSynchronously => true;
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public ILythonTextInput? StandardInput => operation == "read" ? stream : null;
        public ILythonTextOutput? StandardOutput => operation == "stdout" ? stream : null;
        public ILythonTextOutput? StandardError => operation == "stderr" ? stream : null;
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => throw new NotSupportedException();
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask MkDirAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => throw new NotSupportedException();
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
    }
}
