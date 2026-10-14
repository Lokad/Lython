using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CopyOwnerLifetimeTests
{
    public static IEnumerable<object[]> KindsAndModes()
    {
        foreach (var kind in new[] { "list", "dict", "set", "deque" })
            foreach (var asynchronous in new[] { false, true })
                yield return [kind, asynchronous];
    }

    [Theory]
    [MemberData(nameof(KindsAndModes))]
    public async Task FailedDeepcopyBatchesReturnTheirOwnersAfterCleanup(string kind, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(262144);
        for (var batch = 0; batch < 8; batch++)
        {
            using var result = await Run(kind, asynchronous, retain: false, budget);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(16), result.ReturnValue);
            result.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(KindsAndModes))]
    public async Task EscapedPartialDeepcopiesKeepTheirOwnersFunded(string kind, bool asynchronous)
    {
        using var result = await Run(kind, asynchronous, retain: true);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    private static async Task<LythonExecutionResult> Run(string kind, bool asynchronous, bool retain, ILythonMemoryBudget? budget = null)
    {
        var expression = kind switch
        {
            "list" => "[1, Bomb()]",
            "dict" => "{1: 1, 2: Bomb()}",
            "set" => "{Bomb(), 1}",
            _ => "deque([1, Bomb()])"
        };
        var capture = retain ? "  saved.append(memo[id(source)])\n" : "";
        var source = "with open('/ready', 'r') as stream:\n stream.read()\n" +
            "import copy\nfrom collections import deque\nsaved = []\n" +
            "class Bomb:\n def __deepcopy__(self, memo):\n" + capture +
            "  raise ValueError('stop')\nsource = " + expression +
            "\nfor i in range(1500):\n try:\n  copy.deepcopy(source)\n except ValueError:\n  pass\nreturn i + 1\n";
        using var script = new LythonEngine().Compile(source.Replace("1500", (retain ? 1500 : 16).ToString()), budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/ready", "ready");
            var result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
            return result;
        }
        var synchronous = new MockLythonHost();
        synchronous.SeedFile("/ready", "ready");
        var completed = script.Run(synchronous, options);
        Assert.InRange(completed.PeakExecutionMemoryBytes, 1, 65536);
        return completed;
    }
}
