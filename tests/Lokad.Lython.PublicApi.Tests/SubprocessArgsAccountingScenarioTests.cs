using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG21: Popen handles and CompletedProcess values retain their args lists, so
// the argument strings must own governor charges like any retained value.
// Pre-fix peaks sit near 1.06MB (2000 retained Popens) and 82KB (200 retained
// runs); owning 128B plus UTF-8 length per retained arg trips the budgets below.
public sealed class SubprocessArgsAccountingScenarioTests
{
    private static string ArgvCode(string big1, string big2)
        => "[\"tool\", \"" + big1 + "\", \"" + big2 + "\"]";

    private static void Seed(MockLythonHost host, string big1, string big2, string stdout = "")
        => host.SeedSubprocessResult(["tool", big1, big2], 0, stdout, string.Empty);

    [Fact]
    public async Task ManyRetainedPopenArgsStayCharged()
    {
        var big1 = new string('A', 500);
        var big2 = new string('B', 500);
        var script = new LythonEngine().Compile(
            "import subprocess\n" +
            "ps = []\n" +
            "i = 0\n" +
            "while i < 2000:\n" +
            "    ps.append(subprocess.Popen(" + ArgvCode(big1, big2) + "))\n" +
            "    i = i + 1\n" +
            "return len(ps)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
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
    public async Task ManyRetainedRunArgsStayCharged()
    {
        var big1 = new string('A', 500);
        var big2 = new string('B', 500);
        var script = new LythonEngine().Compile(
            "import subprocess\n" +
            "ps = []\n" +
            "i = 0\n" +
            "while i < 500:\n" +
            "    ps.append(subprocess.run(" + ArgvCode(big1, big2) + "))\n" +
            "    i = i + 1\n" +
            "return len(ps)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 400000 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        Seed(host, big1, big2);
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        Seed(asyncHost, big1, big2);
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DroppedPopenArgsFitRoomyBudget()
    {
        var big1 = new string('A', 500);
        var big2 = new string('B', 500);
        var script = new LythonEngine().Compile(
            "import subprocess\n" +
            "i = 0\n" +
            "while i < 2000:\n" +
            "    p = subprocess.Popen(" + ArgvCode(big1, big2) + ")\n" +
            "    i = i + 1\n" +
            "return i\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2000), sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PopenArgsStillProject()
    {
        var big1 = new string('A', 8);
        var big2 = new string('B', 8);
        var script = new LythonEngine().Compile(
            "import subprocess\n" +
            "p = subprocess.Popen(" + ArgvCode(big1, big2) + ")\n" +
            "return p.args\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "tool", big1, big2 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
