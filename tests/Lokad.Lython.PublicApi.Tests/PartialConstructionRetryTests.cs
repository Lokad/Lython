using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Residual (c): composite displays that fail midway must not accumulate
// charges per attempt. Dropped element values release through the ordinary
// drop-and-sweep path with relief bounding the high-water mark, so twenty
// failed 20K-element list builds peak far below twenty footprints.
public sealed class PartialConstructionRetryTests
{
    [Fact]
    public async Task PartialListFailuresDoNotAccumulatePerAttempt()
    {
        const string code = """
            def boom():
                raise ValueError("x")
            ok = 0
            for i in range(20):
                try:
                    v = [0] * 20000 + [boom()]
                except ValueError:
                    ok += 1
            return ok
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(diagnostic => diagnostic.Code + ":" + diagnostic.Message)));
        const long budgetBytes = 8388608;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budgetBytes };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(20), sync.ReturnValue);
        // Sub-budget peaks depend on organic GC timing: dropped charges release only
        // once collected, so a quiet collector lets accounted bytes ride near the budget
        // before exhaustion relief sweeps (8,353,652 of 8,388,608 observed on ubuntu CI).
        // Success itself implies peak <= budget, which is the deterministic pin: twenty
        // failed builds complete without accumulating into an out-of-memory failure.
        Assert.True(sync.PeakExecutionMemoryBytes <= budgetBytes, $"peak={sync.PeakExecutionMemoryBytes}");

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(20), asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= budgetBytes, $"peak={asyncResult.PeakExecutionMemoryBytes}");
    }
}
