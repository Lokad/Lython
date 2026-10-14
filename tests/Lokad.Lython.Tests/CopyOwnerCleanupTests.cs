using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CopyOwnerCleanupTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("dict")]
    [InlineData("set")]
    [InlineData("deque")]
    public void FailedDeepcopyBatchesKeepTheirBoundedBudget(string kind)
    {
        var expression = kind switch
        {
            "list" => "[1, Bomb()]",
            "dict" => "{1: 1, 2: Bomb()}",
            "set" => "{Bomb(), 1}",
            _ => "deque([1, Bomb()])"
        };
        var source = "import copy\nfrom collections import deque\n" +
            "class Bomb:\n def __deepcopy__(self, memo):\n  raise ValueError('stop')\nsource = " + expression +
            "\nfor i in range(1500):\n try:\n  copy.deepcopy(source)\n except ValueError:\n  pass\nreturn i + 1\n";
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(source.Replace("1500", "16"), budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        for (var batch = 0; batch < 8; batch++)
        {
            using var result = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
            Assert.True(result.Success, result.Failure?.ExceptionType + ":" + result.Failure?.Message);
            Assert.Equal(new BigInteger(16), result.ReturnValue);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}