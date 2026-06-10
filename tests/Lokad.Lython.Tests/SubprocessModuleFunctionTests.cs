using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SubprocessModuleFunctionTests
{
    [Fact]
    public void SubprocessRun_UsesHostCapabilityAndReturnsCompletedProcess()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["rg", "Lython", "."], 0, "one\ntwo", "");

        var result = new LythonEngine().Run(
            """
import subprocess
proc = subprocess.run(["rg", "Lython", "."], capture_output=True)
write_text("/out.txt", str(proc.returncode) + "|" + proc.stdout + "|" + proc.stderr)
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("0|one\ntwo|", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubprocessRun_ThreadsInputAndCwdIntoTheHostRequest()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool", "--flag"], 0, "ok", "");

        var result = new LythonEngine().Run(
            """
import subprocess
subprocess.run(["tool", "--flag"], input="payload", cwd="/repo/work", timeout=1500)
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.Equal("/repo/work", host.LastSubprocessRequest!.Cwd);
        Assert.Equal(1500, host.LastSubprocessRequest.TimeoutMilliseconds);
        Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(host.LastSubprocessRequest.StandardInputUtf8.Span));
    }

    [Fact]
    public void SubprocessRun_ThreadsStreamModesTextOptionsAndEnvironmentIntoHostRequest()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool", "/repo/input.txt"], 0, "ok", "ignored");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
import subprocess

env = {"NAME": "VALUE"}
proc = subprocess.run(["tool", Path("/repo/input.txt")], stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, cwd=Path("/repo/work"), text=True, encoding="utf-8", errors="strict", env=env)
write_text("/out.txt", proc.stdout + "|" + str(proc.stderr))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("ok|None", host.ReadText("/out.txt"));
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.Equal(["tool", "/repo/input.txt"], host.LastSubprocessRequest!.Args);
        Assert.Equal("/repo/work", host.LastSubprocessRequest.Cwd);
        Assert.Equal(LythonSubprocessStreamMode.DevNull, host.LastSubprocessRequest.StandardInput);
        Assert.Equal(LythonSubprocessStreamMode.Pipe, host.LastSubprocessRequest.StandardOutput);
        Assert.Equal(LythonSubprocessStreamMode.StandardOutput, host.LastSubprocessRequest.StandardError);
        Assert.False(host.LastSubprocessRequest.UseShell);
        Assert.True(host.LastSubprocessRequest.TextMode);
        Assert.Equal("utf-8", host.LastSubprocessRequest.Encoding);
        Assert.Equal("strict", host.LastSubprocessRequest.Errors);
        Assert.NotNull(host.LastSubprocessRequest.Environment);
        Assert.Equal("VALUE", host.LastSubprocessRequest.Environment!["NAME"]);
    }

    [Fact]
    public void SubprocessRun_AllowsStringOrPathCommandWhenShellIsExplicit()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["echo hi"], 0, "hi", "");
        host.SeedSubprocessResult(["/repo/script.sh"], 0, "path", "");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
