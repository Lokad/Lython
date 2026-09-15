using System.Reflection;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// T01: the consolidated runner distinguishes launch failure, timeout, crash
// and success, drains both streams under one deadline, and discovers the
// probe built with the matching test configuration.
public sealed class SubprocessRunnerTests
{
    [Fact]
    public void SuccessfulRunCapturesBothStreams()
    {
        var run = SubprocessProbeRunner.Run("dotnet", ["--version"]);
        Assert.Equal(0, run.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(run.StandardOutput));
    }

    [Fact]
    public void MissingExecutableThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SubprocessProbeRunner.Run("definitely-not-an-executable-xyz", []));
    }

    [Fact]
    public void MalformedBatchInputExitsTwoWithGuidance()
    {
        var probe = SubprocessProbeRunner.FindProbeDll();
        var run = SubprocessProbeRunner.Run(
            "dotnet",
            [probe, "--batch-json"],
            "this is not a JSON array");
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("probe input", run.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HangingChildHitsDeadlineAndThrowsTimeout()
    {
        var (executable, arguments) = OperatingSystem.IsWindows()
            ? ("ping", new[] { "-n", "30", "127.0.0.1" })
            : ("sleep", new[] { "30" });
        Assert.Throws<TimeoutException>(() =>
            SubprocessProbeRunner.Run(executable, arguments, timeout: TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ProbeDiscoveryPrefersMatchingConfiguration()
    {
        var probe = SubprocessProbeRunner.FindProbeDll();
        Assert.True(File.Exists(probe), probe);
        var matching = typeof(SubprocessProbeRunner).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        if (matching is not null)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var matchingCandidate = Path.Combine(
                    directory.FullName, "tools", "LythonProbe", "bin", matching, "net10.0", "LythonProbe.dll");
                if (File.Exists(matchingCandidate))
                {
                    Assert.Equal(matchingCandidate, probe);
                    return;
                }

                directory = directory.Parent;
            }
        }
    }

    [Fact]
    public void HeavyStreamsOnBothPipesDoNotDeadlock()
    {
        // 300 KB per pipe exceeds OS pipe buffers many times over: sequential
        // drains would wedge with the child blocked on the unread pipe.
        var python = ResolvePython();
        var run = SubprocessProbeRunner.Run(
            python,
            ["-I", "-X", "utf8", "-c", "import sys; sys.stdout.write('o'*300000); sys.stderr.write('e'*300000)"]);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(300000, run.StandardOutput.Length);
        Assert.Equal(300000, run.StandardError.Length);
    }

    private static string ResolvePython()
    {
        var configured = Environment.GetEnvironmentVariable("LYTHON_DIFFTEST_PYTHON");
        foreach (var candidate in configured is null ? new[] { "python", "python3" } : new[] { configured })
        {
            try
            {
                var run = SubprocessProbeRunner.Run(candidate, ["--version"], timeout: TimeSpan.FromSeconds(15));
                if (run.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Try the next candidate.
            }
        }

        throw new InvalidOperationException("A CPython executable is required (tried 'python', 'python3'); set LYTHON_DIFFTEST_PYTHON to its path.");
    }
}
