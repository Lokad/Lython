using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Retained default containers and their parameter layouts remain funded.
// Ordinary fixtures use one small execution batch; original 5000-definition
// scales remain explicit stress coverage. Capacity estimates now include the
// layout maps/arrays, so 20 MiB is no longer a roomy 5000-definition allowance.
public sealed class FunctionDefaultsAccountingScenarioTests
{
    private static string DefaultParams()
        => string.Join(", ", Enumerable.Range(1, 40).Select(k => "a" + k.ToString("D2") + "=0"));

    private static async Task RetainedDefaults(bool lambda, int count, long limit)
    {
        var definition = lambda
            ? "    fs.append(lambda " + DefaultParams() + ": a01)\n"
            : "    def f(" + DefaultParams() + "):\n        return a01\n    fs.append(f)\n";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("fs=[]\ni=0\nwhile i < " + count + ":\n" + definition + "    i=i+1\nreturn len(fs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        using var asynchronous = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asynchronous.Success);
        Assert.Equal("MemoryError", asynchronous.Failure?.ExceptionType);
    }

    private static async Task DroppedDefaults(int count, long limit, bool success)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("i=0\ntotal=0\nwhile i < " + count + ":\n    def f(" + DefaultParams() + "):\n        return a01\n    total=total+f()\n    i=i+1\nreturn total\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.Equal(success, result.Success);
            if (success) Assert.Equal(new BigInteger(0), result.ReturnValue);
            else Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
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
    public Task OriginalDiscardedDefaultBatchRefusesAtItsOldAllowance() => DroppedDefaults(5000, 20971520, false);

    [Fact, Trait("Category", "Stress")]
    public Task OriginalDiscardedDefaultBatchFitsItsConservativeCapacity() => DroppedDefaults(5000, 64 * 1024 * 1024, true);

    [Fact]
    public async Task DefaultValuesStillApply()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "def f(a=1, b=2):\n" +
            "    return a + b\n" +
            "g = lambda x=5: x * 2\n" +
            "return [f(), f(10), f(10, 20), g(), g(21)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(12), new BigInteger(30), new BigInteger(10), new BigInteger(42) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
