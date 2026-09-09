using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R43: statements on the same source line must be separated by a semicolon,
/// and numbers glued to identifiers are one invalid literal. Both used to
/// parse (and run) silently with CPython-divergent meaning.
/// </summary>
public sealed class StatementSeparationScenarioTests
{
    [Fact]
    public void JuxtaposedStatementsFail()
    {
        var cases = new List<string>
        {
            "print(1)print(2)\n",
            "a = 1 b = 2\n",
            "f = print\nf(1)f(2)\n",
            "def f():\n    return [1]return f()\n",
            "def f():\n    a = 1 b = 2\n    return a\n",
        };
        foreach (var source in cases)
        {
            var script = new LythonEngine().Compile(source);
            Assert.False(script.IsValid, source);
            Assert.Contains(script.Diagnostics, d => d.Code == "LA1001");
        }
    }

    [Fact]
    public void GluedNumbersFail()
    {
        var cases = new List<string>
        {
            "x = 1x\n",
            "x = 0x1F\n",
            "x = 1j\n",
            "x = 1x = 2\n",
        };
        foreach (var source in cases)
        {
            var script = new LythonEngine().Compile(source);
            Assert.False(script.IsValid, source);
            Assert.Contains(script.Diagnostics, d => d.Code == "LA1009");
        }
    }

    [Fact]
    public async Task ValidSeparatorsStillCompileAndRun()
    {
        var script = new LythonEngine().Compile(
            """
            import os, sys
            a = 1; b = 2
            def f(x):
                if x:
                    return x
                return 0
            total = 0
            for i in [1, 2]:
                total = total + f(i)
            else:
                total = total + 9
            total = total + (1 if 1in[1, 2] else 0)
            return total
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(13), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(13), asyncResult.ReturnValue);
    }
}
