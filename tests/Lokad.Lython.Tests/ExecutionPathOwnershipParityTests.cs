using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Investigation #5: ownership is identical on the instruction (executable)
// and lowered paths. Native executable ops track their fresh values inline
// while everything else funnels through shared lowered helpers via
// per-expression fallback, so the same script reports exactly the same
// accounted peak and value on both paths.
public sealed class ExecutionPathOwnershipParityTests
{
    // Literal shapes only: these take native executable ops on one path
    // and lowered evaluators on the other, so equal peaks prove identical
    // ownership. Comprehensions always funnel through the shared lowered
    // helpers (per-expression fallback), which cannot diverge by path.
    public static TheoryData<string> Programs => new()
    {
        "x = [1, 2, 3] * 500\nreturn len(x)\n",
        "x = {" + string.Join(",", System.Linq.Enumerable.Range(0, 200).Select(static i => i + ": " + i)) + "}\nreturn len(x)\n",
        "x = {" + string.Join(",", System.Linq.Enumerable.Range(0, 200).Select(static i => i.ToString())) + "}\nreturn len(x)\n",
        "x = (" + string.Join(",", System.Linq.Enumerable.Range(0, 200).Select(static i => i.ToString())) + ")\nreturn len(x)\n",
        "x = {" + string.Join(",", System.Linq.Enumerable.Range(0, 50).Select(static i => "\"k" + i + "\": " + i)) + "}\nreturn len(x)\n",
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public void ExecutableAndLoweredPathsReportIdenticalPeaks(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.True(
            frontend.Diagnostics.All(static d => d.Severity != LythonDiagnosticSeverity.Error),
            string.Join("|", frontend.Diagnostics.Select(static d => d.Code + ":" + d.Message)));
        var lowered = LoweredScript.Lower(frontend.Script!);
        ExecutableScript? executable = null;
        try
        {
            executable = ExecutableScript.Compile(lowered);
        }
        catch (ExecutableLoweringFallbackException)
        {
        }

        Assert.NotNull(executable);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1L << 40 };
        var executableResult = new LythonRuntime().Run(executable, new MockLythonHost(), options);
        var loweredResult = new LythonRuntime().Run(lowered, new MockLythonHost(), options);
        Assert.True(executableResult.Success, executableResult.Failure?.Message);
        Assert.True(loweredResult.Success, loweredResult.Failure?.Message);
        Assert.Equal(executableResult.ReturnValue, loweredResult.ReturnValue);
        Assert.Equal(executableResult.PeakExecutionMemoryBytes, loweredResult.PeakExecutionMemoryBytes);
    }
}
