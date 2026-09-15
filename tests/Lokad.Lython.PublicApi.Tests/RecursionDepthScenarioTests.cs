using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RecursionDepthScenarioTests
{
    [Fact]
    public void DeepCallRecursionRaisesInIsolatedProcess()
    {
        var probe = FindProbeDll();
        var snippets = new[]
        {
            "def loop():\n loop()\nloop()\n",
            "x: int\ndef f():\n a = 1\n b = 2\n c = 3\n d = 4\n return f()\nf()\n",
            "def f(n):\n f(n - 1)\n return 0\nf(100000)\n",
        };
        // Concurrent drains plus a deadline: a pathological snippet must surface as
        // a failure or a TimeoutException, never a wedged test host.
        var run = SubprocessProbeRunner.Run(
            "dotnet",
            [probe, "--batch-json"],
            JsonSerializer.Serialize(snippets));
        // Batch probes exit 0/1 for result mismatches; anything else is a crash.
        Assert.True(run.ExitCode is 0 or 1, "probe exited with " + run.ExitCode + ": " + run.StandardError);
        var lines = run.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(snippets.Length, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var lython = document.RootElement.GetProperty("Lython");
            Assert.False(lython.GetProperty("Success").GetBoolean());
            var failure = lython.GetProperty("Failure");
            Assert.Equal("RuntimeError", failure.GetProperty("ExceptionType").GetString());
            Assert.Contains("maximum interpreter stack depth exceeded", failure.GetProperty("Message").GetString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FundedRecursionDepthSucceeds()
    {
        var result = new LythonEngine().Run(
            "def f(n):\n return 7 if n <= 0 else f(n - 1)\nreturn f(100)\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7", result.ReturnValue?.ToString());
    }

    [Fact]
    public async Task FundedRecursionDepthSucceedsAsync()
    {
        var result = await new LythonEngine().RunAsync(
            "def f(n):\n return 7 if n <= 0 else f(n - 1)\nreturn f(100)\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7", result.ReturnValue?.ToString());
    }

    [Fact]
    public async Task FundedAccumulatingRecursionDepthSucceedsAsync()
    {
        var result = await new LythonEngine().RunAsync(
            "def f(n):\n return 0 if n <= 0 else 1 + f(n - 1)\nreturn f(100)\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("100", result.ReturnValue?.ToString());
    }

    [Fact]
    public async Task DeepCallRecursionRaisesAsync()
    {
        var result = await new LythonEngine().RunAsync(
            "def loop():\n loop()\nloop()\n",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum interpreter stack depth exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    private static string FindProbeDll() => SubprocessProbeRunner.FindProbeDll();
}
