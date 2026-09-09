using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: completed Popen objects retain raw output buffers alongside decoded
/// strings, so the raw bytes charge the execution budget too.
/// </summary>
public sealed class PopenOutputAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedPopenOutputsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import subprocess
            ps = []
            i = 0
            while i < 10:
                p = subprocess.Popen(["tool"], stdout=subprocess.PIPE)
                p.wait()
                ps.append(p)
                i = i + 1
            return len(ps)
            """);
        Assert.True(script.IsValid);
        // Decoded output alone fits; decoded plus raw does not.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 150000 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool"], 0, new string('x', 10240), string.Empty);
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        asyncHost.SeedSubprocessResult(["tool"], 0, new string('x', 10240), string.Empty);
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task PopenCommunicateStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            import subprocess
            p = subprocess.Popen(["tool"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            out, err = p.communicate()
            return [out, err, p.returncode]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "answer", "warning", new BigInteger(0) };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool"], 0, "answer", "warning");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        asyncHost.SeedSubprocessResult(["tool"], 0, "answer", "warning");
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}