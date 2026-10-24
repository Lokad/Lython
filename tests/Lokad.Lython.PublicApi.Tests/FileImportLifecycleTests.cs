using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FileImportLifecycleTests
{
    // Independent CPython 3.13.2 goldens; no Python installation required at test time.
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FileImportLifecycle", "cases.json");
        foreach (var item in JsonSerializer.Deserialize<LifecycleCase[]>(File.ReadAllText(path))!)
            yield return [item.Name, item];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FilesAndImportsFollowPythonLifetimes(string name, LifecycleCase item)
    {
        Assert.Equal(name, item.Name);
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(item.Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var mock = new MockLythonHost();
                var delayed = new DelayedLythonHost();
                foreach (var file in item.Files)
                {
                    var bytes = Convert.FromBase64String(file.Value);
                    mock.SeedRawTextUtf8('/' + file.Key, bytes);
                    delayed.SeedBytes('/' + file.Key, bytes);
                }
                var options = new LythonRunOptions
                {
                    SourcePath = "/main.py",
                    AllowedLocalModules = item.Files.Keys.Select(x => '/' + x).ToHashSet(StringComparer.Ordinal),
                };
                using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options))
                {
                    Assert.True(result.Success == item.Success, result.Failure?.Message);
                    Assert.Equal(item.StandardOutput, result.StandardOutput);
                    Assert.Equal(item.ExceptionType, result.Failure?.ExceptionType);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("w")]
    [InlineData("wb")]
    [InlineData("a")]
    [InlineData("ab")]
    public async Task RefusedOpenDoesNotCreateOrTruncate(string mode)
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var exists in new[] { false, true })
        {
            using var script = new LythonEngine().Compile("import time\ntime.time()\nopen('out','" + mode + "')");
            var mock = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            if (exists) { mock.SeedFile("/out", "old"); delayed.SeedFile("/out", "old"); }
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 0, asynchronous ? delayed : mock);
            using (var result = asynchronous
                ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(exists, await (asynchronous ? (ILythonHost)delayed : mock).ExistsAsync("/out", default));
            if (exists) Assert.Equal("old", asynchronous ? delayed.ReadText("/out") : mock.ReadText("/out"));
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("r")]
    [InlineData("rb")]
    [InlineData("w")]
    [InlineData("wb")]
    [InlineData("a")]
    [InlineData("ab")]
    public async Task OpeningRejectsDirectoriesBeforeConsumption(string mode)
    {
        using var script = new LythonEngine().Compile("open('/directory','" + mode + "')");
        var mock = new MockLythonHost(); await mock.MkDirAsync("/directory", default);
        var delayed = new DelayedLythonHost(); await delayed.MkDirAsync("/directory", default);
        foreach (var result in new[] { script.Run(mock), await script.RunAsync(delayed) })
        {
            using (result)
            {
                Assert.False(result.Success);
                Assert.Equal("IsADirectoryError", result.Failure?.ExceptionType);
            }
        }
        Assert.Equal(0, mock.RangedReadCount);
    }

    public sealed record LifecycleCase(string Name, string Source, Dictionary<string, string> Files,
        bool Success, string StandardOutput, string? ExceptionType);
}
