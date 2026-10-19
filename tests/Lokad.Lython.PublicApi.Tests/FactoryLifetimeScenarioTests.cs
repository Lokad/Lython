using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: comprehension results own lifetime like displays do.
public class FactoryLifetimeScenarioTests : LifetimeScenario
{    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);    [Fact]
    public async Task ListComprehensionsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = [j for j in range(2)]\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task DictComprehensionsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = {j: j for j in range(2)}\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task ListDisplaysDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = [1, 2]\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task SetComprehensionOverRangeCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = {j for j in range(2)}\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task SetComprehensionOverTupleCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = {j for j in (1,)}\nreturn 0\n", "0", Iterations(100000));
}
