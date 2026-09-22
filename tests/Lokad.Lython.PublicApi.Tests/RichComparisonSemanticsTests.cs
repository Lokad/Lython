using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N11 (equality part): rich ==/!= return raw method results with
// strict-subclass reflected precedence; chains/conditions truth-test.
public sealed class RichComparisonSemanticsTests
{
    private static async Task AssertBothModes(string source, string expectedOutput)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expectedOutput, sync.StandardOutput);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expectedOutput, asyncResult.StandardOutput);
    }

    [Fact]
    public async Task StrictSubclassReflectedGoesFirst()
        => await AssertBothModes(
            "class A:\n    def __eq__(self, other):\n        return False\nclass B(A):\n    def __eq__(self, other):\n        return True\nprint(A() == B())\nprint(A() != B())\n",
            "True\nFalse\n");

    [Fact]
    public async Task RawEqResultIsPrinted()
        => await AssertBothModes(
            "class E:\n    def __eq__(self, other):\n        return \"same\"\nprint(E() == E())\n",
            "same\n");

    [Fact]
    public async Task NeFallsBackToNegatedEqTruth()
        => await AssertBothModes(
            "class E:\n    def __eq__(self, other):\n        return \"same\"\nprint(E() != E())\nprint((E() == E()) == True)\n",
            "False\nFalse\n");

    [Fact]
    public async Task ReflectedSideEffectOrder()
        => await AssertBothModes(
            "log = []\nclass A:\n    def __eq__(self, other):\n        log.append(\"A\")\n        return False\nclass B(A):\n    def __eq__(self, other):\n        log.append(\"B\")\n        return True\nprint(A() == B())\nprint(log)\n",
            "True\n['B']\n");

    [Fact]
    public async Task NotImplementedDeclinesToReflected()
        => await AssertBothModes(
            "class A:\n    def __eq__(self, other):\n        return NotImplemented\nclass B:\n    def __eq__(self, other):\n        return True\nprint(A() == B())\nprint(B() == A())\n",
            "True\nTrue\n");

    [Fact]
    public async Task BothNotImplementedFallsBackToStructural()
        => await AssertBothModes(
            "class A:\n    def __eq__(self, other):\n        return NotImplemented\nprint(A() == A())\nprint(A() != A())\n",
            "False\nTrue\n");

    [Fact]
    public async Task ChainsShortCircuitAndReturnBool()
        => await AssertBothModes(
            "log = []\nclass E:\n    def __eq__(self, other):\n        log.append(1)\n        return True\nprint(E() == E() == E())\nprint(len(log))\nprint(1 < 2 < 3)\nprint(1 < 2 > 3)\n",
            "True\n2\nTrue\nFalse\n");

    [Fact]
    public async Task NanStaysUnequalToItself()
        => await AssertBothModes(
            "x = float(\"nan\")\nprint(x == x)\nprint(x != x)\n",
            "False\nTrue\n");

    [Fact]
    public async Task SixOperatorsOnInts()
        => await AssertBothModes(
            "print(1 < 2)\nprint(2 <= 2)\nprint(3 > 2)\nprint(3 >= 4)\nprint(1 == 1)\nprint(1 != 1)\n",
            "True\nTrue\nTrue\nFalse\nTrue\nFalse\n");

    [Fact]
    public async Task DelayedEqSuspendsAsync()
    {
        const string code = "from pathlib import Path\nclass E:\n    def __eq__(self, other):\n        return Path(\"/v.txt\").read_text() == \"x\\n\"\nprint(E() == E())\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.txt", "x\n");
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("True\n", asyncResult.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void DelayedEqFailsFastSync()
    {
        const string code = "from pathlib import Path\nclass E:\n    def __eq__(self, other):\n        return Path(\"/v.txt\").read_text() == \"x\\n\"\nprint(E() == E())\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.txt", "x\n");
        var result = script.Run(host);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
    }
}
