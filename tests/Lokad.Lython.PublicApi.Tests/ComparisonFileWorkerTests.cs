using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonFileWorkerTests
{
    public static IEnumerable<object[]> Scripts => WorkloadCatalog.CreateRepresentative().Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Scripts))]
    public async Task CompleteScriptsVerifyEveryOutputFileAndReplayFreshState(string id)
    {
        var workload = WorkloadCatalog.CreateRepresentative().Single(c => c.Id == id);
        using var catalog = new Catalog(workload);
        var results = await Run(catalog, "warm");
        Assert.Equal(new[] { "Equivalent", "Completed", "Equivalent" }, results.Select(r => r.Status));
        foreach (var result in results)
        {
            Assert.Equal(workload.ExpectedOutputSha256, result.ActualOutputSha256);
            Assert.Equal(workload.ExpectedFilesSha256, result.ActualFilesSha256);
            Assert.Equal(2, result.CompletedInvocations);
        }
        Assert.True(results[1].ElapsedTicks > 0);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("compile-run")]
    [InlineData("warm-async")]
    public async Task SelectedLaneKeepsCompilationAndFileExecutionBoundaries(string lane)
    {
        using var catalog = new Catalog(WorkloadCatalog.CreateRepresentative().First(c => c.Size == 16));
        var result = (await Run(catalog, lane))[1];
        Assert.Equal("Completed", result.Status);
        Assert.Equal(lane == "compile" ? null : catalog.Workload.ExpectedFilesSha256, result.ActualFilesSha256);
        Assert.Equal(lane == "compile" ? null : catalog.Workload.ExpectedOutputSha256, result.ActualOutputSha256);
    }

    [Theory]
    [InlineData("with open('report', 'w') as f:\n    f.write('bad')\nprint('ok')\n")]
    [InlineData("print('ok')\n")]
    [InlineData("with open('report', 'w') as f:\n    f.write('yes')\nwith open('extra', 'w') as f:\n    f.write('x')\nprint('ok')\n")]
    public async Task EqualStdoutCannotHideWrongMissingOrExtraFile(string source)
    {
        using var catalog = new Catalog(Job(source, "ok\n", new(), new() { ["report"] = "yes" }));
        using var input = new MemoryStream(); using var output = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Workload, 1, "verify")); input.Position = 0;
        await new LythonComparisonWorker(catalog.Manifest, catalog.Directory).RunAsync(input, output);
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        using var failure = await ComparisonProtocol.ReadAsync(output);
        var response = failure!.RootElement.Deserialize<WorkerResponse>(ComparisonProtocol.JsonOptions)!;
        Assert.Equal("Mismatch", response.Status);
        Assert.Null(response.ElapsedTicks);
        Assert.Null(response.ActualFilesSha256);
        Assert.Equal(catalog.Workload.ExpectedOutputSha256, response.ActualOutputSha256);
    }

    [Fact]
    public async Task InputMutationsAndOutputsAreResetInsideEveryInvocation()
    {
        using var catalog = new Catalog(Job("with open('input') as f:\n    print(f.read())\nwith open('input', 'w') as f:\n    f.write('changed')\nwith open('report', 'a') as f:\n    f.write('once')\n",
            "original\n", new() { ["input"] = "original" }, new() { ["report"] = "once" }));
        Assert.All(await Run(catalog, "warm"), result => Assert.Equal(catalog.Workload.ExpectedFilesSha256, result.ActualFilesSha256));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("a\\b")]
    [InlineData("CON.txt")]
    [InlineData("a/../b")]
    [InlineData("a.")]
    public void NonportableOrEscapingMetadataIsRejectedBeforeExecution(string path)
    {
        Assert.Throws<InvalidDataException>(() => Job("pass", "", new() { [path] = "x" }, new()).Files);
    }

    [Fact]
    public void ForgedOutputFileDigestCannotEnterTheWorker()
    {
        using var catalog = new Catalog(WorkloadCatalog.CreateRepresentative()[0]);
        var json = JsonNode.Parse(File.ReadAllText(catalog.Path))!;
        json["cases"]![0]!["expectedFilesSha256"] = new string('0', 64);
        File.WriteAllText(catalog.Path, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ComparisonManifest.Load(catalog.Path));
    }

    [Fact]
    public async Task RealWorkerValidatesFilesAndSupervisorRemovesItsDirectory()
    {
        using var catalog = new Catalog(WorkloadCatalog.CreateRepresentative().First(c => c.Size == 16));
        var launch = Launch("actual-worker", catalog.Path);
        await using var worker = await ComparisonWorkerClient.StartAsync(launch, "Lython", catalog.Manifest.Sha256,
            startupDeadline: TimeSpan.FromSeconds(10), fileFixtures: true);
        var root = worker.Identity.GetProperty("fileFixtureDirectory").GetString()!;
        Assert.NotEqual(catalog.Directory, root);
        Assert.Equal("Equivalent", (await worker.VerifyAsync(catalog.Workload)).Status);
        Assert.Equal(catalog.Workload.ExpectedFilesSha256, (await worker.BatchAsync(catalog.Workload, "warm", 2)).ActualFilesSha256);
        Assert.Equal(catalog.Workload.ExpectedFilesSha256, (await worker.BatchAsync(catalog.Workload, "warm-async", 2)).ActualFilesSha256);
        var identity = worker.Identity.Clone();
        await worker.CloseAsync();
        Assert.True(worker.CleanupCompleted);
        Assert.False(System.IO.Directory.Exists(root));
        var once = await FreshProcessRunner.RunAsync(Launch("actual-once", catalog.Path), catalog.Manifest, identity,
            deadline: TimeSpan.FromSeconds(10));
        Assert.Equal("Equivalent", once.Status);
        Assert.Equal(catalog.Workload.ExpectedFilesSha256, once.Response.ActualFilesSha256);
        Assert.False(System.IO.Directory.Exists(once.Identity.GetProperty("fileFixtureDirectory").GetString()));
    }

    [Fact]
    public async Task KilledWorkerCannotLeaveItsOpenFileOrOwnedDirectoryBehind()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var process = await OwnedWorkerProcess.StartAsync(Launch("file-hold", "") with { FileFixtures = true }, deadline.Token);
        var root = process.FixtureDirectory!;
        using var reader = new StreamReader(process.Output);
        Assert.Equal(root, await reader.ReadLineAsync(deadline.Token));
        Assert.True(File.Exists(System.IO.Path.Combine(root, "held.bin")));
        await process.DisposeAsync();
        Assert.True(process.Exit.IsCompletedSuccessfully);
        Assert.False(System.IO.Directory.Exists(root));
    }

    private static async Task<List<WorkerResponse>> Run(Catalog catalog, string lane)
    {
        using var input = new MemoryStream(); using var output = new MemoryStream();
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Workload, 1, "verify"));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Workload, 2, "batch", lane));
        await ComparisonProtocol.WriteAsync(input, Request(catalog.Workload, 3, "verify"));
        input.Position = 0;
        await new LythonComparisonWorker(catalog.Manifest, catalog.Directory).RunAsync(input, output);
        output.Position = 0;
        using var ready = await ComparisonProtocol.ReadAsync(output);
        var results = new List<WorkerResponse>();
        for (var n = 0; n < 3; n++)
        {
            using var frame = await ComparisonProtocol.ReadAsync(output);
            results.Add(frame!.RootElement.Deserialize<WorkerResponse>(ComparisonProtocol.JsonOptions)!);
        }
        return results;
    }

    private static object Request(ComparisonWorkload workload, int id, string operation, string lane = "warm") => new
    {
        protocolVersion = 1, requestId = id, operation, caseId = workload.Id, lane, iterations = 2,
        workload.SourceSha256, workload.FixtureSha256, workload.ExpectedOutputSha256,
    };

    private static ComparisonWorkload Job(string source, string stdout, Dictionary<string, string> inputs, Dictionary<string, string> outputs)
        => new("test", "scripts", "whole-script", "test", 1, source,
            JsonSerializer.Serialize(new { size = 1, text = (string?)null,
                inputFiles = inputs.ToDictionary(p => p.Key, p => Convert.ToBase64String(ComparisonProtocol.Utf8.GetBytes(p.Value))),
                outputFiles = outputs.ToDictionary(p => p.Key, p => Convert.ToBase64String(ComparisonProtocol.Utf8.GetBytes(p.Value))) }), stdout);

    private static WorkerLaunch Launch(string mode, string catalog)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        var configuration = typeof(ComparisonFileWorkerTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var dll = System.IO.Path.Combine(root!.FullName, "tests", "Lokad.Lython.WorkerFixture", "bin", configuration, "net10.0", "Lokad.Lython.WorkerFixture.dll");
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(host)) host = Environment.GetEnvironmentVariable("PATH")!.Split(System.IO.Path.PathSeparator)
            .Select(path => System.IO.Path.Combine(path, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")).First(File.Exists);
        return new(System.IO.Path.GetFullPath(host), [dll, mode, catalog], root.FullName);
    }

    private sealed class Catalog : IDisposable
    {
        public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory(OwnedWorkerProcess.FixtureDirectoryPrefix).FullName;
        public string Path => System.IO.Path.Combine(Directory, "catalog.json");
        public ComparisonWorkload Workload { get; }
        public ComparisonManifest Manifest => ComparisonManifest.Load(Path);
        public Catalog(ComparisonWorkload workload)
        {
            Workload = workload;
            File.WriteAllText(Path, JsonSerializer.Serialize(new { schemaVersion = 1, catalogVersion = 1, cases = new[] { workload } }, ComparisonProtocol.JsonOptions));
        }
        public void Dispose()
        {
            OwnedWorkerProcess.ValidateFixtureDirectory(Directory);
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
