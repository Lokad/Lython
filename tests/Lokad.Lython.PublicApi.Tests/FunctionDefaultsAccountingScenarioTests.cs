using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Retained default containers and shared compiled parameter layouts stay funded.
// Ordinary fixtures use one small execution batch; original 5000-definition
// scales remain explicit stress coverage. Layout metadata belongs to compilation,
// while private defaults remain charged through their execution batch.
public sealed class FunctionDefaultsAccountingScenarioTests
{
    private static string DefaultParams()
        => string.Join(", ", Enumerable.Range(1, 40).Select(k => "a" + k.ToString("D2") + "=0"));

    private static async Task RetainedDefaults(bool lambda, int count, long limit)
    {
        var definition = lambda
            ? "    fs.append(lambda " + DefaultParams() + ": a01)\n"
            : "    def f(" + DefaultParams() + "):\n        return a01\n    fs.append(f)\n";
        await RunBatch("fs=[]\ni=0\nwhile i < " + count + ":\n" + definition + "    i=i+1\nreturn len(fs)\n", limit, false);
    }

    private static async Task DroppedDefaults(int count, long limit, bool success)
    {
        await RunBatch("i=0\ntotal=0\nwhile i < " + count + ":\n    def f(" + DefaultParams() + "):\n        return a01\n    total=total+f()\n    i=i+1\nreturn total\n", limit, success);
    }

    private static async Task RunBatch(string source, long limit, bool success)
    {
        var budget = new LythonMemoryBudget(limit);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.Equal(success, result.Success);
                    if (success) Assert.Equal(new BigInteger(0), result.ReturnValue);
                    else
                    {
                        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                        Assert.True(result.FailureAttribution?.Requested > 0);
                    }
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public Task ManyRetainedDefDefaultsStayCharged() => RetainedDefaults(false, 64, 128 * 1024);

    [Fact]
    public Task ManyRetainedLambdaDefaultsStayCharged() => RetainedDefaults(true, 64, 128 * 1024);

    [Fact]
    public Task DroppedDefDefaultsFitRoomyBudget() => DroppedDefaults(64, 1024 * 1024, true);

    [Fact, Trait("Category", "Stress")]
    public Task OriginalRetainedDefDefaultsStayCharged() => RetainedDefaults(false, 5000, 6291456);

    [Fact, Trait("Category", "Stress")]
    public Task OriginalRetainedLambdaDefaultsStayCharged() => RetainedDefaults(true, 5000, 6291456);

    [Fact, Trait("Category", "Stress")]
    public Task OriginalDiscardedDefaultsRemainChargedToTheirBatch() => DroppedDefaults(5000, 6 * 1024 * 1024, false);

    [Fact, Trait("Category", "Stress")]
    public Task OriginalDiscardedDefaultBatchFitsWithSharedLayout() => DroppedDefaults(5000, 20 * 1024 * 1024, true);

    [Fact]
    public async Task DefaultValuesStillApply()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "def f(a=1, b=2):\n" +
            "    return a + b\n" +
            "g = lambda x=5: x * 2\n" +
            "return [f(), f(10), f(10, 20), g(), g(21)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(12), new BigInteger(30), new BigInteger(10), new BigInteger(42) };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
