using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class AgentCompatibilityBaselineTests
{
    [Fact]
    public void Sum_CoversCommonPythonScratchScriptShapes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []
values.append(str(sum([])))
values.append(str(sum([1, 2, 3])))
values.append(str(sum((1, 2), 10)))
values.append(str(sum(x for x in range(4))))
values.append(str(sum([1.5, 2])))
write_text("/out.txt", "|".join(values))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("0|6|13|6|3.5", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("sum([\"a\"])\n")]
    [InlineData("sum([b\"a\"])\n")]
    public void Sum_RejectsStringAndBytesOperands(string source)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("string or bytes operands", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sum(1)\n")]
    [InlineData("sum(None)\n")]
    [InlineData("sum(True)\n")]
    public void Sum_StaticDiagnosticsCatchProvablyNonIterableArgumentShapes(string source)
    {
        var result = new LythonEngine().Run(
            source + "write_text(\"/out.txt\", \"side effect\")\n",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3031" && d.Message.Contains("iterable", StringComparison.Ordinal));
    }

    [Fact]
    public void SubprocessCheckOutput_ReturnsStdoutAndForcesHostStdoutPipe()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool", "--version"], 0, "lython\n", "ignored");

        var result = new LythonEngine().Run(
            """
import subprocess
out = subprocess.check_output(["tool", "--version"])
write_text("/out.txt", out)
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("lython\n", host.ReadText("/out.txt"));
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.Equal(["tool", "--version"], host.LastSubprocessRequest!.Args);
        Assert.Equal(LythonSubprocessStreamMode.Pipe, host.LastSubprocessRequest.StandardOutput);
    }

    [Fact]
    public void SubprocessCheckOutput_ReportsHostRequirementBeforeExecutionWhenRunnerIsUnavailable()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import subprocess
subprocess.check_output(["tool"])
write_text("/out.txt", "side effect")
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041" && d.Message.Contains("subprocess", StringComparison.Ordinal));
        Assert.False(host.Exists("/out.txt"));
    }

    [Fact]
    public void PathReadText_AcceptsCommonEncodingAndErrorsForms()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/input.txt", "alpha\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

path = Path("/repo/input.txt")
values = []
values.append(path.read_text())
values.append(path.read_text("utf-8"))
values.append(path.read_text(encoding="utf-8"))
values.append(path.read_text(encoding="utf-8-sig", errors="strict"))
write_text("/repo/out.txt", "|".join(values))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("alpha\n|alpha\n|alpha\n|alpha\n", host.ReadText("/repo/out.txt"));
    }

    [Fact]
    public void PathReadText_StaticDiagnosticsCatchUnsupportedEncodingErrorsAndArity()
    {
        var result = new LythonEngine().Run(
            """
from pathlib import Path

path = Path("/repo/input.txt")
path.read_text(encoding="latin-1")
path.read_text(errors="ignore")
path.read_text("utf-8", "strict", "extra")
write_text("/repo/out.txt", "side effect")
""",
            new MockLythonHost("/repo"));

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049" && d.Message.Contains("encoding", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049" && d.Message.Contains("errors", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3114" && d.Message.Contains("read_text", StringComparison.Ordinal));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ??
           string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
}
