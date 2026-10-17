using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringReplacementBulkTests
{
    [Theory]
    [InlineData("", "", "|", 0, "")]
    [InlineData("", "", "|", 4, "|")]
    [InlineData("éλ😀", "", "|", 0, "éλ😀")]
    [InlineData("éλ😀", "", "|", 1, "|éλ😀")]
    [InlineData("éλ😀", "", "|", 2, "|é|λ😀")]
    [InlineData("éλ😀", "", "|", 3, "|é|λ|😀")]
    [InlineData("éλ😀", "", "|", 4, "|é|λ|😀|")]
    [InlineData("éλ😀", "", "|", 99, "|é|λ|😀|")]
    [InlineData("éλ😀", "", "|", -3, "|é|λ|😀|")]
    [InlineData("éλ😀", "", "|", -2147483648, "|é|λ|😀|")]
    [InlineData("éλ😀", "", "|", 2147483647, "|é|λ|😀|")]
    [InlineData("éλ😀", "", "", 2, "éλ😀")]
    [InlineData("aaaaa", "aa", "x", 1, "xaaa")]
    [InlineData("aaaaa", "aa", "x", 2, "xxa")]
    [InlineData("aaaaa", "aa", "x", -1, "xxa")]
    [InlineData("ababa", "aba", "🦊", 2, "🦊ba")]
    [InlineData("😀é😀", "😀", "", 1, "é😀")]
    [InlineData("😀é😀", "😀", "", -1, "é")]
    [InlineData("a\u0000b", "\u0000", "😀", 1, "a😀b")]
    public async Task ReplacementMatchesPython(string value, string oldValue, string newValue, int count, string expected)
    {
        using var script = new LythonEngine().Compile("return value.replace(old, new, count)");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
            { ["value"] = value, ["old"] = oldValue, ["new"] = newValue, ["count"] = count } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedUnicodeReplacementKeepsAReasonableWorkingSetAndRetainedAliases(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("text='éλ😀'*256\nvalue=text.replace('', '|', 1)\nreturn [value,value]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var expected = "|" + string.Concat(Enumerable.Repeat("éλ😀", 256));
        for (var i = 0; i < 2; i++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 32768 })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 32768 }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { expected, expected }, Assert.IsType<List<object?>>(result.ReturnValue));
                Assert.True(budget.ReservedBytes - baseline >= 2048);
                Assert.True(result.PeakExecutionMemoryBytes <= 32768);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
