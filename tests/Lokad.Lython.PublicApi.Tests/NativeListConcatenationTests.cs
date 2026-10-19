using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeListConcatenationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcatenationPreservesCyclesAliasesAndIndependentResults(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile("""
            def check():
                child = []
                child.append(child)
                left = [child]
                right = [child, 7]
                joined = left + right
                joined.append(9)
                right.append(8)
                empty = []
                fresh = empty + empty
                return [len(left), len(right), len(joined), joined[0] is child,
                        joined[0] is joined[1], joined is left or joined is right,
                        child[0] is child, fresh is empty]
            return check()
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        for (var batch = 0; batch < 2; batch++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { BigInteger.One, new BigInteger(3), new BigInteger(4),
                    true, true, false, true, false }, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedPrepaidCopiesRefuseAndLaterCommandsReuseTheSharedAllowance(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import difflib
            matcher = difflib.SequenceMatcher(None, [], [0], autojunk=False)
            value = matcher.b2j[0]
            kept = []
            print('source')
            if grow:
                for i in range(16):
                    value = value + value
                    kept.append(value)
            return len(value)
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var grow in new[] { true, false })
        {
            var options = new LythonRunOptions { MemoryBudget = budget,
                Globals = new Dictionary<string, object?> { ["grow"] = grow } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.Equal("source\n", result.StandardOutput);
                if (grow)
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(BigInteger.One, result.ReturnValue);
                }
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
