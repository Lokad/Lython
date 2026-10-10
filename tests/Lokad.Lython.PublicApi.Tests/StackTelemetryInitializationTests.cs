using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StackTelemetryInitializationTests
{
    [Fact]
    public void ConcurrentFirstUseDoesNotPermanentlyLoseStackTelemetry()
    {
        // These are the platforms whose resolver is exercised by this CI matrix.
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var fixture = Path.Combine(root.FullName, "tests", "Lokad.Lython.WorkerFixture", "bin", configuration,
            "net10.0", "Lokad.Lython.WorkerFixture.dll");
        Assert.True(File.Exists(fixture), "Matching fixture must be built as the suite's dependency.");
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(host)) host = "dotnet";
        var result = SubprocessProbeRunner.Run(host,
            [fixture, "stack-telemetry", typeof(LythonEngine).Assembly.Location], timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        foreach (var phase in new[] { "first", "second" })
        {
            var values = document.RootElement.GetProperty(phase).EnumerateArray().Select(x => x.GetInt64()).ToArray();
            Assert.Equal(32, values.Length);
            Assert.All(values, value => Assert.InRange(value, 96L * 1024, 20L * 1024 * 1024));
        }
        Assert.All(document.RootElement.GetProperty("errors").EnumerateArray(),
            value => Assert.Equal(JsonValueKind.Null, value.ValueKind));
    }
}
