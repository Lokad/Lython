using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Popen handles and CompletedProcess values retain their original args lists.
// Retained payloads remain funded independently of the list growth policy.
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import subprocess\n" +
            "ps = []\n" +
            "i = 0\n" +
            "while i < 2000:\n" +
            "    ps.append(subprocess.Popen(" + ArgvCode(big1, big2) + "))\n" +
            "    i = i + 1\n" +
            "return len(ps)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(2097152) };
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManyRetainedRunArgsStayCharged(bool asynchronous)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import subprocess
            ps = []
            for i in range(32):
                ps.append(subprocess.run(['tool', 'A' * 4096 + str(i)]))
            return (len(ps), ps[0].args[1], ps[-1].args[1])
            """);
        Assert.True(script.IsValid);

        // Distinct live payloads exceed this allowance without depending on
        // an exact number of list slots or repeated charges for shared literals.
        foreach (var limit in new[] { 128000, 1048576 })
        {
            var budget = new LythonMemoryBudget(limit);
            var host = new MockLythonHost();
            host.EnableSubprocess();
            for (var i = 0; i < 32; i++)
                host.SeedSubprocessResult(["tool", new string('A', 4096) + i], 0);
            using var result = asynchronous
                ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget });
            if (limit == 128000)
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.True(host.SubprocessRequests.Count < 32);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object?[] { new BigInteger(32), new string('A', 4096) + "0", new string('A', 4096) + "31" },
                    Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue));
                Assert.True(budget.ReservedBytes > 8192);
            }
            result.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task BoundedPopenArgsBatchFitsRoomyBudget()
    {
        var big1 = new string('A', 500);
        var big2 = new string('B', 500);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import subprocess\n" +
            "i = 0\n" +
            "while i < 8:\n" +
            "    p = subprocess.Popen(" + ArgvCode(big1, big2) + ")\n" +
            "    i = i + 1\n" +
            "return i\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8388608) };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(8), sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(8), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PopenArgsStillProject()
    {
        var big1 = new string('A', 8);
        var big2 = new string('B', 8);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
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
