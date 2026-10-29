using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StackGuardScenarioTests
{
    [Theory]
    [InlineData("exception-cause-cycle")]
    [InlineData("exception-context-cycle")]
    [InlineData("exception-cyclic-argument")]
    [InlineData("native-rglob")]
    [InlineData("native-glob")]
    [InlineData("dataclass-convert-asdict-cycle")]
    [InlineData("dataclass-convert-asdict-depth")]
    [InlineData("dataclass-convert-astuple-cycle")]
    [InlineData("dataclass-convert-astuple-depth")]
    [InlineData("dataclass-hash-cycle")]
    [InlineData("dataclass-hash-depth")]
    [InlineData("function")]
    [InlineData("function-finally")]
    [InlineData("setdefault-hash")]
    [InlineData("prepared-function-finally")]
    [InlineData("prepared-copyhook-finally")]
    [InlineData("comparison")]
    [InlineData("callback")]
    [InlineData("assigned-callback")]
    [InlineData("assigned-setdefault-hash")]
    [InlineData("copyhook")]
    [InlineData("copyhook-finally")]
    [InlineData("copyhook-helper")]
    [InlineData("copyhook-catch")]
    [InlineData("callback-finally")]
    [InlineData("length")]
    [InlineData("length-finally")]
    [InlineData("integer-finally")]
    [InlineData("integer-base-finally")]
    [InlineData("float-finally")]
    [InlineData("float-index-finally")]
    [InlineData("cancellation")]
    public void YieldedDeepWorkUnwindsAndRecoversInAnIsolatedProcess(string scenario)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var fixture = Path.Combine(root.FullName, "tests", "Lokad.Lython.WorkerFixture", "bin", configuration,
            SubprocessProbeRunner.TargetFramework, "Lokad.Lython.WorkerFixture.dll");
        Assert.True(File.Exists(fixture), "Matching fixture must be built as the suite's dependency.");
        var executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(executable)) executable = "dotnet";
        var result = SubprocessProbeRunner.Run(executable,
            [fixture, "stack-guard", typeof(LythonEngine).Assembly.Location, scenario], timeout: TimeSpan.FromSeconds(25));
        Assert.True(result.ExitCode == 0, "fixture exited with " + result.ExitCode + ": " + result.StandardError);
        Assert.Equal("", result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var value = document.RootElement;
        Assert.Equal(scenario, value.GetProperty("scenario").GetString());
        Assert.True(value.GetProperty("completedAsynchronously").GetInt32() > 0);
        if (scenario == "cancellation")
        {
            Assert.False(value.GetProperty("Success").GetBoolean());
            Assert.Equal("RuntimeError", value.GetProperty("error").GetString());
            Assert.Equal("execution canceled", value.GetProperty("message").GetString());
        }
        else Assert.True(value.GetProperty("Success").GetBoolean());
    }
}
