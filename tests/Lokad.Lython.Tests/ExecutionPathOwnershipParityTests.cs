using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Executable buffers have their own admitted capacity. Compare observable
// values, bounded peaks and owner cleanup rather than identical internal peaks.
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
    public void LiteralPathsPreserveResultsAndBoundedOwnership(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        const long cap = 1024 * 1024;
        using (var owner = budget.CreateScope())
        {
            var options = new LythonRunOptions { MemoryBudget = owner, MaxExecutionMemoryBytes = cap };
            using (var executableResult = new LythonRuntime().Run(executable, new MockLythonHost(), options))
            using (var loweredResult = new LythonRuntime().Run(lowered, new MockLythonHost(), options))
            {
                Assert.True(executableResult.Success, executableResult.Failure?.Message);
                Assert.True(loweredResult.Success, loweredResult.Failure?.Message);
                Assert.Equal(executableResult.ReturnValue, loweredResult.ReturnValue);
                Assert.InRange(executableResult.PeakExecutionMemoryBytes, 1, cap);
                Assert.InRange(loweredResult.PeakExecutionMemoryBytes, 1, cap);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
