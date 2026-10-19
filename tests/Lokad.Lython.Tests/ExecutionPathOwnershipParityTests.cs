using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Literal storage and retained results stay funded through either public API.
public sealed class ExecutionPathOwnershipParityTests
{
    public static TheoryData<string> Programs => new()
    {
        "x = [1, 2, 3] * 500\nreturn len(x)\n",
        "x = {" + string.Join(",", Enumerable.Range(0, 200)) + "}\nreturn len(x)\n",
        "x = (" + string.Join(",", Enumerable.Range(0, 200)) + ")\nreturn len(x)\n",
        "x = {" + string.Join(",", Enumerable.Range(0, 50).Select(static i => "\"k" + i + "\": " + i)) + "}\nreturn len(x)\n",
        "x = {0: 0}\nreturn len(x)\n",
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LiteralPathsPreserveResultsAndBoundedOwnership(string source)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        const long cap = 1024 * 1024;
        using (var owner = budget.CreateScope())
        {
            var options = new LythonRunOptions { MemoryBudget = owner};
            using var compiled = new LythonEngine().Compile(source);
            Assert.True(compiled.IsValid);
            using (var syncResult = compiled.Run(new MockLythonHost(), options))
            using (var asyncResult = await compiled.RunAsync(new MockLythonHost(), options))
            {
                Assert.True(syncResult.Success, syncResult.Failure?.Message);
                Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
                Assert.Equal(syncResult.ReturnValue, asyncResult.ReturnValue);
                Assert.InRange(syncResult.PeakExecutionMemoryBytes, 1, cap);
                Assert.InRange(asyncResult.PeakExecutionMemoryBytes, 1, cap);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
