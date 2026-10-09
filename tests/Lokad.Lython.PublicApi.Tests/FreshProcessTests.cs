using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lokad.Lython.Benchmarks.Comparison;
using Fixture = Lokad.Lython.PublicApi.Tests.ComparisonWorkerSupervisorTests.Fixture;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FreshProcessTests
{
    [Fact]
    public async Task LythonOnceInvokesTheHostClockExactlyOnceAndDoesNotHashBinaries()
    {
        using var catalog = new Catalog("import time\nprint(time.time())\n", "0.0\n");
        var host = new ClockHost();
        using var output = new MemoryStream();
        await LythonOnceWorker.RunAsync(catalog.Manifest, output, host: host);
        Assert.Equal(1, host.UtcCalls);
        output.Position = 0;
        using var frame = await ComparisonProtocol.ReadAsync(output);
        var root = frame!.RootElement;
        Assert.Equal("Equivalent", root.GetProperty("response").GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("response").GetProperty("completedInvocations").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("response").GetProperty("elapsedTicks").ValueKind);
        foreach (var library in root.GetProperty("identity").GetProperty("libraries").EnumerateArray())
            Assert.Equal(JsonValueKind.Null, library.GetProperty("sha256").ValueKind);
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Theory]
    [InlineData("print('λ😀')\n", "other\n", "Mismatch")]
    [InlineData("raise ValueError('bad')\n", "", "Failure")]
    [InlineData("return 42\n", "", "Mismatch")]
    [InlineData("async def work():\n    return 1\n", "", "Unsupported")]
    public async Task LythonOnceFailureCannotCarryACompletedJobOrWorkerTiming(string source, string expected, string status)
    {
        using var catalog = new Catalog(source, expected);
        using var output = new MemoryStream();
        await LythonOnceWorker.RunAsync(catalog.Manifest, output);
        output.Position = 0;
        using var frame = await ComparisonProtocol.ReadAsync(output);
        var response = frame!.RootElement.GetProperty("response");
        Assert.Equal(status, response.GetProperty("status").GetString());
        Assert.Equal(0, response.GetProperty("completedInvocations").GetInt32());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("elapsedTicks").ValueKind);
    }

    [Fact]
    public async Task AOnceWorkerRejectsAMultipleCasePayloadBeforeExecuting()
    {
        using var catalog = new Catalog(count: 2);
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => LythonOnceWorker.RunAsync(catalog.Manifest, output));
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("module")]
    [InlineData("gc")]
    [InlineData("hash")]
    [InlineData("benchmark-loader")]
    [InlineData("overrides")]
    public void AChangedRuntimeBuildOrStartupEnvelopeCannotBecomeAFreshSample(string change)
    {
        using var catalog = new Catalog();
        var prepared = JsonSerializer.SerializeToElement(LythonComparisonWorker.CreateIdentity(catalog.Manifest), ComparisonProtocol.JsonOptions);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(LythonComparisonWorker.CreateIdentity(catalog.Manifest, false), ComparisonProtocol.JsonOptions))!;
        if (change == "runtime") actual["runtimeVersion"] = "0.0.0";
        else if (change == "module") actual["libraries"]![0]!["moduleId"] = Guid.Empty.ToString();
        else if (change == "gc") actual["serverGc"] = !prepared.GetProperty("serverGc").GetBoolean();
        else if (change == "hash") actual["libraries"]![0]!["sha256"] = new string('a', 64);
        else if (change == "overrides") actual["runtimeOverrides"]!["DOTNET_PROCESSOR_COUNT"] = "1";
        else actual["benchmarkDotNetLoaded"] = true;
        var element = JsonSerializer.SerializeToElement(actual);
        Assert.Throws<InvalidDataException>(() => FreshProcessRunner.ValidateIdentity(element, prepared, catalog.Manifest, Environment.ProcessId));
    }

    [Fact]
    public async Task FreshParentAcceptsOneCorrectJobOnlyAfterCompleteDrainAndExit()
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture("once-normal", catalog);
        var result = await FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest, Prepared());
        Assert.Equal("Equivalent", result.Status);
        Assert.Equal(1, result.Response.CompletedInvocations);
        Assert.True(result.ElapsedTicks > 0);
        Assert.Equal(Stopwatch.Frequency, result.ClockFrequency);
        Assert.Null(result.Response.ElapsedTicks);
        await AssertExitedAsync(fixture.RootId);
    }

    [Theory]
    [InlineData("once-two-jobs")]
    [InlineData("once-worker-time")]
    [InlineData("once-wrong-case")]
    [InlineData("once-wrong-output")]
    [InlineData("once-two-frames")]
    [InlineData("once-nonzero")]
    public async Task FreshParentRejectsWrongCountTimingMetadataOutputAndShutdown(string mode)
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture(mode, catalog);
        await Assert.ThrowsAsync<InvalidDataException>(() => FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest, Prepared()));
        await AssertExitedAsync(fixture.RootId);
    }

    [Fact]
    public async Task FreshParentRejectsTruncatedTrailingBytes()
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture("once-truncated", catalog);
        await Assert.ThrowsAsync<EndOfStreamException>(() => FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest, Prepared()));
    }

    [Fact]
    public async Task FreshFailureHasNoParentTimingSample()
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture("once-failure", catalog);
        var result = await FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest, Prepared());
        Assert.Equal("Failure", result.Status);
        Assert.Null(result.ElapsedTicks);
        Assert.Null(result.Response.ElapsedTicks);
    }

    [Theory]
    [InlineData("once-hang")]
    [InlineData("once-orphan-pipes")]
    public async Task FreshWatchdogKillsItsScopeAndJoinsPumpsEvenAfterTheRootExits(string mode)
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture(mode, catalog);
        await Assert.ThrowsAsync<TimeoutException>(() => FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest,
            Prepared(), deadline: TimeSpan.FromSeconds(5)));
        await AssertExitedAsync(fixture.RootId);
        if (fixture.ChildId is { } child) await AssertExitedAsync(child);
    }

    [Theory]
    [InlineData("once-stderr-flood")]
    [InlineData("once-stdout-flood")]
    public async Task FreshStreamsRemainBoundedAndOverflowKillsTheWorker(string mode)
    {
        using var catalog = new Catalog();
        using var fixture = NewFixture(mode, catalog);
        await Assert.ThrowsAsync<IOException>(() => FreshProcessRunner.RunAsync(fixture.Launch, catalog.Manifest, Prepared()));
        await AssertExitedAsync(fixture.RootId);
    }

    private static Fixture NewFixture(string mode, Catalog catalog) => new(mode, catalog.Manifest.Sha256,
        new[] { catalog.Case.Id, catalog.Case.SourceSha256, catalog.Case.FixtureSha256, catalog.Case.ExpectedOutputSha256 });

    private static JsonElement Prepared() => JsonSerializer.SerializeToElement(new
    {
        protocolVersion = 1, status = "Ready", engine = "Fixture", catalogVersion = 1, clockFrequency = 1000,
        maximumFrameBytes = 4 * 1024 * 1024, maximumBatchIterations = 1_000_000, maximumBatchSeconds = 60,
    });

    private static async Task AssertExitedAsync(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException) { }
    }

    private sealed class Catalog : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "lython-once-" + Guid.NewGuid().ToString("N") + ".json");
        public ComparisonWorkload Case { get; }
        public ComparisonManifest Manifest { get; }
        public Catalog(string source = "print('λ😀')\n", string expected = "λ😀\n", int count = 1)
        {
            Case = new ComparisonWorkload("once", "control", "control", "test", 1, source, "{\"size\":1,\"text\":null}", expected);
            File.WriteAllText(_path, JsonSerializer.Serialize(new { schemaVersion = 1, catalogVersion = 1,
                cases = Enumerable.Range(0, count).Select(i => i == 0 ? Case : Case with { Id = "once" + i }).ToArray() },
                ComparisonProtocol.JsonOptions), ComparisonProtocol.Utf8);
            Manifest = ComparisonManifest.Load(_path);
        }
        public void Dispose() => File.Delete(_path);
    }

    private sealed class ClockHost : ILythonHost
    {
        public int UtcCalls;
        public string Cwd => "/";
        public DateTimeOffset UtcNow { get { UtcCalls++; return DateTimeOffset.UnixEpoch; } }
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        private static Exception Denied() => new LythonHostCapabilityUnavailableException("test host IO");
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token) => throw Denied();
        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken token) => throw Denied();
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken token) => throw Denied();
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => throw Denied();
        public ValueTask MkDirAsync(string path, CancellationToken token) => throw Denied();
        public ValueTask RemoveAsync(string path, CancellationToken token) => throw Denied();
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => throw Denied();
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => throw Denied();
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => throw Denied();
    }
}
