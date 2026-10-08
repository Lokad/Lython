using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonWorkerProtocolTests
{
    [Fact]
    public async Task ReverificationPreservesTheWarmedCompiledScript()
    {
        using var catalog = new TestCatalog("print(42)\n", "42\n");
        var worker = new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path));
        async Task Verify(int requestId)
        {
            using var input = new MemoryStream(); using var output = new MemoryStream();
            await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, requestId, "verify")); input.Position = 0;
            await worker.RunAsync(input, output);
        }
        await Verify(1);
        var compiled = (Dictionary<string, LythonCompiledScript>)typeof(LythonComparisonWorker)
            .GetField("_compiled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(worker)!;
        var warmed = compiled[catalog.Case.Id];
        await Verify(2);
        Assert.Same(warmed, compiled[catalog.Case.Id]);
    }

    [Theory]
    [InlineData("warm")]
    [InlineData("compile-run")]
    [InlineData("compile")]
    public async Task PersistentWorkerReusesCodeButResetsNamespaceAndChecksCompleteUnicodeOutput(string lane)
    {
        using var catalog = new TestCatalog("try:\n    count += 1\nexcept NameError:\n    count = 1\nprint(count, 'λ😀')\n", "1 λ😀\n");
        var manifest = ComparisonManifest.Load(catalog.Path);
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 2, "batch", lane, 3));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 3, "verify"));
        await ComparisonProtocol.WriteAsync(input, new { protocolVersion = 1, requestId = 4, operation = "quit" });
        input.Position = 0;
        using var output = new MemoryStream();
        await new LythonComparisonWorker(manifest).RunAsync(input, output);
        output.Position = 0;
        using var identity = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal(manifest.Sha256, identity!.RootElement.GetProperty("catalogSha256").GetString());
        Assert.Equal("Ready", identity.RootElement.GetProperty("status").GetString());
        using var first = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Equivalent", first!.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, first.RootElement.GetProperty("completedInvocations").GetInt32());
        using var batch = await ComparisonProtocol.ReadAsync(output);
        var result = batch!.RootElement;
        Assert.Equal("Completed", result.GetProperty("status").GetString());
        Assert.Equal(2, result.GetProperty("requestId").GetInt32());
        Assert.Equal(3, result.GetProperty("completedInvocations").GetInt32());
        Assert.True(result.GetProperty("elapsedTicks").GetInt64() > 0);
        Assert.True(result.GetProperty("clockFrequency").GetInt64() > 0);
        Assert.Equal(catalog.Case.SourceSha256, result.GetProperty("sourceSha256").GetString());
        Assert.Equal(catalog.Case.FixtureSha256, result.GetProperty("fixtureSha256").GetString());
        Assert.Equal(lane == "compile" ? null : catalog.Case.ExpectedOutputSha256,
            result.GetProperty("actualOutputSha256").GetString());
        using var after = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Equivalent", after!.RootElement.GetProperty("status").GetString());
        Assert.Equal(catalog.Case.ExpectedOutputSha256, after.RootElement.GetProperty("actualOutputSha256").GetString());
        using var closed = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Closed", closed!.RootElement.GetProperty("status").GetString());
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Fact]
    public async Task WrongOutputWithEqualLengthCannotBecomeATimedBatch()
    {
        using var catalog = new TestCatalog("print(24)\n", "42\n");
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 2, "batch", "warm", 1));
        input.Position = 0;
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path)).RunAsync(input, output));
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        using var failed = await ComparisonProtocol.ReadAsync(output);
        var result = failed!.RootElement;
        Assert.Equal("Mismatch", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("elapsedTicks").ValueKind);
        Assert.Equal(0, result.GetProperty("completedInvocations").GetInt32());
        Assert.Equal(ComparisonProtocol.Digest("24\n"), result.GetProperty("actualOutputSha256").GetString());
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Theory]
    [InlineData("sourceSha256")]
    [InlineData("fixtureSha256")]
    [InlineData("expectedOutputSha256")]
    public void ModifiedCatalogDigestIsRejectedBeforeCompilation(string field)
    {
        using var catalog = new TestCatalog("print('λ😀')\n", "λ😀\n");
        var json = JsonNode.Parse(File.ReadAllText(catalog.Path))!;
        json["cases"]![0]![field] = new string('0', 64);
        File.WriteAllText(catalog.Path, json.ToJsonString(), ComparisonProtocol.Utf8);
        Assert.Throws<InvalidDataException>(() => ComparisonManifest.Load(catalog.Path));
    }

    [Fact]
    public async Task WrongReceivedSourceDigestIsRejectedBeforeExecutingAJob()
    {
        using var catalog = new TestCatalog("print(42)\n", "42\n");
        var request = JsonSerializer.SerializeToNode(Request(catalog.Case, 1, "verify"), ComparisonProtocol.JsonOptions)!;
        request["sourceSha256"] = new string('0', 64);
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, request);
        input.Position = 0;
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path)).RunAsync(input, output));
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Ready", ready!.RootElement.GetProperty("status").GetString());
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Fact]
    public async Task UnsupportedCompilationReturnsNoTimingOrSuccessfulInvocationCount()
    {
        using var catalog = new TestCatalog("async def unsupported():\n    pass\n", "");
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        input.Position = 0;
        using var output = new MemoryStream();
        await new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path)).RunAsync(input, output);
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        using var rejected = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Unsupported", rejected!.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, rejected.RootElement.GetProperty("completedInvocations").GetInt32());
        Assert.Equal(JsonValueKind.Null, rejected.RootElement.GetProperty("elapsedTicks").ValueKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ComparisonProtocol.MaximumBatchIterations + 1)]
    public async Task InvalidBatchCountIsRejectedAfterVerification(int iterations)
    {
        using var catalog = new TestCatalog("print(42)\n", "42\n");
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 2, "batch", "warm", iterations));
        input.Position = 0;
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path)).RunAsync(input, output));
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        using var verified = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal("Equivalent", verified!.RootElement.GetProperty("status").GetString());
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Fact]
    public async Task ReplayedRequestIdIsRejectedInsteadOfDuplicatingWork()
    {
        using var catalog = new TestCatalog("print(42)\n", "42\n");
        using var input = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Case, 1, "verify"));
        input.Position = 0;
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => new LythonComparisonWorker(ComparisonManifest.Load(catalog.Path)).RunAsync(input, output));
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        using var verified = await ComparisonProtocol.ReadAsync(output);
        Assert.Equal(1, verified!.RootElement.GetProperty("requestId").GetInt32());
        Assert.Null(await ComparisonProtocol.ReadAsync(output));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ComparisonProtocol.MaximumFrameBytes + 1)]
    public async Task InvalidFrameLengthIsRejectedWithoutReadingItsBody(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        using var input = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ComparisonProtocol.ReadAsync(input));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TruncatedHeaderOrBodyIsDistinctFromCleanEndOfInput(bool header)
    {
        byte[] bytes = header ? [0, 0] : [0, 0, 0, 3, (byte)'{'];
        using var input = new MemoryStream(bytes);
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await ComparisonProtocol.ReadAsync(input));
        using var empty = new MemoryStream();
        Assert.Null(await ComparisonProtocol.ReadAsync(empty));
    }

    [Fact]
    public async Task InvalidUtf8FrameIsRejectedInsteadOfReplacingSourceCharacters()
    {
        using var input = new MemoryStream(new byte[] { 0, 0, 0, 1, 0xff });
        await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(async () => await ComparisonProtocol.ReadAsync(input));
    }

    [Theory]
    [InlineData("{\"requestId\":1,\"requestId\":2}")]
    [InlineData("{\"extra\":{\"value\":1,\"value\":2}}")]
    [InlineData("[]")]
    public async Task AmbiguousPropertiesAndNonObjectFramesAreRejected(string json)
    {
        var body = ComparisonProtocol.Utf8.GetBytes(json);
        var bytes = new byte[body.Length + 4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, body.Length);
        body.CopyTo(bytes, 4);
        using var input = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ComparisonProtocol.ReadAsync(input));
    }

    private static object Request(ComparisonWorkload workload, int requestId, string operation,
        string lane = "warm", int iterations = 1) => new
    {
        protocolVersion = ComparisonProtocol.Version, requestId, operation, caseId = workload.Id, lane, iterations,
        sourceSha256 = workload.SourceSha256, fixtureSha256 = workload.FixtureSha256,
        expectedOutputSha256 = workload.ExpectedOutputSha256,
    };

    private sealed class TestCatalog : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lython-catalog-" + Guid.NewGuid().ToString("N") + ".json");
        public ComparisonWorkload Case { get; }
        public TestCatalog(string source, string expected)
        {
            Case = new ComparisonWorkload("test.case", "test", "control", "control", 1, source, "{\"size\":1,\"text\":null}", expected);
            var json = JsonSerializer.Serialize(new { schemaVersion = 1, catalogVersion = 1, cases = new[] { Case } }, ComparisonProtocol.JsonOptions);
            File.WriteAllText(Path, json, ComparisonProtocol.Utf8);
        }
        public void Dispose() => File.Delete(Path);
    }
}
