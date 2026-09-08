using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
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
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("string or bytes operands", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sum(1)\n")]
    [InlineData("sum(None)\n")]
    [InlineData("sum(True)\n")]
    public void Sum_StaticDiagnosticsCatchProvablyNonIterableArgumentShapes(string source)
    {
        var result = new LythonEngine().Run(
            source + "__lython_file = open(\"/out.txt\", \"w\")\n__lython_file.write(\"side effect\")\n__lython_file.close()\n",
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
__lython_file = open("/out.txt", "w")
__lython_file.write(out)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("lython\n", host.ReadText("/out.txt"));
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.Equal(["tool", "--version"], host.LastSubprocessRequest.Args);
        Assert.NotNull(host.LastSubprocessRequest);
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
__lython_file = open("/out.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041" && d.Message.Contains("subprocess", StringComparison.Ordinal));
        Assert.False(host.Exists("/out.txt"));
    }

    [Fact]
    public void CompleteTextWorkflow_CoversSupportedAgentScriptShapes()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/input.txt", "alpha\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

values = ["a", "b"]
out = []
for index, value in enumerate(values, 1):
    out.append(f"{index}:{value}")

text = "alpha beta"
text = text.replace(
    "alpha",
    "gamma",
)

source = Path("/repo/input.txt").read_text(encoding="utf-8", errors="strict")
with open("/repo/output.txt", "w", encoding="utf-8", newline="") as handle:
    handle.write(source.strip() + "|" + text + "|" + ",".join(out))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("alpha|gamma beta|1:a,2:b", host.ReadText("/repo/output.txt"));
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
__lython_file = open("/repo/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
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
path.read_text(encoding="utf-16")
path.read_text(errors="surrogateescape")
path.read_text("utf-8", "strict", "extra")
path.read_text("utf-8", "strict", "", "too-many")
__lython_file = open("/repo/out.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            new MockLythonHost("/repo"));

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049" && d.Message.Contains("encoding", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049" && d.Message.Contains("error handlers", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049" && d.Message.Contains("newline", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3114" && d.Message.Contains("read_text", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.txt").read_text(errors="surrogateescape")
""",
        "LA3049",
        "error handlers")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.bin").read_bytes()
""",
        "LA3047",
        "is not supported by Lython")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.bin").open("rb")
""",
        "LA3061",
        "binary modes like 'rb' and 'wb' are unsupported")]
    [InlineData(
        """
open("/repo/input.bin", "rb")
""",
        "LA3001",
        "binary modes like 'rb' and 'wb' are unsupported")]
    public void IntentionalDivergences_ReportStaticDiagnosticsBeforeHostEffects(
        string source,
        string expectedCode,
        string expectedMessage)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/input.txt", "alpha");

        var result = new LythonEngine().Run(
            source + """

__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(
            result.Diagnostics,
            d => d.Code == expectedCode && d.Message.Contains(expectedMessage, StringComparison.Ordinal));
        Assert.False(host.Exists("/repo/created.txt"));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ??
           string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
}
