using Lokad.Lython.Benchmarks.Comparison;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonWorkloadTests
{
    private static readonly IReadOnlyList<ComparisonWorkload> Catalog = WorkloadCatalog.Create();
    public static IEnumerable<object[]> Cases => Catalog.Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task SharedJobMatchesIndependentGoldenOutputWithNormalLimitsAndFreshState(string id)
    {
        var workload = Catalog.Single(c => c.Id == id);
        var script = new LythonEngine().Compile(workload.Source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        Assert.InRange(workload.Source.Length, 1, LythonEngine.MaxSourceLength);
        Assert.InRange(workload.ExpectedOutputUtf8Bytes, 0, LythonRunOptions.DefaultMaxStandardOutputBytes);

        // Reuse compiled code and fresh public invocations. Repetition detects
        // accidentally retained mutable state, exhausted generators and classes
        // whose fields survive one invocation. Async uses the same source too.
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var sync = script.Run(new MockLythonHost());
            var asynchronous = await script.RunAsync(new MockLythonHost());
            foreach (var result in new[] { sync, asynchronous })
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Null(result.ExitCode);
                Assert.Empty(result.StandardError);
                Assert.Equal(workload.ExpectedOutput, result.StandardOutput);
                Assert.Null(result.ReturnValue);
            }
        }
    }
}
