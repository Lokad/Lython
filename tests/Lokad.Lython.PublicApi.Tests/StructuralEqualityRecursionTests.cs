using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R01: cyclic and deeply nested structural comparisons must raise catchable
// RecursionError instead of overflowing the CLR stack, in both sync (executable)
// and async (lowered) paths, with subsequent-run health preserved.
public sealed class StructuralEqualityRecursionTests
{
    private static void AssertRecursionError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RecursionError", result.Failure!.ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    private static async Task AssertCyclicFailsSyncAsync(string body)
    {
        var sync = new LythonEngine().Run(body, new MockLythonHost());
        AssertRecursionError(sync);
        var asyncResult = await new LythonEngine().RunAsync(body, new MockLythonHost());
        AssertRecursionError(asyncResult);
    }

    [Fact]
    public async Task CyclicListsRaiseRecursionError()
    {
        await AssertCyclicFailsSyncAsync("a = []\nb = []\na.append(a)\nb.append(b)\nprint(a == b)\n");
    }

    [Fact]
    public async Task CyclicListsNotEqualRaises()
    {
        await AssertCyclicFailsSyncAsync("a = []\nb = []\na.append(a)\nb.append(b)\nprint(a != b)\n");
    }

    [Fact]
    public async Task IdenticalCyclicListEqualsItself()
    {
        var sync = new LythonEngine().Run("a = []\na.append(a)\nprint(a == a)\nprint(a != a)\n", new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("True\nFalse\n", sync.StandardOutput);
        var asyncResult = await new LythonEngine().RunAsync("a = []\na.append(a)\nprint(a == a)\nprint(a != a)\n", new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("True\nFalse\n", asyncResult.StandardOutput);
    }

    [Fact]
    public void SharedSubstructureComparesEqual()
    {
        const string code = "a = []\na.append(1)\nb = [a]\nc = [a]\nprint(b == c)\n";
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("True\n", sync.StandardOutput);
    }

    [Fact]
    public async Task CyclicDictsRaise()
    {
        await AssertCyclicFailsSyncAsync("d1 = {}\nd2 = {}\nd1[\"x\"] = d1\nd2[\"x\"] = d2\nprint(d1 == d2)\n");
    }

    [Fact]
    public async Task CyclicDequeRaises()
    {
        await AssertCyclicFailsSyncAsync("from collections import deque\na = deque()\nb = deque()\na.append(a)\nb.append(b)\nprint(a == b)\n");
    }

    [Fact]
    public async Task CyclicTupleViaListRaises()
    {
        await AssertCyclicFailsSyncAsync("a = []\nt = (a,)\na.append(t)\nb = []\ns = (b,)\nb.append(s)\nprint(t == s)\n");
    }

    [Fact]
    public async Task CyclicOrderingRaisesButSelfOrderingIsFalse()
    {
        await AssertCyclicFailsSyncAsync("a = []\nb = []\na.append(a)\nb.append(b)\nprint(a < b)\n");
        var self = new LythonEngine().Run("a = []\na.append(a)\nprint(a < a)\n", new MockLythonHost());
        Assert.True(self.Success, self.Failure?.Message);
        Assert.Equal("False\n", self.StandardOutput);
    }

    [Fact]
    public void MembershipOnCyclicDoesNotCrash()
    {
        var result = new LythonEngine().Run("a = []\na.append(a)\nprint(a in [1, 2, 3])\nprint(1 in a)\n", new MockLythonHost());
        // [a] contains a cycle but membership scans compare ints first; the cyclic
        // candidate never equals an int, so no recursion is needed.
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("False\nFalse\n", result.StandardOutput);
    }

    [Fact]
    public async Task DeeplyNestedAcyclicFailsBeyondBudgetAndSucceedsWithin()
    {
        const string build600 = "a = []\ncur = a\nfor i in range(600):\n cur.append([])\n cur = cur[0]\nb = []\ncur = b\nfor i in range(600):\n cur.append([])\n cur = cur[0]\nprint(a == b)\n";
        await AssertCyclicFailsSyncAsync(build600);
        const string build100 = "a = []\ncur = a\nfor i in range(100):\n cur.append([])\n cur = cur[0]\nb = []\ncur = b\nfor i in range(100):\n cur.append([])\n cur = cur[0]\nprint(a == b)\n";
        var ok = new LythonEngine().Run(build100, new MockLythonHost());
        Assert.True(ok.Success, ok.Failure?.Message);
        Assert.Equal("True\n", ok.StandardOutput);
    }

    [Fact]
    public void NestedTupleKeysRemainUsable()
    {
        var result = new LythonEngine().Run("d = {(1, 2): 3}\nprint(d[(1, 2)])\n", new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3\n", result.StandardOutput);
    }

    [Fact]
    public void SubsequentRunSucceedsAfterRecursionFailure()
    {
        var engine = new LythonEngine();
        var host = new MockLythonHost();
        var failed = engine.Run("a = []\nb = []\na.append(a)\nb.append(b)\nprint(a == b)\n", host);
        AssertRecursionError(failed);
        var ok = engine.Run("print(1 + 1)\n", host);
        Assert.True(ok.Success, ok.Failure?.Message);
        Assert.Equal("2\n", ok.StandardOutput);
    }

    [Fact]
    public void LongScanRespectsStepBudget()
    {
        // 5000-element equality with a 100-step budget must fail via periodic
        // structural work checks, not via the outer single budget check.
        const string code = "a = [0] * 5000\nb = [0] * 5000\nprint(a == b)\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionSteps = 100 });
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Contains("maximum execution step count exceeded", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LongScanRespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        const string code = "a = [0] * 5000\nb = [0] * 5000\nprint(a == b)\n";
        var result = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { CancellationToken = cts.Token });
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Contains("execution canceled", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CyclicEqualityInIsolatedChildDoesNotCrashHost()
    {
        var probe = SubprocessProbeRunner.FindProbeDll();
        var snippets = new[]
        {
            "a = []\nb = []\na.append(a)\nb.append(b)\nprint(a == b)\n",
            "d1 = {}\nd2 = {}\nd1[\"x\"] = d1\nd2[\"x\"] = d2\nprint(d1 == d2)\n",
            "from collections import deque\na = deque()\nb = deque()\na.append(a)\nb.append(b)\nprint(a == b)\n",
            "a = []\nb = []\na.append(a)\nb.append(b)\nprint(a < b)\n",
            "print(1 + 1)\n",
        };
        var run = SubprocessProbeRunner.Run("dotnet", [probe, "--batch-json"], JsonSerializer.Serialize(snippets), timeout: TimeSpan.FromMinutes(2));
        Assert.True(run.ExitCode is 0 or 1, "probe crashed with " + run.ExitCode + ": " + run.StandardError);
        var lines = run.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(snippets.Length, lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            using var document = JsonDocument.Parse(lines[i]);
            var lython = document.RootElement.GetProperty("Lython");
            if (i < snippets.Length - 1)
            {
                Assert.False(lython.GetProperty("Success").GetBoolean());
                var failure = lython.GetProperty("Failure");
                Assert.Equal("RecursionError", failure.GetProperty("ExceptionType").GetString());
                Assert.Contains("maximum recursion depth exceeded", failure.GetProperty("Message").GetString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.True(lython.GetProperty("Success").GetBoolean());
            }
        }
    }
}
