using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: lambda instances own lifetime with reclamation.
public class LambdaLifetimeScenarioTests : LifetimeScenario
{    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);    [Fact]
    public async Task LambdasDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    f = lambda: i\nreturn 0\n", "0", Iterations(100000));
}
