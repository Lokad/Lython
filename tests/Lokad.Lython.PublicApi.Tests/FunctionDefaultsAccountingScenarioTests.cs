using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG11: default-argument maps survive with the function value. The 128B
// constructed-value unit covers the wrapper, binding plan and an empty map,
// so each defaulted parameter owns one 64B table slot beside it. Pre-fix,
// 5000 retained 40-default defs peak near 1.4MB; owning the maps trips the
// 6MB budgets below.
public sealed class FunctionDefaultsAccountingScenarioTests
{
    private static string DefaultParams()
    {
        var pars = "";
        for (var k = 1; k <= 40; k++)
        {
            if (k > 1)
            {
                pars += ", ";
            }
            pars += "a" + k.ToString("D2") + "=0";
        }
        return pars;
    }

    [Fact]
    public async Task ManyRetainedDefDefaultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            "fs = []\n" +
            "i = 0\n" +
            "while i < 5000:\n" +
            "    def f(" + DefaultParams() + "):\n" +
            "        return a01\n" +
            "    fs.append(f)\n" +
            "    i = i + 1\n" +
            "return len(fs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyRetainedLambdaDefaultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            "fs = []\n" +
            "i = 0\n" +
            "while i < 5000:\n" +
            "    fs.append(lambda " + DefaultParams() + ": a01)\n" +
            "    i = i + 1\n" +
            "return len(fs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DroppedDefDefaultsFitRoomyBudget()
    {
        var script = new LythonEngine().Compile(
            "i = 0\n" +
            "total = 0\n" +
            "while i < 5000:\n" +
            "    def f(" + DefaultParams() + "):\n" +
            "        return a01\n" +
            "    total = total + f()\n" +
            "    i = i + 1\n" +
            "return total\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 20971520 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DefaultValuesStillApply()
    {
        var script = new LythonEngine().Compile(
            "def f(a=1, b=2):\n" +
            "    return a + b\n" +
            "g = lambda x=5: x * 2\n" +
            "return [f(), f(10), f(10, 20), g(), g(21)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(12), new BigInteger(30), new BigInteger(10), new BigInteger(42) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
