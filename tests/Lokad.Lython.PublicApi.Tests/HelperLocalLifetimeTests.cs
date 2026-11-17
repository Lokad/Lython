using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HelperLocalLifetimeTests
{
    public static IEnumerable<object[]> Programs()
    {
        yield return ["""
            def choose(flag, base):
                if flag: value = base + 7
                return value
            assert choose(True, 10) == 17
            try: choose(False, 40)
            except UnboundLocalError: pass
            else: raise AssertionError('an earlier invocation supplied an unassigned local')
            assert choose(True, 20) == 27
            print('ok')
            """];
        yield return ["""
            def leaf(value): return value + 1
            def inner(value):
                keep = value + 100
                return leaf(value) + keep
            def middle(value):
                before = value * 3
                after = inner(value)
                return before + after + value
            def recurse(depth, value):
                saved = value + 1
                if depth: return recurse(depth - 1, saved) + saved
                return middle(saved)
            assert middle(5) == 131
            assert recurse(12, 5) == 347
            print('ok')
            """];
        yield return ["""
            def pair(left, right): return left + right, left - right
            def check(value):
                saved = value + 1
                left, right = pair(saved, 3)
                return left, right, saved
            subject = [10, 20]
            match subject:
                case [left, right] if check(left) == (14, 8, 11):
                    assert (left, right) == (10, 20)
                case _: raise AssertionError('helper changed pattern state')
            assert check(20) == (24, 18, 21)
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RepeatedAndNestedHelpersPreserveLocalLifetimes(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
