using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// I01: the probe exposes run options and accounted diagnostics through its
// options and reports, with documented exit codes. The CLI runs out of
// process through the built probe DLL like the other subprocess scenarios.
public sealed class LythonProbeCliTests
{
    private static SubprocessProbeRunner.RunResult RunProbe(params string[] arguments)
    {
        var probe = SubprocessProbeRunner.FindProbeDll();
        return SubprocessProbeRunner.Run("dotnet", [probe, .. arguments], timeout: TimeSpan.FromMinutes(2));
    }

    private static JsonElement RunProbeJson(params string[] arguments)
    {
        var run = RunProbe([.. arguments, "--json"]);
        return JsonDocument.Parse(run.StandardOutput).RootElement;
    }

    [Fact]
    public void HelpDocumentsOptionsAndExitCodes()
    {
        var run = RunProbe("--help");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("--max-memory-bytes", run.StandardOutput);
        Assert.Contains("--async", run.StandardOutput);
        Assert.Contains("Exit codes", run.StandardOutput);
    }

    [Fact]
    public void UnknownOptionExitsTwo()
    {
        var run = RunProbe("--bogus");
        Assert.Equal(2, run.ExitCode);
        Assert.NotEmpty(run.StandardError);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("0")]
    [InlineData("-5")]
    public void InvalidBudgetExitsTwo(string budget)
    {
        var run = RunProbe("-c", "print(1)", "--max-memory-bytes", budget);
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("max-memory-bytes", run.StandardError);
    }

    [Fact]
    public void BudgetDenialReportsDiagnostics()
    {
        var run = RunProbe("-c", "x = [0]*2000000", "--max-memory-bytes", "100000", "--json");
        Assert.Equal(1, run.ExitCode);
        var report = JsonDocument.Parse(run.StandardOutput).RootElement;
        Assert.False(report.GetProperty("Compared").GetBoolean());
        Assert.Equal(100000, report.GetProperty("Options").GetProperty("MaxMemoryBytes").GetInt64());
        Assert.False(report.GetProperty("Options").GetProperty("Async").GetBoolean());
        var lython = report.GetProperty("Lython");
        Assert.False(lython.GetProperty("Success").GetBoolean());
        Assert.Equal("MemoryError", lython.GetProperty("Failure").GetProperty("ExceptionType").GetString());
        Assert.True(lython.GetProperty("PeakExecutionMemoryBytes").GetInt64() > 0);
        Assert.True(lython.GetProperty("DeniedReservationBytes").GetInt64() > 0);
        Assert.Contains("line 1", lython.GetProperty("Failure").GetProperty("Span").GetString());
    }

    [Fact]
    public void AsyncFlagRunsSnippet()
    {
        var report = RunProbeJson("-c", "print(40 + 2)", "--async");
        Assert.True(report.GetProperty("Options").GetProperty("Async").GetBoolean());
        var lython = report.GetProperty("Lython");
        Assert.True(lython.GetProperty("Success").GetBoolean());
        Assert.Contains("42", lython.GetProperty("StandardOutput").GetString());
    }

    [Fact]
    public void SyncCounterpartSucceeds()
    {
        var report = RunProbeJson("-c", "print(40 + 2)");
        Assert.False(report.GetProperty("Options").GetProperty("Async").GetBoolean());
        var lython = report.GetProperty("Lython");
        Assert.True(lython.GetProperty("Success").GetBoolean());
        Assert.Contains("42", lython.GetProperty("StandardOutput").GetString());
    }
}
