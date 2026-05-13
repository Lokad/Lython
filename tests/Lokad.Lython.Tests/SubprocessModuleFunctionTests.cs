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

        Assert.True(result.Success, result.Failure?.Message);
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

        Assert.True(result.Success, result.Failure?.Message);
        Assert.NotNull(host.LastSubprocessRequest);
        Assert.Equal("/repo/work", host.LastSubprocessRequest!.Cwd);
        Assert.Equal(1500, host.LastSubprocessRequest.TimeoutMilliseconds);
        Assert.Equal("payload", System.Text.Encoding.UTF8.GetString(host.LastSubprocessRequest.StandardInputUtf8.Span));
    }

    [Fact]
    public void SubprocessRun_CheckMode_FailsCleanlyForNonZeroExit()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["fail"], 7, "", "bad");

        var result = new LythonEngine().Run(
            """
import subprocess
subprocess.run(["fail"], check=True)
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("return code 7", result.Failure.Message, StringComparison.Ordinal);
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

        Assert.True(result.Success, result.Failure?.Message);
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
}
