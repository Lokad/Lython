using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// Finite host batches release lambda working storage at execution cleanup.
public class LambdaLifetimeScenarioTests : LifetimeScenario
{    private long StreamingBudget => Budget(3145728);

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);    [Fact]
    public async Task LambdasDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    f = lambda: i\nreturn 0\n", "0", Iterations(100000));
}