import subprocess
first = subprocess.run("echo hi", shell=True, stdout=subprocess.PIPE)
second = subprocess.run(Path("/repo/script.sh"), shell=True, stdout=subprocess.PIPE)
write_text("/out.txt", first.stdout + "|" + first.args[0] + "|" + second.stdout + "|" + second.args[0])
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("hi|echo hi|path|/repo/script.sh", host.ReadText("/out.txt"));
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.True(host.LastSubprocessRequest!.UseShell);
        Assert.Equal(["/repo/script.sh"], host.LastSubprocessRequest.Args);
    }

    [Fact]
    public void SubprocessWrappers_ReturnPythonCompatibleShapes()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["out"], 0, "value", "");
        host.SeedSubprocessResult(["call"], 3, "", "");
        host.SeedSubprocessResult(["check"], 0, "", "");

        var result = new LythonEngine().Run(
            """
import subprocess
out = subprocess.check_output(["out"])
code = subprocess.call(["call"])
checked = subprocess.check_call(["check"])
write_text("/out.txt", out + "|" + str(code) + "|" + str(checked))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("value|3|0", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CompletedProcess_ExposesArgsAndNoneForUncapturedOutput()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["quiet"], 0, "hidden", "ignored");

        var result = new LythonEngine().Run(
            """
import subprocess
proc = subprocess.run(["quiet"])
proc.check_returncode()
write_text("/out.txt", str(proc.stdout) + "|" + str(proc.stderr) + "|" + proc.args[0])
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("None|None|quiet", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubprocessRun_CheckMode_FailsCleanlyForNonZeroExit()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["fail"], 7, "out", "bad");

        var result = new LythonEngine().Run(
            """
import subprocess
try:
    subprocess.run(["fail"], check=True, capture_output=True)
except subprocess.CalledProcessError as err:
    write_text("/out.txt", err.type + "|" + str(err.returncode) + "|" + err.cmd[0] + "|" + err.output + "|" + err.stderr + "|" + str(len(err.args)))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("CalledProcessError|7|fail|out|bad|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CompletedProcess_CheckReturnCodeFailsCleanlyForNonZeroExit()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["fail"], 8, "", "");

        var result = new LythonEngine().Run(
            """
import subprocess
proc = subprocess.run(["fail"], capture_output=True)
try:
    proc.check_returncode()
except subprocess.SubprocessError as err:
    write_text("/out.txt", err.type + "|" + str(err.returncode) + "|" + err.cmd[0])
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("CalledProcessError|8|fail", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubprocessObjects_ConstructorsList2CmdlineAndUnsupportedHelpersAreExposed()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();

        var result = new LythonEngine().Run(
            """
import subprocess

manual = subprocess.CompletedProcess(["cmd"], 4, stdout="o", stderr="e")
made = subprocess.CalledProcessError(5, ["cmd"], output="out", stderr="err")
vals = []
try:
    manual.check_returncode()
except subprocess.CalledProcessError as err:
    vals.append(err.type + ":" + str(err.returncode) + ":" + err.stdout + ":" + err.stderr)
vals.append(made.type + ":" + str(made.returncode) + ":" + made.output + ":" + made.stderr)
vals.append(subprocess.list2cmdline(["a b", "c\"d", "tail\\"]))
try:
    subprocess.Popen(["cmd"])
except NotImplementedError as err:
    vals.append(err.type)
try:
    subprocess.getoutput("cmd")
except NotImplementedError as err:
    vals.append(err.type)
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("CalledProcessError:4:o:e|CalledProcessError:5:out:err|\"a b\" \"c\\\"d\" tail\\|NotImplementedError|NotImplementedError", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("subprocess.run([\"tool\"], input=\"x\", stdin=subprocess.PIPE)")]
    [InlineData("subprocess.run([\"tool\"], capture_output=True, stdout=subprocess.PIPE)")]
    [InlineData("subprocess.check_output([\"tool\"], stdout=subprocess.PIPE)")]
    public void SubprocessRun_RejectsConflictingStreamOptions(string statement)
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();

        var result = new LythonEngine().Run(
            $"""
import subprocess
{statement}
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void SubprocessExpandedSurface_InvalidStaticContractsFailAtCompileTime()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();

        var result = new LythonEngine().Run(
            """
import subprocess

subprocess.CompletedProcess(["cmd"], "bad")
subprocess.list2cmdline(["ok", 1])
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("returncode", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("iterable of strings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubprocessRunAsync_AwaitsAsynchronousHostRunner()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.CompleteSubprocessAsynchronously();
        host.SeedSubprocessResult(["tool"], 0, "done", "");

        var result = await new LythonEngine().RunAsync(
            """
import subprocess
proc = subprocess.run(["tool"], capture_output=True)
write_text("/out.txt", str(proc.returncode) + "|" + proc.stdout)
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.True(host.SubprocessCompletedAsynchronously);
        Assert.Equal("0|done", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubprocessRun_WithAsynchronousHostRunner_FailsFastWithRunAsyncGuidance()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.CompleteSubprocessAsynchronously();
        host.SeedSubprocessResult(["tool"], 0, "done", "");

        var result = new LythonEngine().Run(
            """
import subprocess
subprocess.run(["tool"])
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure.Message, StringComparison.Ordinal);
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ??
           string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
}
