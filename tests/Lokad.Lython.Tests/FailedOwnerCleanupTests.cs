using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FailedOwnerCleanupTests
{
    [Theory]
    [InlineData("ordered")]
    [InlineData("instance")]
    public void FailedConstructionBatchesReturnSharedAccounting(string kind)
    {
        var setup = kind == "ordered"
            ? "from collections import OrderedDict\nsource = [(1, 1), (2,)]\n"
            : "import copy\nclass Bomb:\n def __deepcopy__(self, memo):\n  raise ValueError('stop')\nclass Holder:\n pass\nsource = Holder()\nsource.first = 1\nsource.second = Bomb()\n";
        var operation = kind == "ordered" ? "OrderedDict(source)" : "copy.deepcopy(source)";
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(setup + "for i in range(16):\n try:\n  " + operation +
            "\n except (TypeError, ValueError):\n  pass\nreturn i + 1\n", budget);
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
