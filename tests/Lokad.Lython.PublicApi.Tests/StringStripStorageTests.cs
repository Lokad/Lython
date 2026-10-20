using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringStripStorageTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "s='😀éλwordλé😀'\nprint(s.strip('é😀λé'),s.lstrip('é😀λé'),s.rstrip('é😀λé'))", "word wordλé😀 😀éλword\n" },
        { "s='e\\u0301word\\u0301e'\nprint(repr(s.strip('e')),s.strip('e\\u0301'),s.strip('e\\u0301') is s)", "'́word́' word False\n" },
        { "s='ababxabba'\nprint(s.strip('ab'),s.lstrip('ab'),s.rstrip('ab'),s.strip('') is s)", "x xabba ababx True\n" },
        { "s='😀word😀'\nfor m in [s.strip,str.strip.__get__(s,str)]: print(m(*('😀',)))\nprint(str.strip(s,'😀'))", "word\nword\nword\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CustomCharacterSetsPreservePythonBehavior(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("strip", false)]
    [InlineData("strip", true)]
    [InlineData("lstrip", false)]
    [InlineData("lstrip", true)]
    [InlineData("rstrip", false)]
    [InlineData("rstrip", true)]
    public async Task RefusedWorkingStorageCannotBeSuppressedAndTheNextCommandReusesAllowance(string method, bool asynchronous)
    {
        var source = "chars='x'*(16384 if fail else 1)\ntext='word'\nprint('before')\ntry:\n value=text." + method
            + "(chars)\nexcept MemoryError:print('caught')\nfinally:\n if fail:return 'suppressed'\nreturn value";
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var fail in new[] { true, false })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = fail } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.Equal(!fail, result.Success);
                    Assert.Equal("before\n", result.StandardOutput);
                    if (fail) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    else Assert.Equal("word", result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("strip", "word")]
    [InlineData("lstrip", "word😀é")]
    [InlineData("rstrip", "é😀word")]
    public async Task ConcurrentRetainedResultsAndAliasesStayFunded(string method, string expected)
    {
        var source = "value='é😀word😀é'." + method + "('é😀')\nalias=value\nreturn [value,alias,alias is value]";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(retained.Success, retained.Failure?.Message);
                    var values = Assert.IsType<List<object?>>(retained.ReturnValue);
                    Assert.Equal(expected, Assert.IsType<string>(values[0]));
                    Assert.Equal(expected, Assert.IsType<string>(values[1]));
                    Assert.Equal(true, values[2]);
                    var held = budget.ReservedBytes;
                    Assert.True(held > baseline);
                    using (var other = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                    {
                        Assert.True(other.Success, other.Failure?.Message);
                        Assert.True(budget.ReservedBytes > held);
                    }
                    Assert.Equal(held, budget.ReservedBytes);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
