using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: lambda instances own lifetime with reclamation.
public class LambdaLifetimeScenarioTests : LifetimeScenario
{    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }    [Fact]
    public async Task LambdasDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + Iterations(100000) + "):\n    f = lambda: i\nreturn 0\n", "0");
}
