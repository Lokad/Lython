using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG21: each Popen/run call copies the env table into the request, and Popen
// retains that request with the handle. The copied table structure must own
// governor charges; keys and values stay aliased to the guest dict.
public sealed class SubprocessEnvAccountingScenarioTests
{
    private const string EnvSetup =
        "import subprocess\n" +
        "env = {}\n" +
        "i = 0\n" +
        "while i < 5000:\n" +
        "    env['k' + str(i)] = 'v' + str(i)\n" +
        "    i = i + 1\n";

    [Fact]
    public async Task ManyRetainedPopenEnvsStayCharged()
    {
        var script = new LythonEngine().Compile(
            EnvSetup +
            "ps = []\n" +
            "j = 0\n" +
            "while j < 20:\n" +
            "    ps.append(subprocess.Popen([\"tool\"], env=env))\n" +
            "    j = j + 1\n" +
            "return len(ps)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DroppedPopenEnvsFitRoomyBudget()
    {
        var script = new LythonEngine().Compile(
            EnvSetup +
            "j = 0\n" +
            "while j < 20:\n" +
            "    p = subprocess.Popen([\"tool\"], env=env)\n" +
            "    j = j + 1\n" +
            "return j\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(20), sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(20), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PopenEnvStillProjects()
    {
        var script = new LythonEngine().Compile(
            "import subprocess\n" +
            "p = subprocess.Popen([\"tool\"], env={\"A\": \"1\"})\n" +
            "p.wait()\n" +
            "return p.returncode\n");
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedSubprocessResult(["tool"], 0, string.Empty, string.Empty);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        asyncHost.SeedSubprocessResult(["tool"], 0, string.Empty, string.Empty);
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
    }
}
