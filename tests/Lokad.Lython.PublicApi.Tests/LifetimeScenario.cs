using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Retained-data controls use the original iteration policy. Batch controls
// relinquish the execution and result before reusing the shared allowance.
[Collection("RuntimeStress")]
public abstract class LifetimeScenario
{
    protected virtual int Iterations(int stressIterations) => Math.Min(stressIterations, 2048);

    protected virtual long Budget(long stressBudget) => Math.Min(stressBudget, 65536);

    protected virtual int BatchSize => 8;

    protected int BatchIterations(int stressIterations) => Math.Min(Iterations(stressIterations), BatchSize);

    protected virtual int BatchRepetitions(int iterations) => 8;

    protected async Task AssertBatchCompletes(string source, object? expected, long memoryLimit, int iterations = 0,
        Func<ILythonHost>? hostFactory = null, bool? asynchronous = null, string? standardOutput = null)
    {
        var allowance = Math.Max(512 * 1024, memoryLimit + 256 * 1024);
        var budget = new LythonMemoryBudget(allowance);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = budget };
        for (var batch = 0; batch < BatchRepetitions(iterations); batch++)
        {
            foreach (var mode in asynchronous.HasValue ? new[] { asynchronous.Value } : new[] { false, true })
            {
                var host = hostFactory?.Invoke() ?? new MockLythonHost();
                using (var result = mode ? await script.RunAsync(host, options) : script.Run(host, options)) Verify(result);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        void Verify(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            if (expected is string text) Assert.Equal(text, result.ReturnValue?.ToString());
            else Assert.Equal(expected, result.ReturnValue);
            if (standardOutput is not null) Assert.Equal(standardOutput, result.StandardOutput);
            Assert.InRange(budget.ReservedBytes, baseline, allowance);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, allowance - baseline);
        }
    }
}
