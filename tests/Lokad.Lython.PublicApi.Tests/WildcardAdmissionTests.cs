using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WildcardAdmissionTests
{
    [Theory]
    [InlineData("fnmatch.translate")]
    [InlineData("glob.translate")]
    [InlineData("glob.escape")]
    public void TranslationAdmitsConstructionBeforePublishingOutput(string helper)
    {
        using var script = new LythonEngine().Compile($"import fnmatch,glob\ntry:\n {helper}(pattern)\nexcept MemoryError:\n print('must not run')");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(96 * 1024);
        for (var i = 0; i < 2; i++)
        {
            using (var result = script.Run(new MockLythonHost(), new LythonRunOptions
            {
                MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["pattern"] = new string('*', 5000) },
            }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
